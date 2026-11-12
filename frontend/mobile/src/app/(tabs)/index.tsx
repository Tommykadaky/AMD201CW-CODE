// Man Garden (Home)
// - Dien thoai: 1 cot (mockup "Mobile · Home Dashboard")
// - Man hinh rong: 2 cot + bieu do 24h + hoat dong gan day (mockup "Web · Dashboard")
// Thoi tiet (Reminder service), chuoi tuoi cay (Garden service) va chuong thong bao (Notification service) la DU LIEU THAT.
// Moisture, 24h chart, pump activity and "Water now" come from the Sensor service (Nguyen Le's part).
// With no sensor paired, demo values and a simulated pump are shown so the screen can still be demoed.
import { useEffect, useRef, useState } from "react";
import { Animated, Easing, Platform, Pressable, ScrollView, View } from "react-native";
import { router } from "expo-router";
import { Screen } from "../../components/Screen";
import { Card } from "../../components/Card";
import { Icon } from "../../components/Icon";
import { Txt } from "../../components/Txt";
import { Toggle } from "../../components/Toggle";
import { MoistureRing, moistureLevel } from "../../components/MoistureRing";
import { MoistureChart } from "../../components/MoistureChart";
import { useAuth } from "../../auth/AuthContext";
import { sensorApi, type Device, type History } from "../../api/sensor";
import { secondsSince, useDeviceHistory, useDevices, waterAndWait } from "../../hooks/useSensorData";
import { StreakWeek } from "../../components/StreakWeek";
import { useStreak, useUnreadCount, useWeather } from "../../hooks/useGardenData";
import { useIsWide } from "../../hooks/useIsWide";
import { useLang, type Strings } from "../../i18n/LanguageContext";
import type { Streak } from "../../api/garden";
import type { WeatherReport } from "../../api/weather";
import { colors, radius } from "../../theme";

const native = Platform.OS !== "web";

// Demo values shown only while no sensor is paired
const DEMO_MOISTURE = 42;
const PUMP_SECONDS = 5;

// "12s ago" / "5m ago" / "3h ago"
function ago(seconds: number | null, t: Strings) {
  if (seconds == null) return "";
  if (seconds < 60) return t.agoSeconds.replace("{n}", String(seconds));
  if (seconds < 3600) return t.agoMinutes.replace("{n}", String(Math.floor(seconds / 60)));
  return t.agoHours.replace("{n}", String(Math.floor(seconds / 3600)));
}

const pad = (n: number) => String(n).padStart(2, "0");

// "14:02" today, "09/11 14:02" on another day
function clock(iso: string) {
  const d = new Date(iso);
  const time = `${pad(d.getHours())}:${pad(d.getMinutes())}`;
  return d.toDateString() === new Date().toDateString() ? time : `${pad(d.getDate())}/${pad(d.getMonth() + 1)} ${time}`;
}

// Hourly points -> chart values. Empty hours repeat the previous value; null if there is not enough data yet.
function buildChart(history: History | null, t: Strings) {
  if (!history) return null;
  const known = history.points.filter((p) => p.average != null);
  if (known.length < 2) return null;

  let last = known[0].average as number;
  const values = history.points.map((p) => (last = p.average ?? last));
  const start = new Date(history.points[0].hourStartUtc).getTime();
  const marks = history.pumpRuns
    .map((r) => Math.floor((new Date(r.startedAtUtc).getTime() - start) / 3_600_000))
    .filter((i) => i >= 0 && i < values.length);
  const hourLabel = (i: number) => `${pad(new Date(history.points[i].hourStartUtc).getHours())}:00`;
  const n = values.length;
  const axis = [0, Math.round(n / 4), Math.round(n / 2), Math.round((3 * n) / 4)].map(hourLabel).concat(t.now);
  return { values, marks, axis };
}

// Latest pump runs for the "Recent activity" card
function activityItems(history: History | null, t: Strings) {
  return [...(history?.pumpRuns ?? [])]
    .reverse()
    .slice(0, 4)
    .map((r) => ({
      key: String(r.id),
      text:
        r.endedAtUtc == null
          ? t.activityRunning
          : (r.trigger === "manual" ? t.activityManual : t.activityAuto)
              .replace("{s}", String(r.durationSeconds ?? 0))
              .replace("{before}", String(r.moistureBefore ?? "?"))
              .replace("{after}", String(r.moistureAfter ?? "?")),
      time: clock(r.startedAtUtc),
      dot: r.trigger === "manual" ? colors.primary : colors.water,
    }));
}

