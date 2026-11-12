// ---------------------------------------------------------------------------
// TreeO - ESP32 soil moisture sensor + water pump
//
// What it does:
//   - reads the capacitive soil sensor every second (averaged + smoothed)
//   - waters automatically when the soil is too dry, stops when it is wet enough
//   - reports to the Sensor service every 5 s (and right away when the pump switches)
//   - follows the server's reply: thresholds, auto-watering on/off, "Water now" commands
//   - accepts new firmware over Wi-Fi (OTA), so no USB cable is needed after the first upload
//
// The pump logic runs on the board itself, so the plant is still watered when
// Wi-Fi or the server is down. The loop never blocks (no long delay()), so the
// pump safety timer is always checked.
// ---------------------------------------------------------------------------
#include <Arduino.h>
#include <WiFi.h>
#include <HTTPClient.h>
#include <ArduinoJson.h>
#include <ArduinoOTA.h>

#if __has_include("config.h")
#include "config.h"
#else
#error "Missing include/config.h - copy include/config.example.h to include/config.h and fill it in."
#endif

#define FIRMWARE_VERSION "1.2.1"

// OTA settings may be missing from an older config.h: fall back to safe defaults
#ifndef OTA_HOSTNAME
#define OTA_HOSTNAME "treeo-esp32"
#endif
#ifndef OTA_PASSWORD
#warning "OTA_PASSWORD is not set in config.h: anyone on your Wi-Fi could upload firmware. Copy it from config.example.h."
#define OTA_PASSWORD ""
#endif

// ----- Hardware -----
const int SOIL_PIN  = 36;          // sensor signal (SN / VP)
const int RELAY_PIN = 17;          // relay IN pin (GPIO17, as in the team's wiring diagram)
const int RELAY_ON  = HIGH;        // swap HIGH/LOW if your relay module is "active low"
const int RELAY_OFF = LOW;

// ----- Calibration (raw ADC values) -----
// The Serial Monitor prints "(raw N)" on every report: note N with the sensor in dry air -> ADC_DRY,
// and with the probe in a glass of water -> ADC_WET. Each sensor (and 3.3 V vs 5 V power) is different.
const int ADC_DRY = 3560;          // sensor in dry air
const int ADC_WET = 1500;          // sensor in a glass of water

// ----- Timing -----
const unsigned long SAMPLE_INTERVAL_MS = 1000;   // read the sensor
const unsigned long REPORT_INTERVAL_MS = 5000;   // send to the server
const unsigned long WIFI_RETRY_MS      = 10000;  // try to reconnect Wi-Fi
const uint16_t      HTTP_TIMEOUT_MS    = 2000;   // never wait longer for the server

// ----- Pump safety -----
const unsigned long AUTO_RUN_MAX_MS   = 8000;    // one automatic run lasts at most 8 s
const unsigned long MANUAL_RUN_MAX_MS = 15000;   // "Water now" lasts at most 15 s
const unsigned long MIN_REST_MS       = 60000;   // wait 1 min between automatic runs so the water can soak in
const int           HARD_STOP_PERCENT = 85;      // never pump above this moisture, whatever the command

// ----- Settings received from the server (defaults until the first reply) -----
int  dryThreshold = 30;
int  wetThreshold = 60;
bool autoWatering = true;

// ----- State -----
float smoothedMoisture = -1;       // -1 = no reading yet
int   lastRaw          = 0;        // last averaged ADC value, printed for calibration
int   moisturePercent  = 0;
bool  pumpOn           = false;
bool  pumpManual       = false;
bool  everPumped       = false;
unsigned long pumpStartedAt = 0;
unsigned long pumpLimitMs   = 0;
unsigned long pumpStoppedAt = 0;
unsigned long lastSampleAt  = 0;
unsigned long lastReportAt  = 0;
unsigned long lastWifiTryAt = 0;
bool reportNow = true;             // send a first report as soon as Wi-Fi is up
bool otaStarted = false;           // ArduinoOTA is started once, after the first Wi-Fi connection

// ---------------------------------------------------------------------------
// Sensor
// ---------------------------------------------------------------------------

