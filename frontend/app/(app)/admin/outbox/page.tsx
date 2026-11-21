"use client";

import { useCallback, useEffect, useState } from "react";
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
import { api, apiJson, errorMessage } from "@/lib/api";
import {
  formatDate,
  type OutboxMessageDto,
  type PagedResult
} from "@/lib/admin";
import { RoleNames } from "@/lib/roles";
import { useDebouncedValue } from "@/lib/useDebouncedValue";

type Notice = { message: string; tone: "error" | "success" } | null;

export default function OutboxPage() {
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
          setNotice({ message: errorMessage(caught), tone: "error" });
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
      setNotice({ message: "Message queued for retry.", tone: "success" });
      await load()
        .then(setResult)
        .catch((caught: unknown) =>
          setNotice({ message: errorMessage(caught), tone: "error" })
        );
    } catch (caught) {
      setNotice({ message: errorMessage(caught), tone: "error" });
    } finally {
      setRetryingId(null);
    }
  }

  const columns: Column<OutboxMessageDto>[] = [
    {
      key: "when",
      header: "Failed at",
      render: (message) => (
        <div>
          <div>{formatDate(message.updatedAt ?? message.createdAt)}</div>
          <div className="metric__label">
            Created {formatDate(message.createdAt)}
          </div>
        </div>
      )
    },
    {
      key: "type",
      header: "Message type",
      render: (message) => <strong>{message.type}</strong>
    },
    {
      key: "entity",
      header: "Entity",
      render: (message) => <code>{message.entityId}</code>
    },
    {
      key: "status",
      header: "Status",
      render: (message) => <Badge>{message.status}</Badge>
    },
    {
      key: "retries",
      header: "Retries",
      render: (message) => (
        <div>
          <div>{message.retryCount}</div>
          <div className="metric__label">
            {message.nextRetryAt
              ? `Next ${formatDate(message.nextRetryAt)}`
              : "No retry scheduled"}
          </div>
        </div>
      )
    },
    {
      key: "error",
      header: "Last error",
      render: (message) =>
        message.errorMessage ? (
          <code title={message.errorMessage}>{message.errorMessage}</code>
        ) : (
          "—"
        )
    },
    {
      key: "actions",
      header: "Actions",
      render: (message) => (
        <Button
          variant="secondary"
          disabled={retryingId !== null}
          onClick={() => void retry(message.id)}
        >
          {retryingId === message.id ? "Retrying…" : "Retry"}
        </Button>
      )
    }
  ];

  return (
    <AuthGuard allowedRoles={[RoleNames.SystemAdmin]}>
      <div className="page">
        <header className="page__header">
          <div>
            <h1>Outbox dead letters</h1>
            <p>
              Messages that exhausted their retries and were removed from the
              delivery queue. Retrying requeues a message for another delivery
              attempt.
            </p>
          </div>
          <Button variant="secondary" onClick={resetFilters}>
            Clear filters
          </Button>
        </header>

        <AdminNav />

        <section className="card">
          <div className="filters">
            <Input
              label="Message type"
              placeholder="e.g. DonationRecorded"
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
              emptyMessage="No dead-lettered messages. The outbox is healthy."
            />
          )}
        </section>
      </div>

      <Toast message={notice?.message ?? null} tone={notice?.tone} onDismiss={() => setNotice(null)} />
    </AuthGuard>
  );
}
