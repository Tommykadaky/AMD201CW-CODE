// Hooks for the Sensor service: live device values (polled every 5 s while the screen is open),
// the 24-hour history, and "Water now" that waits until the pump has actually finished.
import { useCallback, useState } from "react";
import { useFocusEffect } from "expo-router";
import { useAuth } from "../auth/AuthContext";
import type { AuthFetch } from "../api/http";
import { sensorApi, type Device, type History } from "../api/sensor";

const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

// My paired devices. Polls only while the screen is focused, so a hidden screen costs nothing.
export function useDevices(pollMs = 5000) {
  const { authFetch } = useAuth();
  const [devices, setDevices] = useState<Device[] | null>(null);   // null = not loaded yet
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setDevices(await sensorApi(authFetch).list());
      setError(null);
    } catch (e) {
      setError((e as Error).message);
    }
  }, [authFetch]);

  useFocusEffect(
    useCallback(() => {
      load();
      const timer = setInterval(load, pollMs);
      return () => clearInterval(timer);
    }, [load, pollMs]),
  );

  // Replace one device in the list (after pairing, renaming, toggling auto-watering)
  const replace = useCallback((d: Device) => {
    setDevices((list) => {
      const rest = (list ?? []).filter((x) => x.id !== d.id);
      return [...rest, d];
    });
  }, []);

  const remove = useCallback((deviceId: string) => {
    setDevices((list) => (list ?? []).filter((x) => x.id !== deviceId));
  }, []);

  return {
    devices: devices ?? [],
    device: devices?.[0] ?? null,                 // the app shows one plant/device for now
    loaded: devices !== null || error !== null,
    error,
    reload: load,
    replace,
    remove,
  };
}

// Chart data. History changes slowly (one stored sample per minute), so once a minute is enough.
export function useDeviceHistory(deviceId: string | null) {
  const { authFetch } = useAuth();
  const [history, setHistory] = useState<History | null>(null);

  const load = useCallback(async () => {
    if (!deviceId) {
      setHistory(null);
      return;
    }
    try {
      setHistory(await sensorApi(authFetch).history(deviceId, 24));
    } catch {
      // keep the last chart on a temporary error
    }
  }, [authFetch, deviceId]);

  useFocusEffect(
    useCallback(() => {
      load();
      const timer = setInterval(load, 60_000);
      return () => clearInterval(timer);
    }, [load]),
  );

  return { history, reload: load };
}

// Sends "Water now" and resolves when the device reports the pump has switched off again.
// The device picks the command up within ~5 s, then pumps for `seconds`.
export async function waterAndWait(
  authFetch: AuthFetch,
  device: Device,
  seconds: number,
  isAlive: () => boolean,
): Promise<Device> {
  const api = sensorApi(authFetch);
  await api.water(device.id, seconds);

  const before = device.lastWateredAtUtc;
  const deadline = Date.now() + (seconds + 30) * 1000;
  while (Date.now() < deadline && isAlive()) {
    await sleep(1500);
    const now = await api.get(device.id);
    // Finished = a new pump run exists and the pump is off again
    if (!now.pumpOn && now.lastWateredAtUtc && now.lastWateredAtUtc !== before) return now;
  }
  throw new Error("timeout");
}

// "12s ago", "5m ago", "3h ago" for the live card
export function secondsSince(iso: string | null): number | null {
  if (!iso) return null;
  return Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 1000));
}
