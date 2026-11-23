"use client";

import { useCallback, useEffect, useState } from "react";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import {
  Badge,
  Button,
  Input,
  ServerTable,
  Skeleton,
  Toast,
  type Column
} from "@/components/ui";
import { AuthGuard } from "@/components/AuthGuard";
import { AdminNav } from "@/components/safeway/AdminNav";
import { api, apiJson } from "@/lib/api";
import {
  formatDate,
  type OutboxMessageDto,
  type PagedResult
} from "@/lib/admin";
import { useT } from "@/lib/i18n";
import { RoleNames } from "@/lib/roles";
import { useDebouncedValue } from "@/lib/useDebouncedValue";

type Notice = { message: string; tone: "error" | "success" } | null;

export default function OutboxPage() {
  const t = useT();
  const [type, setType] = useState("");
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<PagedResult<OutboxMessageDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [notice, setNotice] = useState<Notice>(null);
  const [retryingId, setRetryingId] = useState<string | null>(null);

  const debouncedType = useDebouncedValue(type);

  const load = useCallback(async () => {
    const params = new URLSearchParams({
      page: String(page),
      pageSize: "20"
    });

    if (debouncedType.trim()) {
      params.set("type", debouncedType.trim());
    }

    return api<PagedResult<OutboxMessageDto>>(
      `/api/v1/admin/outbox/dead?${params.toString()}`
    );
  }, [debouncedType, page]);

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

  function resetFilters() {
    setType("");
    setPage(1);
  }

  async function retry(id: string) {
    setRetryingId(id);
    setNotice(null);

    try {
      await apiJson<{ message: string }>(
        `/api/v1/admin/dead-letters/${id}/retry`,
        "POST",
        {}
      );
      setNotice({ message: t("admin.outbox.retryQueued"), tone: "success" });
      await load()
        .then(setResult)
        .catch((caught: unknown) =>
          setNotice({ message: localizedErrorMessage(caught, t), tone: "error" })
        );
    } catch (caught) {
      setNotice({ message: localizedErrorMessage(caught, t), tone: "error" });
    } finally {
      setRetryingId(null);
    }
  }

  const columns: Column<OutboxMessageDto>[] = [
    {
      key: "when",
      header: t("admin.outbox.colFailedAt"),
      render: (message) => (
        <div>
          <div>{formatDate(message.updatedAt ?? message.createdAt)}</div>
          <div className="metric__label">
            {t("admin.outbox.createdAt", { date: formatDate(message.createdAt) })}
          </div>
        </div>
      )
    },
    {
      key: "type",
      header: t("admin.outbox.colType"),
      render: (message) => <strong>{message.type}</strong>
    },
    {
      key: "entity",
      header: t("admin.outbox.colEntity"),
      render: (message) => <code>{message.entityId}</code>
    },
    {
      key: "status",
      header: t("admin.outbox.colStatus"),
      render: (message) => <Badge>{message.status}</Badge>
    },
    {
      key: "retries",
      header: t("admin.outbox.colRetries"),
      render: (message) => (
        <div>
          <div>{message.retryCount}</div>
          <div className="metric__label">
            {message.nextRetryAt
              ? t("admin.outbox.nextRetry", { date: formatDate(message.nextRetryAt) })
              : t("admin.outbox.noRetryScheduled")}
          </div>
        </div>
      )
    },
    {
      key: "error",
      header: t("admin.outbox.colError"),
      render: (message) =>
        message.errorMessage ? (
          <code title={message.errorMessage}>{message.errorMessage}</code>
        ) : (
          "—"
        )
    },
    {
      key: "actions",
      header: t("admin.outbox.colActions"),
      render: (message) => (
        <Button
          variant="secondary"
          disabled={retryingId !== null}
          onClick={() => void retry(message.id)}
        >
          {retryingId === message.id ? t("admin.outbox.retrying") : t("admin.outbox.retry")}
        </Button>
      )
    }
  ];

  return (
    <AuthGuard allowedRoles={[RoleNames.SystemAdmin]}>
      <div className="page">
        <header className="page__header">
          <div>
            <h1>{t("admin.outbox.title")}</h1>
            <p>{t("admin.outbox.subtitle")}</p>
          </div>
          <Button variant="secondary" onClick={resetFilters}>
            {t("admin.outbox.clearFilters")}
          </Button>
        </header>

        <AdminNav />

        <section className="card">
          <div className="filters">
            <Input
              label={t("admin.outbox.fieldType")}
              placeholder={t("admin.outbox.placeholderType")}
              value={type}
              onChange={(event) => {
                setType(event.target.value);
                setPage(1);
              }}
            />
          </div>
        </section>

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
              emptyMessage={t("admin.outbox.empty")}
            />
          )}
        </section>
      </div>

      <Toast message={notice?.message ?? null} tone={notice?.tone} onDismiss={() => setNotice(null)} />
    </AuthGuard>
  );
}
