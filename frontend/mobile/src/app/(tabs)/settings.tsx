// Man Settings & Profile: thong tin user that, doi ngon ngu, thiet bi IoT, dang xuat
import { useState } from "react";
import { Pressable, ScrollView, View } from "react-native";
import { router, type Href } from "expo-router";
import { Screen } from "../../components/Screen";
import { Card } from "../../components/Card";
import { Icon, type IconName } from "../../components/Icon";
import { Txt } from "../../components/Txt";
import { useAuth } from "../../auth/AuthContext";
import { timeAgo, useLang, type Lang, type Strings } from "../../i18n/LanguageContext";
import { useDevices } from "../../hooks/useSensorData";
import { colors, radius } from "../../theme";

function InfoTile({ label, value }: { label: string; value: string }) {
  return (
    <View style={{ flexBasis: "47%", flexGrow: 1, backgroundColor: colors.bg, borderRadius: 12, paddingVertical: 10, paddingHorizontal: 12, gap: 2 }}>
      <Txt size={11} color={colors.muted}>{label}</Txt>
      <Txt weight="semibold">{value}</Txt>
    </View>
  );
}

function Row({ icon, label, top, href }: { icon: IconName; label: string; top?: boolean; href?: Href }) {
  return (
    <Pressable
      onPress={href ? () => router.push(href) : undefined}
      accessibilityRole="button"
      style={{ height: 52, paddingHorizontal: 16, flexDirection: "row", alignItems: "center", gap: 12, borderTopWidth: top ? 0 : 1, borderTopColor: "#EEF2EE" }}
    >
      <Icon name={icon} size={20} />
      <Txt size={15} style={{ flex: 1 }}>{label}</Txt>
      <Icon name="chevron" size={18} color={colors.muted} />
    </Pressable>
  );
}

// Wi-Fi strength from RSSI (dBm): above -60 strong, above -75 fair, otherwise weak
function wifiLabel(rssi: number | null, t: Strings) {
  if (rssi == null) return "—";
  const level = rssi > -60 ? t.strong : rssi > -75 ? t.fair : t.weak;
  return `${level} · ${rssi} dBm`;
}

