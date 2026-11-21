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
import {
  DecisionOutcomes,
  getDecisionReviews,
  type ReviewEntryDto
} from "@/lib/decisions";
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

/** Date inputs are local calendar dates; the review feed takes UTC bounds. */
function toUtcBound(value: string, endOfDay: boolean): string {
  return `${value}T${endOfDay ? "23:59:59" : "00:00:00"}Z`;
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
  const [fromInput, setFromInput] = useState("");
  const [toInput, setToInput] = useState("");
  const [outcomeInput, setOutcomeInput] = useState("");
  const [filters, setFilters] = useState({
    from: "",
    to: "",
    outcome: ""
  });
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<CmsPagedResult<ReviewEntryDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [notice, setNotice] = useState<Notice>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setNotice(null);

    try {
      setResult(
        await getDecisionReviews({
          crisisId,
          page,
          pageSize,
          from: filters.from ? toUtcBound(filters.from, false) : undefined,
          to: filters.to ? toUtcBound(filters.to, true) : undefined,
          outcome: filters.outcome || undefined
        })
      );
    } catch (error) {
      setNotice({ message: errorMessage(error), tone: "error" });
    } finally {
      setLoading(false);
    }
  }, [crisisId, filters, page]);

  useEffect(() => {
    void load();
  }, [load]);

  function applyFilter(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (crisisInput.trim() && !isGuid(crisisInput)) {
      setNotice({ message: "Crisis ID must be a valid GUID.", tone: "error" });
      return;
    }

    const nextCrisisId = crisisInput.trim();
    const nextFrom = fromInput.trim();
    const nextTo = toInput.trim();
    const nextOutcome = outcomeInput;

    if (nextFrom && nextTo && nextFrom > nextTo) {
      setNotice({ message: "The start date must be before the end date.", tone: "error" });
      return;
    }

    setPage(1);
    setNotice(null);

    const unchanged =
      nextCrisisId === crisisId &&
      nextFrom === filters.from &&
      nextTo === filters.to &&
      nextOutcome === filters.outcome;

    if (unchanged) {
      void load();
      return;
    }

    setCrisisId(nextCrisisId);
    setFilters({ from: nextFrom, to: nextTo, outcome: nextOutcome });
  }

  // The date/outcome bounds are sent to the review feed. Older feeds ignore
  // them, so narrow the returned page as well until the route adopts them.
  const visibleRows = (result?.items ?? []).filter((entry) => {
    if (filters.outcome && entry.outcome !== filters.outcome) {
      return false;
    }

    if (!filters.from && !filters.to) {
      return true;
    }

    const decidedAt = entry.decidedAt ? Date.parse(entry.decidedAt) : Number.NaN;

    if (filters.from && (!Number.isFinite(decidedAt) || decidedAt < Date.parse(toUtcBound(filters.from, false)))) {
      return false;
    }

    if (filters.to && (!Number.isFinite(decidedAt) || decidedAt > Date.parse(toUtcBound(filters.to, true)))) {
      return false;
    }

    return true;
  });

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
      key: "finalRequestStatus",
      header: "Final request status",
      render: (entry) => (
        <span>{entry.finalRequestStatus ?? "—"}</span>
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
            Append-only review of every recommendation, the officer outcome and the final request
            status recorded against it.
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
            <p>From the post-incident review feed</p>
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
          <Input
            label="From date (optional)"
            type="date"
            value={fromInput}
            onChange={(event) => setFromInput(event.target.value)}
          />
          <Input
            label="To date (optional)"
            type="date"
            value={toInput}
            onChange={(event) => setToInput(event.target.value)}
          />
          <label className="field">
            <span className="field__label">Outcome (optional)</span>
            <select
              className="input"
              value={outcomeInput}
              onChange={(event) => setOutcomeInput(event.target.value)}
            >
              <option value="">Any outcome</option>
              {DecisionOutcomes.map((outcome) => (
                <option key={outcome} value={outcome}>
                  {outcome}
                </option>
              ))}
            </select>
          </label>
          <Button type="submit" disabled={loading}>
            Apply filters
          </Button>
        </form>

        <div style={{ padding: "0 20px 20px" }}>
          {loading ? (
            <Skeleton rows={6} />
          ) : (
            <ServerTable
              columns={columns}
              rows={visibleRows}
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