function greeting() {
  const h = new Date().getHours();
  if (h < 12) return "Good morning";
  if (h < 18) return "Good afternoon";
  return "Good evening";
}

function todayLabel() {
  return new Date().toLocaleDateString("en-GB", { weekday: "long", day: "numeric", month: "long" });
}

// ---------- Cac khoi giao dien dung chung cho ca 2 bo cuc ----------

function StreakFlame() {
  const v = useRef(new Animated.Value(0)).current;
  useEffect(() => {
    const loop = Animated.loop(
      Animated.sequence([
        Animated.timing(v, { toValue: 1, duration: 700, easing: Easing.inOut(Easing.ease), useNativeDriver: native }),
        Animated.timing(v, { toValue: 0, duration: 700, easing: Easing.inOut(Easing.ease), useNativeDriver: native }),
      ]),
    );
    loop.start();
    return () => loop.stop();
  }, [v]);
  const scale = v.interpolate({ inputRange: [0, 1], outputRange: [1, 1.12] });
  const rotate = v.interpolate({ inputRange: [0, 1], outputRange: ["-4deg", "4deg"] });
  return (
    <Animated.View style={{ transform: [{ scale }, { rotate }] }}>
      <Icon name="flame" size={18} />
    </Animated.View>
  );
}

function StreakPill({ streak }: { streak: Streak | null }) {
  const { t } = useLang();
  const has = !!streak && streak.current > 0;
  return (
    <View style={{ gap: 10 }}>
      <View
        style={{
          alignSelf: "flex-start",
          flexDirection: "row",
          alignItems: "center",
          gap: 8,
          paddingVertical: 8,
          paddingHorizontal: 14,
          borderRadius: radius.pill,
          backgroundColor: colors.sunSoft,
          borderWidth: 1,
          borderColor: colors.sunLine,
        }}
      >
        <StreakFlame />
        <Txt weight="semibold" color={colors.warnText}>
          {has ? `${streak!.current}${t.streakDays}` : t.streakStart}
        </Txt>
      </View>
      {streak ? (
        <View style={{ gap: 6 }}>
          <StreakWeek streak={streak} />
          {streak.atRisk ? <Txt size={12} weight="medium" color={colors.warnIcon}>{t.streakAtRisk}</Txt> : null}
          {streak.wateredToday ? <Txt size={12} weight="medium" color={colors.primaryDark}>{t.streakDone}</Txt> : null}
        </View>
      ) : null}
    </View>
  );
}

// Chuong thong bao + huy hieu so thu chua doc. Bam de mo man Thong bao
function BellButton() {
  const { t } = useLang();
  const unread = useUnreadCount();
  return (
    <Pressable
      onPress={() => router.push("/notifications")}
      accessibilityRole="button"
      accessibilityLabel={unread > 0 ? `${t.notificationsLabel} (${unread})` : t.notificationsLabel}
      style={{ width: 44, height: 44, borderRadius: 22, borderWidth: 1, borderColor: colors.border, backgroundColor: "#FFFFFF", alignItems: "center", justifyContent: "center" }}
    >
      <Icon name="bell" size={20} />
      {unread > 0 ? (
        <View style={{ position: "absolute", top: -2, right: -2, minWidth: 18, height: 18, paddingHorizontal: 4, borderRadius: 9, backgroundColor: colors.danger, alignItems: "center", justifyContent: "center" }}>
          <Txt size={10} weight="bold" color="#FFFFFF">{unread > 9 ? "9+" : String(unread)}</Txt>
        </View>
      ) : null}
    </Pressable>
  );
}

function Avatar({ name }: { name?: string }) {
  return (
    <View style={{ width: 44, height: 44, borderRadius: 22, backgroundColor: colors.primary, alignItems: "center", justifyContent: "center" }}>
      <Txt weight="semibold" color="#FFFFFF">{(name ?? "?").charAt(0).toUpperCase()}</Txt>
    </View>
  );
}

function conditionLabel(code: string, t: Strings) {
  const key = `cond_${code}` as keyof Strings;
  return (t[key] as string | undefined) ?? t.cond_unknown;
}

