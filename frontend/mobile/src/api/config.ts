// Backend addresses.
//
// Recommended: send everything through the API Gateway with ONE line in frontend/mobile/.env
//     EXPO_PUBLIC_GATEWAY_URL=http://<computer-IP>:5100        (or the server address after deploying)
//
// Without it, the app calls each service directly (default: localhost, same computer):
//     EXPO_PUBLIC_API_URL=http://<computer-IP>:5101            (Identity)
//     EXPO_PUBLIC_FORUM_URL=http://<computer-IP>:5107
//     EXPO_PUBLIC_GARDEN_URL=http://<computer-IP>:5103         (watering streak)
//     EXPO_PUBLIC_REMINDER_URL=http://<computer-IP>:5104       (weather)
//     EXPO_PUBLIC_NOTIFICATION_URL=http://<computer-IP>:5105
//     EXPO_PUBLIC_AI_URL=http://<computer-IP>:5106             (TreeO AI)
//     EXPO_PUBLIC_SENSOR_URL=http://<computer-IP>:5182         (soil sensor + pump)
// A per-service variable always wins over the gateway (handy for testing one service directly).
// A real phone and the computer must be on the same Wi-Fi.
//
// Note: Expo only replaces process.env.EXPO_PUBLIC_* when written out in full like below.
const trim = (url: string) => url.replace(/\/$/, "");

const GATEWAY_URL = process.env.EXPO_PUBLIC_GATEWAY_URL;
const pick = (specific: string | undefined, localDefault: string) => trim(specific ?? GATEWAY_URL ?? localDefault);

export const API_URL = pick(process.env.EXPO_PUBLIC_API_URL, "http://localhost:5101");                   // Identity
export const FORUM_URL = pick(process.env.EXPO_PUBLIC_FORUM_URL, "http://localhost:5107");               // Forum
export const GARDEN_URL = pick(process.env.EXPO_PUBLIC_GARDEN_URL, "http://localhost:5103");             // Garden (watering streak)
export const REMINDER_URL = pick(process.env.EXPO_PUBLIC_REMINDER_URL, "http://localhost:5104");         // Reminder (weather)
export const NOTIFICATION_URL = pick(process.env.EXPO_PUBLIC_NOTIFICATION_URL, "http://localhost:5105"); // Notification
export const AI_URL = pick(process.env.EXPO_PUBLIC_AI_URL, "http://localhost:5106");                     // AI Assistant
export const SENSOR_URL = pick(process.env.EXPO_PUBLIC_SENSOR_URL, "http://localhost:5182");             // Sensor (soil moisture + pump)
