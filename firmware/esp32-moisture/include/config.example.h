// ---------------------------------------------------------------------------
// TreeO ESP32 - personal settings
//
// 1. Copy this file to "config.h" in the same folder (include/config.h)
// 2. Fill in your Wi-Fi, the computer's IP and a device key
//
// config.h is listed in .gitignore, so your Wi-Fi password never goes to GitHub.
// ---------------------------------------------------------------------------
#pragma once

// Wi-Fi the ESP32 joins (2.4 GHz only; the ESP32 cannot see 5 GHz networks)
#define WIFI_SSID      "your-wifi-name"
#define WIFI_PASSWORD  "your-wifi-password"

// Address of the Sensor service: the IP of the computer running "dotnet run" + port 5182.
// Find the IP with "ipconfig" (Windows) -> IPv4 Address. The ESP32 and the computer must share the Wi-Fi.
#define SERVER_URL     "http://192.168.1.10:5182"

// Identity of this board. DEVICE_KEY is its password: at least 16 characters, keep it secret.
// You type the same ID and key in the app (Settings -> IoT device -> Pair device).
#define DEVICE_ID      "TREEO_ESP32_01"
#define DEVICE_KEY     "change-me-to-a-long-random-key"

// Wi-Fi upload (OTA). After the first upload over USB, new firmware can be sent over Wi-Fi.
// OTA_PASSWORD must match the TREEO_OTA_PASSWORD environment variable on the computer (see platformio.ini).
#define OTA_HOSTNAME   "treeo-esp32"
#define OTA_PASSWORD   "change-me-ota-password"
