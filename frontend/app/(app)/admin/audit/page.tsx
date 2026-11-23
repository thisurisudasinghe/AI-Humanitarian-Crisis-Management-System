"use client";

import { useCallback, useEffect, useState } from "react";
import { localizedErrorMessage } from "@/lib/i18n/errors";
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
import { api } from "@/lib/api";
import {
  formatDate,
  type AuditEventDto,
  type PagedResult
} from "@/lib/admin";
import { useT } from "@/lib/i18n";
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
  const t = useT();
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
      header: t("admin.audit.colWhen"),
      render: (event) => formatDate(event.createdAt)
    },
    {
      key: "action",
      header: t("admin.audit.colAction"),
      render: (event) => <strong>{event.action}</strong>
    },
    {
      key: "entity",
      header: t("admin.audit.colEntity"),
      render: (event) => (
        <div>
          <div>{event.entityType}</div>
          <div className="metric__label">{event.entityId ?? "—"}</div>
        </div>
      )
    },
    {
      key: "actor",
      header: t("admin.audit.colActor"),
      render: (event) => event.actorUserId ?? t("admin.audit.systemActor")
    },
    {
      key: "details",
      header: t("admin.audit.colDetails"),
      render: (event) => <code>{event.details ?? "—"}</code>
    },
    {
      key: "ip",
      header: t("admin.audit.colIp"),
      render: (event) => event.ipAddress ?? "—"
    }
  ];

  return (
    <AuthGuard allowedRoles={[RoleNames.SystemAdmin]}>
      <div className="page">
        <header className="page__header">
          <div>
            <h1>{t("admin.audit.title")}</h1>
            <p>{t("admin.audit.subtitle")}</p>
          </div>
          <Button variant="secondary" onClick={resetFilters}>
            {t("admin.audit.clearFilters")}
          </Button>
        </header>

        <AdminNav />

        <section className="card">
          <div className="filters">
            <Input
              label={t("admin.audit.fieldAction")}
              placeholder={t("admin.audit.placeholderAction")}
              value={action}
              onChange={(event) => {
                setAction(event.target.value);
                setPage(1);
              }}
            />
            <Input
              label={t("admin.audit.fieldEntityType")}
              placeholder={t("admin.audit.placeholderEntityType")}
              value={entityType}
              onChange={(event) => {
                setEntityType(event.target.value);
                setPage(1);
              }}
            />
            <Input
              label={t("admin.audit.fieldEntityId")}
              placeholder={t("admin.audit.placeholderGuid")}
              value={entityId}
              onChange={(event) => {
                setEntityId(event.target.value);
                setPage(1);
              }}
            />
            <Input
              label={t("admin.audit.fieldActorUserId")}
              placeholder={t("admin.audit.placeholderGuid")}
              value={actorUserId}
              onChange={(event) => {
                setActorUserId(event.target.value);
                setPage(1);
              }}
            />
            <Input
              label={t("admin.audit.fieldFrom")}
              type="datetime-local"
              value={from}
              onChange={(event) => {
                setFrom(event.target.value);
                setPage(1);
              }}
            />
            <Input
              label={t("admin.audit.fieldTo")}
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
              emptyMessage={t("admin.audit.empty")}
            />
          )}
        </section>
      </div>

      <Toast message={notice?.message ?? null} tone={notice?.tone} onDismiss={() => setNotice(null)} />
    </AuthGuard>
  );
}