// Average 10 raw samples (removes ADC noise), convert to %, then smooth over time
int readMoisturePercent() {
  long sum = 0;
  for (int i = 0; i < 10; i++) {
    sum += analogRead(SOIL_PIN);
    delayMicroseconds(200);
  }
  lastRaw = (int)(sum / 10);
  int raw = constrain(lastRaw, ADC_WET, ADC_DRY);
  int percent = map(raw, ADC_DRY, ADC_WET, 0, 100);

  smoothedMoisture = (smoothedMoisture < 0) ? percent : smoothedMoisture * 0.7f + percent * 0.3f;
  return (int)lroundf(smoothedMoisture);
}

// ---------------------------------------------------------------------------
// Pump
// ---------------------------------------------------------------------------

void startPump(unsigned long limitMs, bool manual) {
  digitalWrite(RELAY_PIN, RELAY_ON);
  pumpOn = true;
  pumpManual = manual;
  pumpStartedAt = millis();
  pumpLimitMs = limitMs;
  reportNow = true;
  Serial.printf("[Pump] ON (%s, max %lu s) at %d%%\n", manual ? "manual" : "auto", limitMs / 1000, moisturePercent);
}

void stopPump(const char* reason) {
  digitalWrite(RELAY_PIN, RELAY_OFF);
  pumpOn = false;
  everPumped = true;
  pumpStoppedAt = millis();
  reportNow = true;
  Serial.printf("[Pump] OFF (%s) at %d%%\n", reason, moisturePercent);
}

// Checked on every loop pass: the time limit and the hard stop always win
void checkPumpSafety(unsigned long now) {
  if (!pumpOn || now < pumpStartedAt) return;   // a time from before the pump started is not a timeout
  if (now - pumpStartedAt >= pumpLimitMs) stopPump("time limit");
  else if (moisturePercent >= HARD_STOP_PERCENT) stopPump("hard stop");
}

// Automatic watering with hysteresis: start below dryThreshold, stop at wetThreshold.
// The gap between the two values stops the pump from flicking on and off.
void updateAutoWatering(unsigned long now) {
  if (pumpOn) {
    if (!pumpManual && moisturePercent >= wetThreshold) stopPump("wet enough");
    return;
  }
  bool rested = !everPumped || (now - pumpStoppedAt >= MIN_REST_MS);
  if (autoWatering && moisturePercent < dryThreshold && rested) {
    startPump(AUTO_RUN_MAX_MS, false);
  }
}

// ---------------------------------------------------------------------------
// Wi-Fi (non-blocking: the pump keeps working while we reconnect)
// ---------------------------------------------------------------------------

void ensureWifi(unsigned long now) {
  if (WiFi.status() == WL_CONNECTED) return;
  if (lastWifiTryAt != 0 && now - lastWifiTryAt < WIFI_RETRY_MS) return;
  lastWifiTryAt = now;
  Serial.println("[WiFi] Connecting...");
  WiFi.disconnect();
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
}

// ---------------------------------------------------------------------------
// OTA: upload new firmware over Wi-Fi (PlatformIO env "esp32dev_ota")
// ---------------------------------------------------------------------------

void setupOta() {
  ArduinoOTA.setHostname(OTA_HOSTNAME);
  if (strlen(OTA_PASSWORD) > 0) ArduinoOTA.setPassword(OTA_PASSWORD);

  // The upload blocks the loop for several seconds, so the pump safety timer cannot run:
  // always switch the pump off before the upload starts.
  ArduinoOTA.onStart([]() {
    if (pumpOn) stopPump("firmware update");
    Serial.println("[OTA] Update started");
  });
  ArduinoOTA.onEnd([]() { Serial.println("\n[OTA] Done, rebooting"); });
  ArduinoOTA.onProgress([](unsigned int done, unsigned int total) {
    Serial.printf("[OTA] %u%%\r", done * 100 / total);
  });
  ArduinoOTA.onError([](ota_error_t error) { Serial.printf("[OTA] Error %u\n", error); });

  ArduinoOTA.begin();
  otaStarted = true;
  Serial.printf("[OTA] Ready: upload to %s.local or %s\n", OTA_HOSTNAME, WiFi.localIP().toString().c_str());
}

// ---------------------------------------------------------------------------
// Server
// ---------------------------------------------------------------------------