function WeatherCard({ wide, weather, loading, error }: { wide?: boolean; weather: WeatherReport | null; loading: boolean; error: string | null }) {
  const { t } = useLang();

  if (!weather) {
    return (
      <Card style={wide ? { borderRadius: radius.hero, padding: 20 } : null}>
        <View style={{ flexDirection: "row", alignItems: "center", gap: 14 }}>
          <Icon name="sun" size={wide ? 48 : 44} />
          <View style={{ flex: 1, gap: 2 }}>
            <Txt color={colors.muted}>{loading ? t.weatherLoading : t.weatherUnavailable}</Txt>
            {!loading && error ? <Txt size={12} color={colors.muted}>{error}</Txt> : null}
          </View>
        </View>
      </Card>
    );
  }

  const tomorrow = weather.forecast[1];
  return (
    <Card style={wide ? { borderRadius: radius.hero, padding: 20 } : null}>
      <View style={{ flexDirection: "row", alignItems: "center", gap: 14 }}>
        <Icon name="sun" size={wide ? 48 : 44} />
        <View style={{ flex: 1, gap: 2 }}>
          <Txt size={wide ? 30 : 28} weight="bold">{Math.round(weather.current.temperatureC)}°C</Txt>
          <Txt size={13} color={colors.muted}>{conditionLabel(weather.current.condition, t)} · {weather.locationName}</Txt>
        </View>
        <View style={{ alignItems: "flex-end", gap: 2 }}>
          <Txt size={12} color={colors.muted}>{wide && tomorrow ? t.tomorrow : t.humidity}</Txt>
          <Txt size={15} weight="semibold">
            {wide && tomorrow ? `${Math.round(tomorrow.maxC)}°C` : `${weather.current.humidityPercent}%`}
          </Txt>
        </View>
      </View>
      {weather.heatwave.tomorrow || weather.heatwave.today ? (
        <View style={{ flexDirection: "row", gap: 10, alignItems: "center", backgroundColor: colors.warnBg, borderRadius: 12, paddingVertical: 10, paddingHorizontal: 12 }}>
          <Icon name="alert" size={18} color={colors.warnIcon} />
          <Txt size={13} weight="medium" color={colors.warnText} style={{ flex: 1 }}>
            {weather.heatwave.tomorrow ? t.heatwaveTomorrow : t.heatwaveToday}
          </Txt>
        </View>
      ) : null}
    </Card>
  );
}

function StatusPill({ moisture, short }: { moisture: number; short?: boolean }) {
  const { t } = useLang();
  const level = moistureLevel(moisture);
  const s = {
    dry: { label: t.statusDry, fg: colors.dryText, bg: colors.sunSoft },
    optimal: { label: t.statusOptimal, fg: colors.primaryDark, bg: colors.primarySoft },
    wet: { label: short ? t.statusWet : t.statusWetLong, fg: colors.waterText, bg: colors.waterSoft },
  }[level];
  return (
    <View style={{ backgroundColor: s.bg, paddingVertical: 4, paddingHorizontal: 12, borderRadius: radius.pill, alignSelf: "center" }}>
      <Txt weight="semibold" color={s.fg}>{s.label}</Txt>
    </View>
  );
}

function StatTile({ label, value }: { label: string; value: string }) {
  return (
    <View style={{ flex: 1, minWidth: 96, backgroundColor: colors.bg, borderRadius: 12, paddingVertical: 10, paddingHorizontal: 12, gap: 2 }}>
      <Txt size={12} color={colors.muted}>{label}</Txt>
      <Txt size={15} weight="semibold">{value}</Txt>
    </View>
  );
}

function WaterButton({ watering, onPress, flex }: { watering: boolean; onPress: () => void; flex?: boolean }) {
  const { t } = useLang();
  return (
    <Pressable
      onPress={onPress}
      disabled={watering}
      accessibilityRole="button"
      accessibilityState={{ busy: watering, disabled: watering }}
      style={{ flex: flex ? 1 : undefined, height: 48, borderRadius: radius.button, backgroundColor: colors.primary, opacity: watering ? 0.75 : 1, flexDirection: "row", alignItems: "center", justifyContent: "center", gap: 8 }}
    >
      <Icon name="drop" size={18} color="#FFFFFF" />
      <Txt size={15} weight="semibold" color="#FFFFFF">{watering ? t.wateringNow : t.waterNow}</Txt>
    </Pressable>
  );
}

