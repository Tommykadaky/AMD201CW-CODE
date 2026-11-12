// Pair / manage the soil sensor (Settings -> IoT device, or the plant chip on the Garden screen).
// Not paired: form with Device ID + Device key (same values as firmware config.h or the simulator).
// Paired: rename the plant, see device details, unpair.
import { useEffect, useState } from "react";
import { Pressable, View } from "react-native";
import { Field, FormError, PrimaryButton } from "../components/AuthForm";
import { SubScreen } from "../components/SubScreen";
import { Card } from "../components/Card";
import { Icon } from "../components/Icon";
import { Txt } from "../components/Txt";
import { useAuth } from "../auth/AuthContext";
import { sensorApi } from "../api/sensor";
import { useDevices } from "../hooks/useSensorData";
import { useLang } from "../i18n/LanguageContext";
import { colors, radius } from "../theme";

export default function PairDeviceScreen() {
  const { authFetch } = useAuth();
  const { t } = useLang();
  const { device, loaded, replace, remove } = useDevices();

  const [deviceId, setDeviceId] = useState("");
  const [deviceKey, setDeviceKey] = useState("");
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // Fill the name field with the current name once the device is loaded
  useEffect(() => {
    if (device) setName(device.name);
  }, [device?.id]); // eslint-disable-line react-hooks/exhaustive-deps

  async function pair() {
    const id = deviceId.trim();
    if (!id) return setError(t.errDeviceId);
    if (deviceKey.trim().length < 16) return setError(t.errDeviceKey);

    setError(null);
    setBusy(true);
    try {
      replace(await sensorApi(authFetch).pair(id, deviceKey.trim(), name.trim() || undefined));
      setDeviceKey("");
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  async function rename() {
    if (!device || !name.trim()) return;
    setError(null);
    setBusy(true);
    try {
      replace(await sensorApi(authFetch).update(device.id, { name: name.trim() }));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  async function unpair() {
    if (!device) return;
    setError(null);
    setBusy(true);
    try {
      await sensorApi(authFetch).unpair(device.id);
      remove(device.id);
      setName("");
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <SubScreen title={t.device} backLabel={t.back}>
      {!loaded ? null : device ? (
        <>
          <Card style={{ padding: 20, gap: 14 }}>
            <View style={{ flexDirection: "row", alignItems: "center", gap: 10 }}>
              <Icon name="chip" color={colors.primary} />
              <Txt size={15} weight="semibold" style={{ flex: 1 }}>{device.id}</Txt>
              <View style={{ flexDirection: "row", alignItems: "center", gap: 6, backgroundColor: device.online ? colors.primarySoft : "#FDECEC", paddingVertical: 4, paddingHorizontal: 10, borderRadius: radius.pill }}>
                <View style={{ width: 8, height: 8, borderRadius: 4, backgroundColor: device.online ? colors.primary : colors.danger }} />
                <Txt size={12} weight="semibold" color={device.online ? colors.primaryDark : colors.danger}>
                  {device.online ? t.online : t.deviceOffline}
                </Txt>
              </View>
            </View>
            <Txt size={13} color={colors.muted}>
              {t.firmwareLabel}: {device.firmware ?? "—"} · {t.ip}: {device.ipAddress ?? "—"}
            </Txt>
            <Field label={t.plantName} value={name} onChangeText={setName} placeholder={t.plantNamePlaceholder} maxLength={60} onSubmitEditing={rename} />
            <FormError message={error} />
            <PrimaryButton label={busy ? t.saving : t.saveName} onPress={rename} loading={busy} />
          </Card>

          <Pressable
            onPress={unpair}
            disabled={busy}
            accessibilityRole="button"
            style={{ height: 50, borderRadius: 14, borderWidth: 1, borderColor: colors.dangerLine, backgroundColor: "#FFFFFF", alignItems: "center", justifyContent: "center", opacity: busy ? 0.6 : 1 }}
          >
            <Txt weight="semibold" color={colors.danger}>{busy ? t.unpairing : t.unpair}</Txt>
          </Pressable>
          <Txt size={12} color={colors.muted} style={{ textAlign: "center" }}>{t.unpairHint}</Txt>
        </>
      ) : (
        <>
          <Txt color={colors.muted}>{t.pairSub}</Txt>
          <Card style={{ padding: 20, gap: 16 }}>
            <Field label={t.deviceIdLabel} value={deviceId} onChangeText={setDeviceId} placeholder="TREEO_ESP32_01" autoCapitalize="characters" autoCorrect={false} />
            <Field label={t.deviceKeyLabel} value={deviceKey} onChangeText={setDeviceKey} secure hint={t.deviceKeyHint} autoCapitalize="none" autoCorrect={false} />
            <Field label={t.plantName} value={name} onChangeText={setName} placeholder={t.plantNamePlaceholder} maxLength={60} onSubmitEditing={pair} />
            <FormError message={error} />
            <PrimaryButton label={busy ? t.pairing : t.pairDevice} onPress={pair} loading={busy} />
          </Card>
        </>
      )}
    </SubScreen>
  );
}
