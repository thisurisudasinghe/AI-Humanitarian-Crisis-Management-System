"use client";

import Link from "next/link";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useCallback, useEffect, useState } from "react";
import { Skeleton, Toast } from "@/components/ui";
import { Icon } from "@/components/safeway/Icon";
import { useT } from "@/lib/i18n";
import { EmptyPanel } from "@/components/safeway/IntegrationNotice";
import { formatDate } from "@/lib/admin";

import {
  getInbox,
  markAllRead,
  markRead,
  type NotificationDto,
  type NotificationInboxDto
} from "@/lib/notifications";

type Notice = { message: string; tone: "error" | "success" } | null;
type Filter = "all" | "unread";

const filters: [Filter, string][] = [
  ["all", "notifications.allMessages"],
  ["unread", "notifications.unread"]
];

export default function NotificationsPage() {
  const t = useT();
  const [inbox, setInbox] = useState<NotificationInboxDto | null>(null);
  const [filter, setFilter] = useState<Filter>("all");
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [markingAll, setMarkingAll] = useState(false);
  const [notice, setNotice] = useState<Notice>(null);

  const load = useCallback(async () => {
    setLoading(true);

    try {
      const result = await getInbox({ limit: 100 });
      setInbox(result);
      window.dispatchEvent(new Event("cms:notifications-changed"));
    } catch (error) {
      setNotice({ message: localizedErrorMessage(error, t), tone: "error" });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const items = inbox?.items ?? [];
  const unreadCount = inbox?.unreadCount ?? 0;
  const visibleItems =
    filter === "unread" ? items.filter((notification) => !notification.isRead) : items;

  async function handleMarkRead(notification: NotificationDto) {
    setBusyId(notification.id);

    try {
      await markRead(notification.id);
      await load();
    } catch (error) {
      setNotice({ message: localizedErrorMessage(error, t), tone: "error" });
    } finally {
      setBusyId(null);
    }
  }

  async function handleMarkAllRead() {
    setMarkingAll(true);

    try {
      const result = await markAllRead();
      setNotice({
        message:
          result.updated === 0
            ? t("notifications.alreadyRead")
            : t("notifications.markedRead", { count: String(result.updated) }),
        tone: "success"
      });
      await load();
    } catch (error) {
      setNotice({ message: localizedErrorMessage(error, t), tone: "error" });
    } finally {
      setMarkingAll(false);
    }
  }

  return (
    <>
      <div className="reports-page-head">
        <div>
          <p>{t("notifications.eyebrow")}</p>
          <h1>{t("notifications.title")}</h1>
          <span>{t("notifications.intro")}</span>
        </div>
        <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
          <button
            className="btn btn-outline"
            type="button"
            onClick={handleMarkAllRead}
            disabled={markingAll || unreadCount === 0}
          >
            <Icon name="check" /> {markingAll ? t("notifications.marking") : t("notifications.markAllRead")}
          </button>
          <Link className="btn btn-primary" href="/report">
            <Icon name="alert" /> {t("notifications.reportCrisis")}
          </Link>
        </div>
      </div>

      <Toast
        message={notice?.message ?? null}
        tone={notice?.tone ?? "error"}
        onDismiss={() => setNotice(null)}
      />

      <div className="report-summary">
        {filters.map(([value, label]) => (
          <button
            className={filter === value ? "active" : ""}
            type="button"
            onClick={() => setFilter(value)}
            key={value}
          >
            <i>
              <Icon name={value === "unread" ? "bell" : "file"} />
            </i>
            <span>
              <b>{value === "unread" ? unreadCount : inbox?.totalCount ?? "—"}</b>
              <small>{t(label)}</small>
            </span>
          </button>
        ))}
      </div>

      <section className="reports-detail-list">
        {loading ? (
          <div style={{ padding: 24 }}>
            <Skeleton rows={4} />
          </div>
        ) : visibleItems.length === 0 ? (
          <EmptyPanel title={filter === "unread" ? t("notifications.allCaughtUp") : t("notifications.noNotifications")}>
            {filter === "unread"
              ? t("notifications.allReadCopy")
              : t("notifications.noNotificationsCopy")}
          </EmptyPanel>
        ) : (
          visibleItems.map((notification) => (
            <article className="notice" key={notification.id}>
              <i style={notification.isRead ? { background: "#b6c6d2" } : undefined} />
              <span style={{ flex: 1 }}>
                <b>{notification.subject ?? notification.templateKey}</b>
                <p>{notification.body ?? "No further details were included."}</p>
                <small style={{ color: "var(--muted)", fontSize: 9, marginTop: 4 }}>
                  {formatDate(notification.createdAt)}
                  {notification.isRead ? " · Read" : " · Unread"}
                </small>
              </span>
              {!notification.isRead ? (
                <button
                  className="btn btn-outline"
                  type="button"
                  onClick={() => handleMarkRead(notification)}
                  disabled={busyId === notification.id}
                  style={{ alignSelf: "center", whiteSpace: "nowrap" }}
                >
                  {busyId === notification.id ? "Saving…" : "Mark read"}
                </button>
              ) : null}
            </article>
          ))
        )}
      </section>
    </>
  );
}