function AutoRow({ auto, setAuto, sub }: { auto: boolean; setAuto: (v: boolean) => void; sub: string }) {
  const { t } = useLang();
  return (
    <View style={{ flexDirection: "row", alignItems: "center", gap: 12 }}>
      <View style={{ flex: 1, gap: 2 }}>
        <Txt size={15} weight="medium">{t.autoWatering}</Txt>
        <Txt size={12} color={colors.muted}>{sub}</Txt>
      </View>
      <Toggle value={auto} onChange={setAuto} label={t.autoWatering} />
    </View>
  );
}

function Ripple({ active }: { active: boolean }) {
  const v = useRef(new Animated.Value(0)).current;
  useEffect(() => {
    if (!active) return;
    v.setValue(0);
    const loop = Animated.loop(Animated.timing(v, { toValue: 1, duration: 1200, easing: Easing.out(Easing.ease), useNativeDriver: native }));
    loop.start();
    return () => loop.stop();
  }, [active, v]);
  if (!active) return null;
  return (
    <Animated.View
      pointerEvents="none"
      style={{
        position: "absolute",
        width: 64,
        height: 64,
        borderRadius: 32,
        backgroundColor: colors.primaryLine,
        opacity: v.interpolate({ inputRange: [0, 1], outputRange: [0.55, 0] }),
        transform: [{ scale: v.interpolate({ inputRange: [0, 1], outputRange: [1, 1.9] }) }],
      }}
    />
  );
}

function FloatingPump({ watering, onPress, inset }: { watering: boolean; onPress: () => void; inset: number }) {
  return (
    <View style={{ position: "absolute", right: inset, bottom: inset, width: 64, height: 64, alignItems: "center", justifyContent: "center" }}>
      <Ripple active={watering} />
      <Pressable
        onPress={onPress}
        accessibilityRole="button"
        accessibilityLabel="Pump water"
        style={{ width: 64, height: 64, borderRadius: 32, backgroundColor: colors.primary, alignItems: "center", justifyContent: "center", elevation: 6, shadowColor: colors.primary, shadowOpacity: 0.45, shadowRadius: 10, shadowOffset: { width: 0, height: 6 } }}
      >
        <Icon name="drop" size={28} color="#FFFFFF" fill={colors.waterSoft} strokeWidth={1.6} />
      </Pressable>
    </View>
  );
}

interface ToastData {
  text: string;
  note?: string | null;
  error?: boolean;
}

function Toast({ toast, wide }: { toast: ToastData; wide: boolean }) {
  return (
    <View
      accessibilityLiveRegion="polite"
      style={[
        { position: "absolute", top: wide ? 24 : 20, backgroundColor: toast.error ? colors.danger : colors.primaryDark, borderRadius: 14, padding: 14, flexDirection: "row", gap: 10, alignItems: "center", elevation: 6 },
        wide ? { right: 32 } : { left: 20, right: 20 },
      ]}
    >
      <Icon name={toast.error ? "alert" : "check"} size={18} color={toast.error ? "#FFFFFF" : colors.primaryLine} strokeWidth={2.5} />
      <View style={{ flex: 1, gap: 2 }}>
        <Txt weight="medium" color="#FFFFFF">{toast.text}</Txt>
        {toast.note ? <Txt size={12} color={toast.error ? "#FFFFFF" : colors.primaryLine}>{toast.note}</Txt> : null}
      </View>
    </View>
  );
}

// Shown instead of live data while no sensor is paired
function PairBanner() {
  const { t } = useLang();
  return (
    <Pressable
      onPress={() => router.push("/pair-device")}
      accessibilityRole="button"
      style={{ alignSelf: "stretch", flexDirection: "row", alignItems: "center", gap: 10, backgroundColor: colors.primaryTint, borderWidth: 1, borderColor: colors.primaryLine, borderRadius: 12, paddingVertical: 10, paddingHorizontal: 12 }}
    >
      <Icon name="chip" size={18} color={colors.primaryDark} />
      <Txt size={13} color={colors.primaryDark} style={{ flex: 1 }}>{t.demoData}</Txt>
      <Txt size={13} weight="semibold" color={colors.primaryDark}>{t.pairSensor}</Txt>
      <Icon name="chevron" size={16} color={colors.primaryDark} />
    </Pressable>
  );
}