export default function SettingsScreen() {
  const { user, logout } = useAuth();
  const { lang, setLang, t } = useLang();
  const { device, loaded: deviceLoaded } = useDevices(10_000);
  const [leaving, setLeaving] = useState(false);

  async function handleLogout() {
    setLeaving(true);
    try {
      await logout(); // Xoa token tren may; layout goc tu chuyen ve man Login (redirectTo="/login")
    } finally {
      setLeaving(false);
    }
  }

  return (
    <Screen>
      <ScrollView contentContainerStyle={{ padding: 20, paddingBottom: 32, gap: 16 }}>
        <Txt size={24} weight="bold">{t.settingsTitle}</Txt>

        {/* Ho so */}
        <Card style={{ flexDirection: "row", alignItems: "center", gap: 14 }}>
          <View style={{ width: 56, height: 56, borderRadius: 28, backgroundColor: colors.primary, alignItems: "center", justifyContent: "center" }}>
            <Txt size={22} weight="semibold" color="#FFFFFF">{(user?.displayName ?? "?").charAt(0).toUpperCase()}</Txt>
          </View>
          <View style={{ flex: 1, gap: 2 }}>
            <Txt size={17} weight="semibold">{user?.displayName}</Txt>
            <Txt size={13} color={colors.muted} numberOfLines={1}>{user?.email}</Txt>
            {user && user.role !== "User" && (
              <Txt size={12} weight="semibold" color={colors.primaryDark}>{user.role === "Admin" ? t.roleAdmin : t.roleExpert}</Txt>
            )}
          </View>
          <View style={{ flexDirection: "row", alignItems: "center", gap: 4, backgroundColor: colors.sunSoft, paddingVertical: 4, paddingHorizontal: 10, borderRadius: radius.pill }}>
            <Icon name="flame" size={14} />
            <Txt size={12} weight="semibold" color={colors.warnText}>7</Txt>
          </View>
        </Card>

        {/* Ngon ngu */}
        <Card style={{ flexDirection: "row", alignItems: "center", gap: 12 }}>
          <Icon name="globe" color={colors.primary} />
          <Txt size={15} weight="medium" style={{ flex: 1 }}>{t.language}</Txt>
          <View accessibilityRole="radiogroup" style={{ flexDirection: "row", backgroundColor: "#EEF5EE", borderRadius: 10, padding: 3 }}>
            {(["en", "vi"] as Lang[]).map((l) => {
              const on = lang === l;
              return (
                <Pressable
                  key={l}
                  accessibilityRole="radio"
                  accessibilityState={{ checked: on }}
                  onPress={() => setLang(l)}
                  style={{ width: 52, height: 34, borderRadius: 8, alignItems: "center", justifyContent: "center", backgroundColor: on ? colors.primary : "transparent" }}
                >
                  <Txt size={13} weight="bold" color={on ? "#FFFFFF" : colors.muted}>{l.toUpperCase()}</Txt>
                </Pressable>
              );
            })}
          </View>
        </Card>

        {/* IoT device: live data from the Sensor service. Tap to pair / manage the device */}
        <Pressable onPress={() => router.push("/pair-device")} accessibilityRole="button" accessibilityLabel={device ? t.manageDevice : t.pairDevice}>
          <Card style={{ gap: 14 }}>
            <View style={{ flexDirection: "row", alignItems: "center", gap: 10 }}>
              <Icon name="chip" color={colors.primary} />
              <Txt size={15} weight="semibold" style={{ flex: 1 }}>{t.device}</Txt>
              {device ? (
                <View style={{ flexDirection: "row", alignItems: "center", gap: 6, backgroundColor: device.online ? colors.primarySoft : "#FDECEC", paddingVertical: 4, paddingHorizontal: 10, borderRadius: radius.pill }}>
                  <View style={{ width: 8, height: 8, borderRadius: 4, backgroundColor: device.online ? colors.primary : colors.danger }} />
                  <Txt size={12} weight="semibold" color={device.online ? colors.primaryDark : colors.danger}>{device.online ? t.online : t.deviceOffline}</Txt>
                </View>
              ) : null}
              <Icon name="chevron" size={18} color={colors.muted} />
            </View>
            {device ? (
              <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 10 }}>
                <InfoTile label={t.board} value={`ESP32 · ${device.id}`} />
                <InfoTile label={t.ip} value={device.ipAddress ?? "—"} />
                <InfoTile label="Wi-Fi" value={wifiLabel(device.wifiRssi, t)} />
                <InfoTile label={t.lastSeen} value={device.lastSeenAtUtc ? (device.online ? t.justNow : timeAgo(device.lastSeenAtUtc, t, lang)) : "—"} />
              </View>
            ) : deviceLoaded ? (
              <View style={{ flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: 10 }}>
                <Txt size={13} color={colors.muted} style={{ flex: 1 }}>{t.noDevice}</Txt>
                <Txt size={13} weight="semibold" color={colors.primaryDark}>{t.pairDevice}</Txt>
              </View>
            ) : null}
          </Card>
        </Pressable>

        {/* Google Sheets */}
        <Card>
          <View style={{ flexDirection: "row", alignItems: "center", gap: 10 }}>
            <Icon name="sheet" color={colors.primary} />
            <View style={{ flex: 1, gap: 2 }}>
              <Txt size={15} weight="semibold">Google Sheets</Txt>
              <Txt size={12} color={colors.muted}>{t.sheetsSub}</Txt>
            </View>
          </View>
          <View style={{ flexDirection: "row", gap: 10 }}>
            <Pressable accessibilityRole="button" style={{ flex: 1, height: 44, borderRadius: 12, backgroundColor: colors.primary, alignItems: "center", justifyContent: "center" }}>
              <Txt weight="semibold" color="#FFFFFF">{t.openSheet}</Txt>
            </Pressable>
            <Pressable accessibilityRole="button" style={{ flex: 1, height: 44, borderRadius: 12, borderWidth: 1, borderColor: colors.primaryLine, backgroundColor: colors.primaryTint, alignItems: "center", justifyContent: "center" }}>
              <Txt weight="semibold" color={colors.primaryDark}>{t.exportData}</Txt>
            </Pressable>
          </View>
        </Card>

        {/* Tai khoan */}
        <Card style={{ padding: 0, gap: 0, overflow: "hidden" }}>
          <Txt size={13} weight="semibold" color={colors.muted} style={{ paddingHorizontal: 16, paddingTop: 14, paddingBottom: 6 }}>{t.account}</Txt>
          <Row icon="lock" label={t.password_change} top href="/change-password" />
          <Row icon="bell" label={t.notifications} />
        </Card>

        {/* Dang xuat */}
        <Pressable
          onPress={handleLogout}
          disabled={leaving}
          accessibilityRole="button"
          style={{ height: 52, borderRadius: 16, borderWidth: 1, borderColor: colors.dangerLine, backgroundColor: "#FFFFFF", flexDirection: "row", alignItems: "center", justifyContent: "center", gap: 8, opacity: leaving ? 0.6 : 1 }}
        >
          <Icon name="logout" size={20} color={colors.danger} />
          <Txt size={15} weight="semibold" color={colors.danger}>{t.logout}</Txt>
        </Pressable>
      </ScrollView>
    </Screen>
  );
}
