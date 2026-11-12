# TreeO – API Reference

All endpoints are reachable through the **API Gateway** (`http://localhost:5100` locally) or directly on each service's port.
🔒 = needs `Authorization: Bearer <accessToken>` from Identity. Errors return JSON `{ "message": "..." }` (validation errors: `{ "errors": { ... } }`).

Every service also has `GET /health` (no login). The gateway adds `GET /health/all`, which checks every service.

## Identity (5101)

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/register` | – | Create an account (always role User). Rate-limited at the gateway |
| POST | `/api/auth/login` | – | Returns `accessToken`, `refreshToken`, `expiresAt`, `user`. Rate-limited |
| POST | `/api/auth/refresh` | – | Exchange a refresh token for new tokens |
| POST | `/api/auth/logout` | – | Revoke a refresh token |
| GET | `/api/auth/me` | 🔒 | Current user |
| PUT | `/api/auth/password` | 🔒 | Change password (signs out other devices) |
| POST | `/api/auth/forgot-password` | – | Email a 6-digit reset code (valid 15 min). Rate-limited |
| POST | `/api/auth/reset-password` | – | Set a new password with the code. Rate-limited |
| GET | `/api/admin/users` | 🔒 Admin | List users |
| PUT | `/api/admin/users/{id}/role` | 🔒 Admin | Change role: User / Expert / Admin |

## Forum (5107)

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/forum/posts?filter=all\|experts\|following` | 🔒 | Posts, newest first |
| POST | `/api/forum/posts` | 🔒 | Create a post `{ content }` (1–1000 characters) |
| DELETE | `/api/forum/posts/{id}` | 🔒 | Delete own post (Admin: any post) |
| POST / DELETE | `/api/forum/posts/{id}/like` | 🔒 | Like / unlike |
| POST / DELETE | `/api/forum/follows/{authorId}` | 🔒 | Follow / unfollow an author |

## Garden (5103)

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/garden/streak` | 🔒 | Current and longest streak, watered today, at risk, last 7 days |
| POST | `/api/garden/watering` | 🔒 | Record today's watering; sends a notification at 3/7/14/30/60/100/200/365 days |

## Reminder (5104)

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/weather?lat=&lon=&name=` | 🔒 | Current weather + 3-day forecast + heatwave flags (default: Ho Chi Minh City) |

## Notification (5105)

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/notifications?take=30` | 🔒 | Latest notifications + unread count |
| POST | `/api/notifications/{id}/read` | 🔒 | Mark one as read |
| POST | `/api/notifications/read-all` | 🔒 | Mark all as read |
| DELETE | `/api/notifications/{id}` | 🔒 | Delete one |

Notification types: `streak`, `heatwave`, `pump`, `soil_dry`, `water_tank` (any other type is shown with its English title and body).

## AI Assistant (5106)

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/api/ai/chat` | 🔒 | `{ mode: "care" \| "fengshui", messages: [{ role, content }] }` → `{ reply }`. 10 requests/min per user |

## Sensor (5182)

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/api/sensors/readings` | device key (`X-Device-Key`) | ESP32 report `{ deviceId, moisture, pumpOn, wifiRssi?, firmware? }` → `{ pumpSeconds, autoWatering, dryThreshold, wetThreshold }` |
| GET | `/api/sensors/devices` | 🔒 | My devices with live values (moisture, online, pump, last watered) |
| POST | `/api/sensors/devices` | 🔒 | Pair a device `{ deviceId, deviceKey, name? }` |
| GET | `/api/sensors/devices/{id}` | 🔒 | One device |
| PATCH | `/api/sensors/devices/{id}` | 🔒 | `{ name?, autoWatering? }` |
| DELETE | `/api/sensors/devices/{id}` | 🔒 | Unpair and delete the device's history |
| GET | `/api/sensors/devices/{id}/history?hours=24` | 🔒 | Hourly average/min/max + pump runs (1–168 hours) |
| GET | `/api/sensors/devices/{id}/activity?limit=10` | 🔒 | Latest pump runs |
| POST | `/api/sensors/devices/{id}/water` | 🔒 | Water now `{ seconds? }` (1–15, default 5) → 202; 409 if the device is offline or already pumping |

## RabbitMQ event

Exchange `treeo.events` (topic) · routing key `notification.<type>` · queue `notification.requests` (binding `notification.#`).

```json
{
  "userId": "8a7c…",
  "type": "pump",
  "title": "Plant watered",
  "body": "Money Tree was watered for 5s. Soil moisture is now 63%.",
  "data": { "device": "Money Tree", "seconds": "5", "moisture": "63", "trigger": "manual" },
  "dedupKey": "pump:TREEO_ESP32_01:42",
  "occurredAtUtc": "2026-11-10T09:00:00Z"
}
```
