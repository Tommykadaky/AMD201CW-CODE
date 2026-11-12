// Calls to the Sensor service (services/sensor): live soil moisture, 24-hour history, pump control
import { SENSOR_URL } from "./config";
import type { AuthFetch } from "./http";

export interface Device {
  id: string;
  name: string;
  online: boolean;                 // reported in the last 30 seconds
  lastSeenAtUtc: string | null;
  moisture: number | null;         // 0-100 %, null before the first reading
  pumpOn: boolean;
  autoWatering: boolean;
  wateringPending: boolean;        // a "Water now" command is waiting for the device
  ipAddress: string | null;
  wifiRssi: number | null;         // dBm, e.g. -58
  firmware: string | null;
  dryThreshold: number;            // below this the soil needs water
  wetThreshold: number;            // the pump stops here
  lastWateredAtUtc: string | null;
  tankEmpty?: boolean;             // the last pump run did not wet the soil (empty tank?)
}

export interface HistoryPoint {
  hourStartUtc: string;
  average: number | null;          // null = no data in that hour
  min: number | null;
  max: number | null;
}

export interface PumpRun {
  id: number;
  trigger: "auto" | "manual";
  startedAtUtc: string;
  endedAtUtc: string | null;
  durationSeconds: number | null;
  moistureBefore: number | null;
  moistureAfter: number | null;
}

export interface History {
  hours: number;
  points: HistoryPoint[];          // one per hour, the last one is the current hour
  pumpRuns: PumpRun[];             // oldest first
}

const base = `${SENSOR_URL}/api/sensors/devices`;
const json = (body: unknown): RequestInit => ({ body: JSON.stringify(body) });
const id = (deviceId: string) => encodeURIComponent(deviceId);

export const sensorApi = (authFetch: AuthFetch) => ({
  list: () => authFetch<Device[]>(base),

  get: (deviceId: string) => authFetch<Device>(`${base}/${id(deviceId)}`),

  pair: (deviceId: string, deviceKey: string, name?: string) =>
    authFetch<Device>(base, { method: "POST", ...json({ deviceId, deviceKey, name }) }),

  update: (deviceId: string, changes: { name?: string; autoWatering?: boolean }) =>
    authFetch<Device>(`${base}/${id(deviceId)}`, { method: "PATCH", ...json(changes) }),

  unpair: (deviceId: string) => authFetch<void>(`${base}/${id(deviceId)}`, { method: "DELETE" }),

  history: (deviceId: string, hours = 24) => authFetch<History>(`${base}/${id(deviceId)}/history?hours=${hours}`),

  water: (deviceId: string, seconds = 5) =>
    authFetch<{ seconds: number; message: string }>(`${base}/${id(deviceId)}/water`, { method: "POST", ...json({ seconds }) }),
});
