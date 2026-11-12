// Man Thong bao (bam chuong o man Garden). Du lieu that tu Notification service (services/notification).
import { useCallback, useState } from "react";
import { ActivityIndicator, Pressable, View } from "react-native";
import { useFocusEffect } from "expo-router";
import { SubScreen } from "../components/SubScreen";
import { Card } from "../components/Card";
import { Icon, type IconName } from "../components/Icon";
import { Txt } from "../components/Txt";
import { useAuth } from "../auth/AuthContext";
import { notificationsApi, type AppNotification } from "../api/notifications";
import { notificationText } from "../i18n/notificationText";
import { timeAgo, useLang } from "../i18n/LanguageContext";
import { colors } from "../theme";

const ICONS: Record<string, { icon: IconName; color: string; bg: string }> = {
  heatwave: { icon: "alert", color: colors.warnIcon, bg: colors.warnBg },
  streak: { icon: "flame", color: colors.sun, bg: colors.sunSoft },
  pump: { icon: "drop", color: colors.water, bg: colors.waterSoft },
  soil_dry: { icon: "drop", color: colors.warnIcon, bg: colors.sunSoft },
  water_tank: { icon: "alert", color: colors.danger, bg: colors.warnBg },
};
const FALLBACK = { icon: "bell" as IconName, color: colors.primary, bg: colors.primarySoft };

export default function NotificationsScreen() {
  const { authFetch } = useAuth();
  const { t, lang } = useLang();
  const [items, setItems] = useState<AppNotification[]>([]);
  const [unread, setUnread] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const res = await notificationsApi(authFetch).list(50);
      setItems(res.items);
      setUnread(res.unreadCount);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }, [authFetch]);

  useFocusEffect(
    useCallback(() => {
      load();
    }, [load]),
  );

  // Cap nhat giao dien ngay, loi thi tai lai de lay trang thai that tu server
  async function open(n: AppNotification) {
    if (n.isRead) return;
    setItems((l) => l.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)));
    setUnread((u) => Math.max(0, u - 1));
    try {
      await notificationsApi(authFetch).markRead(n.id);
    } catch (e) {
      setError((e as Error).message);
      load();
    }
  }

  async function readAll() {
    setItems((l) => l.map((x) => ({ ...x, isRead: true })));
    setUnread(0);
    try {
      await notificationsApi(authFetch).markAllRead();
    } catch (e) {
      setError((e as Error).message);
      load();
    }
  }

  async function remove(n: AppNotification) {
    setItems((l) => l.filter((x) => x.id !== n.id));
    if (!n.isRead) setUnread((u) => Math.max(0, u - 1));
    try {
      await notificationsApi(authFetch).remove(n.id);
    } catch (e) {
      setError((e as Error).message);
      load();
    }
  }

  const right =
    unread > 0 ? (
      <Pressable onPress={readAll} accessibilityRole="button" style={{ paddingHorizontal: 12, height: 44, justifyContent: "center" }}>
        <Txt size={13} weight="semibold" color={colors.primary}>{t.markAllRead}</Txt>
      </Pressable>
    ) : null;

  return (
    <SubScreen title={t.notificationsTitle} backLabel={t.back} right={right}>
      {loading ? <ActivityIndicator color={colors.primary} /> : null}

      {error ? (
        <Card style={{ gap: 8 }}>
          <Txt color={colors.danger}>{error}</Txt>
          <Pressable onPress={load} accessibilityRole="button">
            <Txt weight="semibold" color={colors.primary}>{t.retry}</Txt>
          </Pressable>
        </Card>
      ) : null}

      {!loading && !error && items.length === 0 ? <Txt color={colors.muted}>{t.noNotifications}</Txt> : null}

      {items.map((n) => {
        const look = ICONS[n.type] ?? FALLBACK;
        const text = notificationText(n, t);
        return (
          <Pressable key={n.id} onPress={() => open(n)} accessibilityRole="button">
            <Card style={{ flexDirection: "row", gap: 12, alignItems: "flex-start", opacity: n.isRead ? 0.7 : 1 }}>
              <View style={{ width: 40, height: 40, borderRadius: 20, backgroundColor: look.bg, alignItems: "center", justifyContent: "center" }}>
                <Icon name={look.icon} size={20} color={look.color} />
              </View>
              <View style={{ flex: 1, gap: 2 }}>
                <View style={{ flexDirection: "row", alignItems: "center", gap: 6 }}>
                  <Txt weight={n.isRead ? "medium" : "bold"} style={{ flexShrink: 1 }}>{text.title}</Txt>
                  {!n.isRead ? <View style={{ width: 8, height: 8, borderRadius: 4, backgroundColor: colors.primary }} /> : null}
                </View>
                {text.body ? <Txt size={13} color={colors.muted}>{text.body}</Txt> : null}
                <Txt size={12} color={colors.muted}>{timeAgo(n.createdAt, t, lang)}</Txt>
              </View>
              <Pressable
                onPress={() => remove(n)}
                accessibilityRole="button"
                accessibilityLabel={t.deleteNotification}
                hitSlop={8}
                style={{ padding: 4 }}
              >
                <Txt size={18} color={colors.muted}>×</Txt>
              </Pressable>
            </Card>
          </Pressable>
        );
      })}
    </SubScreen>
  );
}