// ---------- Man hinh ----------

export default function GardenScreen() {
  const { user, authFetch } = useAuth();
  const { t } = useLang();
  const wide = useIsWide();
  const { weather, loading: weatherLoading, error: weatherError } = useWeather();
  const { streak, recordWatering } = useStreak();
  const { device, loaded: devicesLoaded, replace: replaceDevice } = useDevices();
  const { history, reload: reloadHistory } = useDeviceHistory(device?.id ?? null);
  const [demoMoisture, setDemoMoisture] = useState(DEMO_MOISTURE);
  const [demoAuto, setDemoAuto] = useState(true);
  const [watering, setWatering] = useState(false);
  const [toast, setToast] = useState<ToastData | null>(null);
  const timers = useRef<ReturnType<typeof setTimeout>[]>([]);
  const alive = useRef(true);

  useEffect(
    () => () => {
      alive.current = false;
      timers.current.forEach(clearTimeout);
    },
    [],
  );

  // ----- Values shown on screen: real sensor data, or demo values when nothing is paired -----
  const hasReading = device?.moisture != null;
  const moisture = device ? (device.moisture ?? 0) : demoMoisture;
  const dry = device?.dryThreshold ?? 30;
  const wet = device?.wetThreshold ?? 60;
  const auto = device ? device.autoWatering : demoAuto;
  const busy = watering || !!device?.pumpOn;
  const plantName = device?.name ?? "Money Tree";
  const boardLabel = device ? `ESP32 · ${device.id}` : "ESP32 · Desk-01";
  const seen = secondsSince(device?.lastSeenAtUtc ?? null);
  const statusLine = !device
    ? t.demoData
    : !hasReading
      ? t.waitingFirstReading
      : device.online
        ? t.updatedAgo.replace("{time}", ago(seen, t))
        : t.offlineSince.replace("{time}", ago(seen, t));
  const autoSub = t.autoWateringSub.replace("{dry}", String(dry)).replace("{wet}", String(wet));
  const chart = buildChart(history, t);
  const activity = activityItems(history, t);
  const showPairBanner = devicesLoaded && !device;

  function showToast(next: ToastData) {
    setToast(next);
    timers.current.push(setTimeout(() => alive.current && setToast(null), 4500));
  }

  // Count the watering in the streak (Garden service), then confirm on screen
  async function finishWatering(finalMoisture: number, demo: boolean) {
    const res = await recordWatering();
    if (!alive.current) return;
    const note = !res ? t.streakFailed : res.milestoneReached ? t.streakMilestone : demo ? t.demoWatering : null;
    showToast({ text: t.wateringDone.replace("{m}", String(finalMoisture)), note });
  }

  async function pump() {
    if (busy) return;
    setToast(null);

    // No sensor paired: 3-second simulation so the screen can still be demoed
    if (!device) {
      setWatering(true);
      timers.current.push(
        setTimeout(() => {
          if (!alive.current) return;
          setWatering(false);
          setDemoMoisture(65);
          finishWatering(65, true);
        }, 3000),
      );
      return;
    }

    if (!device.online) {
      showToast({ text: t.wateringOffline, error: true });
      return;
    }

    // Real pump: send the command, wait until the device reports the pump has stopped
    setWatering(true);
    try {
      const final = await waterAndWait(authFetch, device, PUMP_SECONDS, () => alive.current);
      if (!alive.current) return;
      replaceDevice(final);
      reloadHistory();
      await finishWatering(final.moisture ?? 0, false);
    } catch (e) {
      if (!alive.current) return;
      const message = (e as Error).message;
      showToast({ text: message === "timeout" ? t.wateringTimeout : message, error: true });
    } finally {
      if (alive.current) setWatering(false);
    }
  }

  // Auto-watering switch: update the screen at once, then save; undo if saving fails
  async function setAuto(value: boolean) {
    if (!device) return setDemoAuto(value);
    const previous: Device = device;
    replaceDevice({ ...device, autoWatering: value });
    try {
      replaceDevice(await sensorApi(authFetch).update(device.id, { autoWatering: value }));
    } catch (e) {
      replaceDevice(previous);
      showToast({ text: (e as Error).message, error: true });
    }
  }

  const lastWateredLabel = device?.lastWateredAtUtc ? clock(device.lastWateredAtUtc) : device ? t.never : "14:02";
  const chartView = chart ? (
    <MoistureChart
      values={chart.values}
      pumpMarks={chart.marks}
      low={dry}
      high={wet}
      title={t.last24h}
      optimalLabel={t.optimalRange.replace("{dry}", String(dry)).replace("{wet}", String(wet))}
      pumpLabel={t.pumpRan}
      axisLabels={chart.axis}
    />
  ) : (
    <View style={{ gap: 6 }}>
      <Txt size={15} weight="semibold" accessibilityRole="header">{t.last24h}</Txt>
      <Txt size={13} color={colors.muted}>{t.notEnoughData}</Txt>
    </View>
  );

  if (wide) {
    return (
      <Screen maxWidth={null}>
        <ScrollView contentContainerStyle={{ paddingHorizontal: 32, paddingTop: 16, paddingBottom: 48, gap: 20, maxWidth: 1344, width: "100%" }}>
          {/* Dau trang */}
          <View style={{ flexDirection: "row", flexWrap: "wrap", alignItems: "center", gap: 16 }}>
            <View style={{ flexGrow: 1, flexBasis: 260, gap: 4 }}>
              <Txt color={colors.muted}>{todayLabel()}</Txt>
              <Txt size={28} weight="bold" accessibilityRole="header">Hello, {user?.displayName}!</Txt>
            </View>
            <StreakPill streak={streak} />
            <Pressable
              onPress={() => router.push("/pair-device")}
              accessibilityRole="button"
              accessibilityLabel={device ? t.manageDevice : t.pairDevice}
              style={{ height: 44, borderRadius: 12, borderWidth: 1, borderColor: colors.border, backgroundColor: "#FFFFFF", paddingHorizontal: 14, flexDirection: "row", alignItems: "center", gap: 8 }}
            >
              <Txt>{device ? `${plantName} · ${device.id}` : t.pairDevice}</Txt>
              <View style={{ transform: [{ rotate: "90deg" }] }}>
                <Icon name="chevron" size={16} color={colors.muted} />
              </View>
            </Pressable>
            <BellButton />
            <Avatar name={user?.displayName} />
          </View>

          <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 20, alignItems: "flex-start" }}>
            {/* Cot trai: do am + bieu do */}
            <Card style={{ flexGrow: 2, flexBasis: 560, minWidth: 0, borderRadius: radius.hero, padding: 24, gap: 20 }}>
              <View style={{ flexDirection: "row", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 8 }}>
                <Txt size={18} weight="semibold" accessibilityRole="header">{t.liveMoisture}</Txt>
                <Txt size={13} color={device && !device.online ? colors.danger : colors.muted}>{device ? `${boardLabel} · ${statusLine}` : statusLine}</Txt>
              </View>
              {showPairBanner ? <PairBanner /> : null}
              <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 28, alignItems: "center" }}>
                <MoistureRing moisture={moisture} />
                <View style={{ flexGrow: 1, flexBasis: 240, gap: 14 }}>
                  <View style={{ flexDirection: "row", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
                    <Txt size={56} weight="bold" style={{ lineHeight: 60 }}>{!device || hasReading ? `${moisture}%` : "--%"}</Txt>
                    {!device || hasReading ? <StatusPill moisture={moisture} short /> : null}
                  </View>
                  <View style={{ flexDirection: "row", gap: 10, flexWrap: "wrap" }}>
                    <StatTile label={t.lastWatered} value={lastWateredLabel} />
                    <StatTile label={t.targetRange} value={`${dry}–${wet}%`} />
                    <StatTile label={t.autoStop} value={`≥ ${wet}%`} />
                  </View>
                </View>
              </View>
              {chartView}
            </Card>

            {/* Cot phai */}
            <View style={{ flexGrow: 1, flexBasis: 320, minWidth: 0, gap: 20 }}>
              <WeatherCard wide weather={weather} loading={weatherLoading} error={weatherError} />

              <Card style={{ borderRadius: radius.hero, padding: 20, gap: 14 }}>
                <Txt size={16} weight="semibold" accessibilityRole="header">{t.quickControls}</Txt>
                <WaterButton watering={busy} onPress={pump} />
                <AutoRow auto={auto} setAuto={setAuto} sub={autoSub} />
                <Pressable
                  accessibilityRole="button"
                  style={{ height: 44, borderRadius: 12, borderWidth: 1, borderColor: colors.primaryLine, backgroundColor: colors.primaryTint, alignItems: "center", justifyContent: "center" }}
                >
                  <Txt weight="semibold" color={colors.primaryDark}>Sync history to Google Sheets</Txt>
                </Pressable>
              </Card>

              <Card style={{ borderRadius: radius.hero, padding: 20 }}>
                <Txt size={16} weight="semibold" accessibilityRole="header">{t.recentActivity}</Txt>
                {activity.length === 0 ? <Txt size={13} color={colors.muted}>{t.noActivity}</Txt> : null}
                {activity.map((a) => (
                  <View key={a.key} style={{ flexDirection: "row", gap: 12, alignItems: "flex-start" }}>
                    <View style={{ width: 10, height: 10, borderRadius: 5, marginTop: 5, backgroundColor: a.dot }} />
                    <View style={{ gap: 2, flex: 1 }}>
                      <Txt weight="medium">{a.text}</Txt>
                      <Txt size={12} color={colors.muted}>{a.time}</Txt>
                    </View>
                  </View>
                ))}
              </Card>
            </View>
          </View>
        </ScrollView>

        {toast && <Toast toast={toast} wide />}
        <FloatingPump watering={busy} onPress={pump} inset={32} />
      </Screen>
    );
  }

  // ----- Bo cuc dien thoai -----
  return (
    <Screen>
      <ScrollView contentContainerStyle={{ padding: 20, paddingBottom: 110, gap: 16 }}>
        <View style={{ flexDirection: "row", justifyContent: "space-between", alignItems: "center" }}>
          <View style={{ gap: 2, flex: 1 }}>
            <Txt color={colors.muted}>{greeting()}</Txt>
            <Txt size={24} weight="bold" numberOfLines={1}>Hello, {user?.displayName}!</Txt>
          </View>
          <View style={{ flexDirection: "row", gap: 10, alignItems: "center" }}>
            <BellButton />
            <Avatar name={user?.displayName} />
          </View>
        </View>

        <StreakPill streak={streak} />
        <WeatherCard weather={weather} loading={weatherLoading} error={weatherError} />

        <Card style={{ borderRadius: radius.hero, padding: 20, alignItems: "center" }}>
          <View style={{ alignSelf: "stretch", flexDirection: "row", justifyContent: "space-between", alignItems: "center" }}>
            <Txt size={16} weight="semibold" numberOfLines={1} style={{ flexShrink: 1 }}>{plantName}</Txt>
            <Txt size={12} color={colors.muted}>{boardLabel}</Txt>
          </View>
          {showPairBanner ? <PairBanner /> : null}
          <MoistureRing moisture={moisture} />
          <View style={{ alignItems: "center", gap: 4 }}>
            <Txt size={40} weight="bold" style={{ lineHeight: 44 }}>{!device || hasReading ? `${moisture}%` : "--%"}</Txt>
            {!device || hasReading ? <StatusPill moisture={moisture} /> : null}
            <Txt size={12} color={device && !device.online ? colors.danger : colors.muted} style={{ marginTop: 4, textAlign: "center" }}>
              {t.soilMoisture} · {statusLine}
            </Txt>
          </View>
        </Card>

        {device ? <Card style={{ gap: 14 }}>{chartView}</Card> : null}

        <Card style={{ gap: 14 }}>
          <Txt size={16} weight="semibold">{t.quickControls}</Txt>
          <View style={{ flexDirection: "row", gap: 10 }}>
            <WaterButton watering={busy} onPress={pump} flex />
            <Pressable
              accessibilityRole="button"
              style={{ flex: 1, height: 48, borderRadius: radius.button, borderWidth: 1, borderColor: colors.primaryLine, backgroundColor: colors.primaryTint, flexDirection: "row", alignItems: "center", justifyContent: "center", gap: 8 }}
            >
              <Icon name="sheet" size={18} color={colors.primaryDark} />
              <Txt size={15} weight="semibold" color={colors.primaryDark}>Sync to Sheets</Txt>
            </Pressable>
          </View>
          <AutoRow auto={auto} setAuto={setAuto} sub={autoSub} />
        </Card>
      </ScrollView>

      {toast && <Toast toast={toast} wide={false} />}
      <FloatingPump watering={busy} onPress={pump} inset={20} />
    </Screen>
  );
}
