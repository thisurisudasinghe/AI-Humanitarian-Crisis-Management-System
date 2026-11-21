"use client";

import Link from "next/link";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import {
  Badge,
  Button,
  Input,
  ServerTable,
  Skeleton,
  Toast,
  type Column
} from "@/components/ui";
import { formatDate } from "@/lib/admin";
import { errorMessage } from "@/lib/api";
import { getDecisionReviews, type ReviewEntryDto } from "@/lib/decisions";
import type { CmsPagedResult } from "@/lib/admin";
import { StaffRoles } from "@/lib/roles";

type Notice = { message: string; tone: "error" | "success" } | null;

const guidPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const pageSize = 20;

function isGuid(value: string): boolean {
  return guidPattern.test(value.trim());
}

function confidencePercent(value: number): number {
  const scaled = value <= 1 ? value * 100 : value;
  return Math.round(Math.min(100, Math.max(0, scaled)));
}

function JsonSnippet({ value, label }: { value?: string | null; label: string }) {
  if (!value) {
    return <span>—</span>;
  }

  return (
    <details>
      <summary>{label}</summary>
      <pre
        style={{
          whiteSpace: "pre-wrap",
          overflowWrap: "anywhere",
          fontSize: 11,
          padding: 10,
          background: "#f5f8fa",
          border: "1px solid var(--line)"
        }}
      >
        {value}
      </pre>
    </details>
  );
}

export default function DecisionsReviewPage() {
  const [crisisInput, setCrisisInput] = useState("");
  const [crisisId, setCrisisId] = useState("");
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<CmsPagedResult<ReviewEntryDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [notice, setNotice] = useState<Notice>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setNotice(null);

    try {
      setResult(await getDecisionReviews({ crisisId, page, pageSize }));
    } catch (error) {
      setNotice({ message: errorMessage(error), tone: "error" });
    } finally {
      setLoading(false);
    }
  }, [crisisId, page]);

  useEffect(() => {
    void load();
  }, [load]);

  function applyFilter(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (crisisInput.trim() && !isGuid(crisisInput)) {
      setNotice({ message: "Crisis ID must be a valid GUID.", tone: "error" });
      return;
    }

    const next = crisisInput.trim();
    setPage(1);
    setNotice(null);

    if (next === crisisId) {
      void load();
      return;
    }

    setCrisisId(next);
  }

  const columns: Column<ReviewEntryDto>[] = [
    {
      key: "recommendation",
      header: "Recommendation",
      render: (entry) => (
        <div>
          <strong>{entry.type}</strong>
          <div className="metric__label">
            <code>{entry.recommendationId}</code>
          </div>
          <div className="metric__label">
            Target <code>{entry.targetId}</code>
          </div>
        </div>
      )
    },
    {
      key: "ai",
      header: "AI output",
      render: (entry) => (
        <div>
          <Badge>{confidencePercent(entry.confidence)}% confidence</Badge>
          <p className="metric__label">{entry.rationale}</p>
          <small>Source: {entry.source}</small>
          <JsonSnippet value={entry.payloadJson} label="AI payload" />
        </div>
      )
    },
    {
      key: "outcome",
      header: "Human outcome",
      render: (entry) => (
        <div className="actions">
          {entry.outcome ? (
            <Badge>{entry.outcome}</Badge>
          ) : (
            <Badge>Awaiting decision</Badge>
          )}
          <span className={`badge ${entry.isOverride ? "badge--danger" : "badge--muted"}`}>
            {entry.isOverride ? "Override" : "No override"}
          </span>
        </div>
      )
    },
    {
      key: "reason",
      header: "Reason & final payload",
      render: (entry) => (
        <div>
          <span>{entry.reason ?? "—"}</span>
          <JsonSnippet value={entry.finalPayloadJson} label="Final payload" />
        </div>
      )
    },
    {
      key: "decider",
      header: "Decider / time",
      render: (entry) => (
        <div>
          {entry.decidedBy ? <code>{entry.decidedBy}</code> : <span>—</span>}
          <div className="metric__label">{formatDate(entry.decidedAt)}</div>
        </div>
      )
    }
  ];

  return (
    <AuthGuard allowedRoles={StaffRoles}>
      <div className="ops-title">
        <div>
          <p>POST-INCIDENT REVIEW</p>
          <h1>AI output vs human decisions</h1>
          <span>
            Append-only review of every recommendation and the officer outcome recorded against it.
          </span>
        </div>
        <div>
          <Link className="btn btn-outline" href="/operations/ai">
            <Icon name="brain" /> Recommendation queue
          </Link>
        </div>
      </div>

      <Toast
        message={notice?.message ?? null}
        tone={notice?.tone ?? "error"}
        onDismiss={() => setNotice(null)}
      />

      <section className="ops-card">
        <div className="card-heading">
          <div>
            <h3>Decision history</h3>
            <p>From the decision review feed</p>
          </div>
          <Button variant="secondary" onClick={() => void load()} disabled={loading}>
            Refresh
          </Button>
        </div>

        <form className="filters" onSubmit={applyFilter} style={{ padding: 20 }}>
          <Input
            label="Crisis ID (optional)"
            placeholder="Guid"
            value={crisisInput}
            onChange={(event) => setCrisisInput(event.target.value)}
          />
          <Button type="submit" disabled={loading}>
            Apply filter
          </Button>
        </form>

        <div style={{ padding: "0 20px 20px" }}>
          {loading ? (
            <Skeleton rows={6} />
          ) : (
            <ServerTable
              columns={columns}
              rows={result?.items ?? []}
              page={result?.page ?? page}
              pageSize={result?.pageSize ?? pageSize}
              totalCount={result?.total ?? 0}
              onPageChange={setPage}
              emptyMessage="No decisions recorded for this filter."
            />
          )}
        </div>
      </section>
    </AuthGuard>
  );
}
