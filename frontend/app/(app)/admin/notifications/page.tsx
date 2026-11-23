"use client";

import { useCallback, useEffect, useState } from "react";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import {
  Badge,
  Button,
  ServerTable,
  Skeleton,
  Toast,
  type Column
} from "@/components/ui";
import { AuthGuard } from "@/components/AuthGuard";
import { AdminNav } from "@/components/safeway/AdminNav";
import { api } from "@/lib/api";
import { formatDate, type PagedResult } from "@/lib/admin";
import { useT } from "@/lib/i18n";
import type { NotificationDto } from "@/lib/notifications";
import { RoleNames } from "@/lib/roles";

type Notice = { message: string; tone: "error" | "success" } | null;

export default function FailedNotificationsPage() {
  const t = useT();
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<PagedResult<NotificationDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [notice, setNotice] = useState<Notice>(null);

  const load = useCallback(async () => {
    const params = new URLSearchParams({ page: String(page), pageSize: "20" });
    return api<PagedResult<NotificationDto>>(
      `/api/v1/admin/notifications/failed?${params.toString()}`
    );
  }, [page]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);

    void load()
      .then((value) => {
        if (!cancelled) {
          setResult(value);
        }
      })
      .catch((caught: unknown) => {
        if (!cancelled) {
          setNotice({ message: localizedErrorMessage(caught, t), tone: "error" });
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [load]);

  const columns: Column<NotificationDto>[] = [
    {
      key: "created",
      header: t("admin.notifications.colFailedAt"),
      render: (notification) => formatDate(notification.createdAt)
    },
    {
      key: "recipient",
      header: t("admin.notifications.colRecipient"),
      render: (notification) => <code>{notification.recipientUserId}</code>
    },
    {
      key: "channel",
      header: t("admin.notifications.colChannel"),
      render: (notification) => <Badge>{notification.channel}</Badge>
    },
    {
      key: "template",
      header: t("admin.notifications.colTemplate"),
      render: (notification) => <strong>{notification.templateKey}</strong>
    },
    {
      key: "subject",
      header: t("admin.notifications.colSubject"),
      render: (notification) => notification.subject ?? "—"
    },
    {
      key: "status",
      header: t("admin.notifications.colStatus"),
      render: (notification) => <Badge>{notification.status}</Badge>
    }
  ];

  return (
    <AuthGuard allowedRoles={[RoleNames.SystemAdmin]}>
      <div className="page">
        <header className="page__header">
          <div>
            <h1>{t("admin.notifications.title")}</h1>
            <p>{t("admin.notifications.subtitle")}</p>
          </div>
          <Button variant="secondary" onClick={() => void load().then(setResult)}>
            {t("admin.notifications.refresh")}
          </Button>
        </header>

        <AdminNav />

        <section className="card">
          {loading ? (
            <Skeleton rows={6} />
          ) : (
            <ServerTable
              columns={columns}
              rows={result?.items ?? []}
              page={result?.page ?? page}
              pageSize={result?.pageSize ?? 20}
              totalCount={result?.totalCount ?? 0}
              onPageChange={setPage}
              emptyMessage={t("admin.notifications.empty")}
            />
          )}
        </section>
      </div>

      <Toast message={notice?.message ?? null} tone={notice?.tone} onDismiss={() => setNotice(null)} />
    </AuthGuard>
  );
}