void applyServerReply(const String& body) {
  JsonDocument doc;
  if (deserializeJson(doc, body)) {
    Serial.println("[HTTP] Reply is not valid JSON");
    return;
  }

  int dry = doc["dryThreshold"] | dryThreshold;
  int wet = doc["wetThreshold"] | wetThreshold;
  if (dry >= 0 && wet <= 100 && dry < wet) {
    dryThreshold = dry;
    wetThreshold = wet;
  }
  autoWatering = doc["autoWatering"] | autoWatering;

  // The server only sends a command when the pump is off and the soil is below the hard stop,
  // but the board checks again: its own reading is newer than the one the server saw.
  int pumpSeconds = doc["pumpSeconds"] | 0;
  if (pumpSeconds > 0 && !pumpOn && moisturePercent < HARD_STOP_PERCENT) {
    unsigned long ms = min((unsigned long)pumpSeconds * 1000UL, MANUAL_RUN_MAX_MS);
    startPump(ms, true);
  }
}

void sendReport() {
  HTTPClient http;
  http.setConnectTimeout(HTTP_TIMEOUT_MS);
  http.setTimeout(HTTP_TIMEOUT_MS);
  http.begin(String(SERVER_URL) + "/api/sensors/readings");
  http.addHeader("Content-Type", "application/json");
  http.addHeader("X-Device-Key", DEVICE_KEY);

  JsonDocument doc;
  doc["deviceId"] = DEVICE_ID;
  doc["moisture"] = moisturePercent;
  doc["pumpOn"]   = pumpOn;
  doc["wifiRssi"] = WiFi.RSSI();
  doc["firmware"] = FIRMWARE_VERSION;
  String body;
  serializeJson(doc, body);

  int code = http.POST(body);
  if (code == 200) {
    applyServerReply(http.getString());
    Serial.printf("[HTTP] Sent %d%% (raw %d) pump=%s auto=%s\n", moisturePercent, lastRaw, pumpOn ? "on" : "off", autoWatering ? "on" : "off");
  } else if (code == 401) {
    Serial.println("[HTTP] 401 - DEVICE_ID / DEVICE_KEY not accepted (paired with a different key?)");
  } else if (code > 0) {
    Serial.printf("[HTTP] Server answered %d: %s\n", code, http.getString().c_str());
  } else {
    Serial.printf("[HTTP] Cannot reach %s (%s)\n", SERVER_URL, http.errorToString(code).c_str());
  }
  http.end();
}

// ---------------------------------------------------------------------------
// Setup & loop
// ---------------------------------------------------------------------------

void setup() {
  Serial.begin(115200);
  pinMode(RELAY_PIN, OUTPUT);
  digitalWrite(RELAY_PIN, RELAY_OFF);     // pump off at boot
  analogReadResolution(12);

  WiFi.mode(WIFI_STA);
  WiFi.setAutoReconnect(true);
  Serial.printf("\n[TreeO] Firmware %s, device %s\n", FIRMWARE_VERSION, DEVICE_ID);
}

void loop() {
  unsigned long now = millis();

  ensureWifi(now);

  if (WiFi.status() == WL_CONNECTED) {
    if (!otaStarted) setupOta();
    ArduinoOTA.handle();
  }

  if (now - lastSampleAt >= SAMPLE_INTERVAL_MS) {
    lastSampleAt = now;
    moisturePercent = readMoisturePercent();
    updateAutoWatering(millis());
  }

  // Always use a fresh millis() here, never the "now" taken at the top of the loop:
  // a pump started a moment ago has pumpStartedAt > now, and the unsigned subtraction
  // now - pumpStartedAt would wrap around to a huge number and stop the pump instantly.
  checkPumpSafety(millis());

  // Report on schedule, or immediately after the pump switched.
  // While the pump runs, only switch events are reported, so a slow server never delays the stop.
  bool due = now - lastReportAt >= REPORT_INTERVAL_MS;
  if ((reportNow || (due && !pumpOn)) && WiFi.status() == WL_CONNECTED) {
    reportNow = false;
    lastReportAt = now;
    sendReport();
    checkPumpSafety(millis());
  }

  delay(10);
}