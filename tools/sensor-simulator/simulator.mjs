#!/usr/bin/env node
// ---------------------------------------------------------------------------
// TreeO sensor simulator - behaves like the ESP32 so the team can test and demo without hardware.
//
//   node simulator.mjs
//   node simulator.mjs --device TREEO_SIM_02 --start 25 --dry-rate 10
//   node simulator.mjs --empty-tank          (pump runs but the soil stays dry -> "check the water tank")
//
// It uses the same API, the same device key header and the same pump rules as firmware/esp32-moisture.
// Needs Node 18 or newer (already installed for the Expo app). No npm install needed.
// ---------------------------------------------------------------------------

const args = parseArgs(process.argv.slice(2));
if (args.help) {
  console.log(`Options:
  --url <url>          Sensor service address        (default http://localhost:5182)
  --device <id>        Device ID                     (default TREEO_SIM_01)
  --key <key>          Device key, 16+ characters    (default sim-key-change-me-0123456789)
  --start <percent>    Starting soil moisture        (default 45)
  --dry-rate <n>       Moisture lost per minute, %   (default 3; use 10+ for a fast demo)
  --interval <s>       Seconds between reports       (default 5)
  --empty-tank         The pump runs but no water comes out`);
  process.exit(0);
}

const SERVER_URL = (args.url ?? "http://localhost:5182").replace(/\/$/, "");
const DEVICE_ID = args.device ?? "TREEO_SIM_01";
const DEVICE_KEY = args.key ?? "sim-key-change-me-0123456789";
const REPORT_INTERVAL_MS = Number(args.interval ?? 5) * 1000;
const DRY_RATE_PER_SECOND = Number(args["dry-rate"] ?? 3) / 60;
const EMPTY_TANK = Boolean(args["empty-tank"]);
const PUMP_GAIN_PER_SECOND = 4;                 // how fast the pump wets the soil

// Same safety values as the firmware
const AUTO_RUN_MAX_MS = 8000;
const MANUAL_RUN_MAX_MS = 15000;
const MIN_REST_MS = 60000;
const HARD_STOP_PERCENT = 85;

// Settings from the server (defaults until the first reply)
let dryThreshold = 30;
let wetThreshold = 60;
let autoWatering = true;

// State
let soil = clamp(Number(args.start ?? 45), 0, 100);
let pumpOn = false;
let pumpManual = false;
let pumpStartedAt = 0;
let pumpLimitMs = 0;
let pumpStoppedAt = -Infinity;
let lastReportAt = -Infinity;
let reportNow = true;
let sending = false;

const moisture = () => Math.round(soil);

function startPump(limitMs, manual) {
  pumpOn = true;
  pumpManual = manual;
  pumpStartedAt = Date.now();
  pumpLimitMs = limitMs;
  reportNow = true;
  log(`Pump ON (${manual ? "manual" : "auto"}, max ${limitMs / 1000}s) at ${moisture()}%`);
}

function stopPump(reason) {
  pumpOn = false;
  pumpStoppedAt = Date.now();
  reportNow = true;
  log(`Pump OFF (${reason}) at ${moisture()}%`);
}

// One simulated second: the soil dries out, or gets wetter while the pump runs
function tick() {
  const now = Date.now();
  const noise = (Math.random() - 0.5) * 0.4;
  soil = clamp(soil - DRY_RATE_PER_SECOND + noise + (pumpOn && !EMPTY_TANK ? PUMP_GAIN_PER_SECOND : 0), 0, 100);

  if (pumpOn) {
    if (now - pumpStartedAt >= pumpLimitMs) stopPump("time limit");
    else if (moisture() >= HARD_STOP_PERCENT) stopPump("hard stop");
    else if (!pumpManual && moisture() >= wetThreshold) stopPump("wet enough");
  } else if (autoWatering && moisture() < dryThreshold && now - pumpStoppedAt >= MIN_REST_MS) {
    startPump(AUTO_RUN_MAX_MS, false);
  }

  const due = now - lastReportAt >= REPORT_INTERVAL_MS;
  if (!sending && (reportNow || (due && !pumpOn))) {
    reportNow = false;
    lastReportAt = now;
    sendReport();
  }
}

async function sendReport() {
  sending = true;
  const body = { deviceId: DEVICE_ID, moisture: moisture(), pumpOn, wifiRssi: -55, firmware: "simulator" };
  try {
    const res = await fetch(`${SERVER_URL}/api/sensors/readings`, {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-Device-Key": DEVICE_KEY },
      body: JSON.stringify(body),
      signal: AbortSignal.timeout(2000),
    });

    if (res.status === 401) {
      log("401 - device ID / key not accepted. Was this ID paired with a different key?");
    } else if (!res.ok) {
      log(`Server answered ${res.status}: ${await res.text()}`);
    } else {
      const reply = await res.json();
      if (reply.dryThreshold < reply.wetThreshold) {
        dryThreshold = reply.dryThreshold;
        wetThreshold = reply.wetThreshold;
      }
      autoWatering = Boolean(reply.autoWatering);
      log(`Sent ${body.moisture}% pump=${pumpOn ? "on" : "off"} | auto=${autoWatering ? "on" : "off"} range ${dryThreshold}-${wetThreshold}%`);
      if (reply.pumpSeconds > 0 && !pumpOn && moisture() < HARD_STOP_PERCENT) startPump(Math.min(reply.pumpSeconds * 1000, MANUAL_RUN_MAX_MS), true);
    }
  } catch (e) {
    log(`Cannot reach ${SERVER_URL} (${e.name === "TimeoutError" ? "timeout" : e.message}). Is Sensor.Api running?`);
  } finally {
    sending = false;
  }
}

function parseArgs(list) {
  const out = {};
  for (let i = 0; i < list.length; i++) {
    const a = list[i];
    if (!a.startsWith("--")) continue;
    const name = a.slice(2);
    const next = list[i + 1];
    if (next === undefined || next.startsWith("--")) out[name] = true;
    else out[name] = list[++i];
  }
  return out;
}

function clamp(v, lo, hi) {
  return Math.min(hi, Math.max(lo, v));
}

function log(msg) {
  console.log(`[${new Date().toLocaleTimeString()}] ${msg}`);
}

log(`Simulating ${DEVICE_ID} -> ${SERVER_URL} (start ${moisture()}%, drying ${(DRY_RATE_PER_SECOND * 60).toFixed(1)}%/min${EMPTY_TANK ? ", EMPTY TANK" : ""}). Ctrl+C to stop.`);
setInterval(tick, 1000);
tick();
