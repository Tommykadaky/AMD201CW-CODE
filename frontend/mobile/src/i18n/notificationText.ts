// Translates notifications into the selected language.
// Known types (heatwave, streak, pump, soil_dry, water_tank) use the strings in LanguageContext + the server's Data values.
// Unknown types show the English Title/Body set by the sender.
import type { AppNotification } from "../api/notifications";
import type { Strings } from "./LanguageContext";

// Replace {name} placeholders with values from the notification data
function fill(template: string, data: Record<string, string>) {
  return template.replace(/\{(\w+)\}/g, (match, key: string) => data[key] ?? match);
}

export function notificationText(n: AppNotification, t: Strings): { title: string; body: string } {
  if (n.type === "heatwave" && n.data?.maxTemp) {
    return { title: t.notifHeatwaveTitle, body: t.notifHeatwaveBody.replace("{temp}", n.data.maxTemp) };
  }
  if (n.type === "streak" && n.data?.days) {
    return {
      title: t.notifStreakTitle.replace("{days}", n.data.days),
      body: t.notifStreakBody.replace("{days}", n.data.days),
    };
  }
  // Sensor service (pump / dry soil / water tank) always sends device, moisture (and seconds for the pump)
  if (n.data?.device && n.data.moisture) {
    if (n.type === "pump" && n.data.seconds) return { title: t.notifPumpTitle, body: fill(t.notifPumpBody, n.data) };
    if (n.type === "water_tank" && n.data.seconds) return { title: t.notifTankTitle, body: fill(t.notifTankBody, n.data) };
    if (n.type === "soil_dry") return { title: t.notifDryTitle, body: fill(t.notifDryBody, n.data) };
  }
  return { title: n.title, body: n.body };
}
