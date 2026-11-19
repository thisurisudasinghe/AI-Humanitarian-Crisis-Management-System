"use client";

import { useCallback, useEffect, useState } from "react";
import {
  Button,
  Input,
  ServerTable,
  Skeleton,
  Toast,
  type Column
} from "@/components/ui";
import { AuthGuard } from "@/components/AuthGuard";
import { AdminNav } from "@/components/safeway/AdminNav";
import { api, errorMessage } from "@/lib/api";
import {
  formatDate,
  type AuditEventDto,
  type PagedResult
} from "@/lib/admin";
import { RoleNames } from "@/lib/roles";
import { useDebouncedValue } from "@/lib/useDebouncedValue";

type Notice = { message: string; tone: "error" | "success" } | null;

function toUtcIso(value: string): string | undefined {
  if (!value) {
    return undefined;
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? undefined : date.toISOString();
}

export default function AuditPage() {
  const [action, setAction] = useState("");
  const [entityType, setEntityType] = useState("");
  const [entityId, setEntityId] = useState("");
  const [actorUserId, setActorUserId] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<PagedResult<AuditEventDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [notice, setNotice] = useState<Notice>(null);

  const debouncedAction = useDebouncedValue(action);
  const debouncedEntityType = useDebouncedValue(entityType);
  const debouncedEntityId = useDebouncedValue(entityId);
  const debouncedActorUserId = useDebouncedValue(actorUserId);

  const load = useCallback(async () => {
    const params = new URLSearchParams({ page: String(page), pageSize: "20" });

    const add = (key: string, value: string) => {
      if (value.trim()) {
        params.set(key, value.trim());
      }
    };

    add("action", debouncedAction);
    add("entityType", debouncedEntityType);
    add("entityId", debouncedEntityId);
    add("actorUserId", debouncedActorUserId);

    const fromIso = toUtcIso(from);
    const toIso = toUtcIso(to);

    if (fromIso) {
      params.set("from", fromIso);
    }

    if (toIso) {
      params.set("to", toIso);
    }

    return api<PagedResult<AuditEventDto>>(
      `/api/v1/audit-events?${params.toString()}`
    );
  }, [
    debouncedAction,
    debouncedActorUserId,
    debouncedEntityId,
    debouncedEntityType,
    from,
    page,
    to
  ]);

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
    setAction("");
    setEntityType("");
    setEntityId("");
    setActorUserId("");
    setFrom("");
    setTo("");
    setPage(1);
  }

  const columns: Column<AuditEventDto>[] = [
    {
      key: "createdAt",
      header: "When",
      render: (event) => formatDate(event.createdAt)
    },
    {
      key: "action",
      header: "Action",
      render: (event) => <strong>{event.action}</strong>
    },
    {
      key: "entity",
      header: "Entity",
      render: (event) => (
        <div>
          <div>{event.entityType}</div>
          <div className="metric__label">{event.entityId ?? "—"}</div>
        </div>
      )
    },
    {
      key: "actor",
      header: "Actor",
      render: (event) => event.actorUserId ?? "System"
    },
    {
      key: "details",
      header: "Details (masked)",
      render: (event) => <code>{event.details ?? "—"}</code>
    },
    {
      key: "ip",
      header: "IP address",
      render: (event) => event.ipAddress ?? "—"
    }
  ];

  return (
    <AuthGuard allowedRoles={[RoleNames.SystemAdmin]}>
      <div className="page">
        <header className="page__header">
          <div>
            <h1>Audit trail</h1>
            <p>Read-only, append-only record of security-relevant changes.</p>
          </div>
          <Button variant="secondary" onClick={resetFilters}>
            Clear filters
          </Button>
        </header>

        <AdminNav />

        <section className="card">
          <div className="filters">
            <Input
              label="Action"
              placeholder="User.LoggedIn"
              value={action}
              onChange={(event) => {
                setAction(event.target.value);
                setPage(1);
              }}
            />
            <Input
              label="Entity type"
              placeholder="identity.User"
              value={entityType}
              onChange={(event) => {
                setEntityType(event.target.value);
                setPage(1);
              }}
            />
            <Input
              label="Entity ID"
              placeholder="GUID"
              value={entityId}
              onChange={(event) => {
                setEntityId(event.target.value);
                setPage(1);
              }}
            />
            <Input
              label="Actor user ID"
              placeholder="GUID"
              value={actorUserId}
              onChange={(event) => {
                setActorUserId(event.target.value);
                setPage(1);
              }}
            />
            <Input
              label="From"
              type="datetime-local"
              value={from}
              onChange={(event) => {
                setFrom(event.target.value);
                setPage(1);
              }}
            />
            <Input
              label="To"
              type="datetime-local"
              value={to}
              onChange={(event) => {
                setTo(event.target.value);
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
              emptyMessage="No audit events match the current filters."
            />
          )}
        </section>
      </div>

      <Toast message={notice?.message ?? null} tone={notice?.tone} onDismiss={() => setNotice(null)} />
    </AuthGuard>
  );
}
