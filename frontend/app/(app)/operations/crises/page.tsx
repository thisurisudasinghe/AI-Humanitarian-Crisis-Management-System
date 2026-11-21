"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import { EmptyPanel } from "@/components/safeway/IntegrationNotice";
import { StaffRoles } from "@/lib/roles";
import {
  RESOLVED_REQUEST_STATUSES,
  caseRequestErrorMessage,
  getCrisisSummary,
  listCrises,
  listUnresolvedCaseRequests,
  type CaseRequestListItem,
  type CrisisRecord,
  type CrisisSummaryRecord
} from "@/lib/crisis";

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString();
}

function totalByStatus(summary?: CrisisSummaryRecord): number {
  if (!summary) return 0;
  return Object.values(summary.byStatus).reduce((total, count) => total + count, 0);
}

function resolvedByStatus(summary?: CrisisSummaryRecord): number {
  if (!summary) return 0;
  return RESOLVED_REQUEST_STATUSES.reduce(
    (total, status) => total + (summary.byStatus[status] ?? 0),
    0
  );
}

function urgencyLabel(summary?: CrisisSummaryRecord): string {
  if (!summary) return "No data";
  const order = ["Critical", "High", "Medium", "Low"] as const;
  const highest = order.find((level) => (summary.byUrgency[level] ?? 0) > 0);
  return highest ? `${highest} · ${summary.byUrgency[highest]}` : "No open";
}

function urgencyTone(label: string): string {
  if (label.startsWith("Critical")) return "coral";
  if (label.startsWith("High")) return "amber";
  return "green";
}

export default function CrisesPage() {
  const [query, setQuery] = useState("");
  const [crises, setCrises] = useState<CrisisRecord[]>([]);
  const [summaries, setSummaries] = useState<Record<string, CrisisSummaryRecord>>({});
  const [requests, setRequests] = useState<CaseRequestListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    setQuery(params.get("q") ?? "");
  }, []);

  useEffect(() => {
    let active = true;

    (async () => {
      try {
        setLoading(true);
        setError("");

        const [crisisList, unresolved] = await Promise.all([
          listCrises(),
          listUnresolvedCaseRequests()
        ]);

        const entries = await Promise.all(
          crisisList.map(async (crisis) => {
            try {
              return [crisis.id, await getCrisisSummary(crisis.id)] as const;
            } catch {
              return [crisis.id, null] as const;
            }
          })
        );

        const summaryMap: Record<string, CrisisSummaryRecord> = {};
        entries.forEach(([id, summary]) => {
          if (summary) summaryMap[id] = summary;
        });

        if (active) {
          setCrises(crisisList);
          setSummaries(summaryMap);
          setRequests(unresolved.items);
        }
      } catch (cause) {
        if (active) {
          setError(caseRequestErrorMessage(cause, "The incident queue could not be loaded."));
        }
      } finally {
        if (active) setLoading(false);
      }
    })();

    return () => {
      active = false;
    };
  }, []);

  const normalizedQuery = query.trim().toLowerCase();

  const visibleCrises = useMemo(() => {
    if (!normalizedQuery) return crises;
    return crises.filter(
      (crisis) =>
        crisis.name.toLowerCase().includes(normalizedQuery) ||
        crisis.type.toLowerCase().includes(normalizedQuery) ||
        (crisis.description ?? "").toLowerCase().includes(normalizedQuery)
    );
  }, [crises, normalizedQuery]);

  const visibleRequests = useMemo(() => {
    if (!normalizedQuery) return requests;
    return requests.filter(
      (request) =>
        request.description.toLowerCase().includes(normalizedQuery) ||
        request.category.toLowerCase().includes(normalizedQuery) ||
        request.status.toLowerCase().includes(normalizedQuery)
    );
  }, [requests, normalizedQuery]);

  const metrics = useMemo(() => {
    const allSummaries = Object.values(summaries);
    const total = allSummaries.reduce((sum, summary) => sum + totalByStatus(summary), 0);
    const resolved = allSummaries.reduce((sum, summary) => sum + resolvedByStatus(summary), 0);

    return [
      {
        title: "Active crises",
        icon: "alert",
        tone: "blue",
        value: crises.filter((crisis) => crisis.status === "Active").length,
        detail: `${crises.length} recorded`
      },
      {
        title: "Pending verification",
        icon: "clock",
        tone: "amber",
        value: requests.filter((request) => request.status === "Submitted").length,
        detail: "Unresolved queue"
      },
      {
        title: "Critical incidents",
        icon: "shield",
        tone: "coral",
        value: requests.filter((request) => request.urgency === "Critical").length,
        detail: "Unresolved queue"
      },
      {
        title: "Response progress",
        icon: "chart",
        tone: "green",
        value: `${total ? Math.round((resolved / total) * 100) : 0}%`,
        detail: `${resolved} of ${total} requests resolved`
      }
    ];
  }, [crises, requests, summaries]);

  return (
    <AuthGuard allowedRoles={StaffRoles}>
      <div className="ops-title">
        <div>
          <p>CRISES &amp; CASES</p>
          <h1>Incident queue</h1>
          <span>
            {query
              ? `Showing matches for “${query}”`
              : "Every reported incident, its severity and coordination state."}
          </span>
        </div>
        <div>
          <Link className="btn btn-outline" href="/operations/map">
            <Icon name="map" /> Open live map
          </Link>
          <Link className="btn btn-primary" href="/report">
            <Icon name="alert" /> New public report
          </Link>
        </div>
      </div>

      {error ? (
        <div className="form-error" role="alert" style={{ marginBottom: 16 }}>
          {error}
        </div>
      ) : null}

      <div className="metric-grid compact">
        {metrics.map((metric) => (
          <article className={`metric ${metric.tone}`} key={metric.title}>
            <div>
              <span>{metric.title}</span>
              <b>{metric.value}</b>
              <small>{metric.detail}</small>
            </div>
            <i>
              <Icon name={metric.icon} />
            </i>
          </article>
        ))}
      </div>

      <section className="ops-card crisis-table-card">
        <div className="card-heading">
          <div>
            <h3>All incidents</h3>
            <p>Priority incidents requiring coordination</p>
          </div>
          <div className="table-actions">
            <span className="badge green">
              <i /> Live case data
            </span>
          </div>
        </div>
        <div className="crisis-table">
          <div className="table-head">
            <span>INCIDENT</span>
            <span>SEVERITY</span>
            <span>ASSIGNED</span>
            <span>PROGRESS</span>
            <span>UPDATED</span>
            <span />
          </div>
          {loading ? (
            <EmptyPanel title="Loading incident queue">Reading crises and case summaries.</EmptyPanel>
          ) : visibleCrises.length === 0 ? (
            <EmptyPanel title="No incidents to display">
              {normalizedQuery
                ? "No crisis matches the current search."
                : "No crises have been recorded in the case service yet."}
            </EmptyPanel>
          ) : (
            visibleCrises.map((crisis) => {
              const summary = summaries[crisis.id];
              const severity = urgencyLabel(summary);
              const total = totalByStatus(summary);
              const open = summary?.openTotal ?? 0;
              const progress = total ? Math.round(((total - open) / total) * 100) : 0;

              return (
                <Link
                  className="table-row"
                  href={`/operations/crises/${crisis.id}`}
                  key={crisis.id}
                >
                  <span>
                    <i className={`priority ${severity.startsWith("Critical") ? "critical" : ""}`} />
                    <b>{crisis.name}</b>
                    <small>
                      {crisis.type} · {crisis.status}
                    </small>
                  </span>
                  <span>
                    <span className={`badge ${urgencyTone(severity)}`}>
                      <i /> {severity}
                    </span>
                  </span>
                  <span>—</span>
                  <span>
                    {open} open
                    <small>{total} reported</small>
                    <span className="progress">
                      <em style={{ width: `${progress}%` }} />
                    </span>
                  </span>
                  <span>{formatDate(crisis.startedAt)}</span>
                  <span>
                    <Icon name="arrow" size={14} />
                  </span>
                </Link>
              );
            })
          )}
        </div>
      </section>

      <section className="ops-card" style={{ marginTop: 16 }}>
        <div className="card-heading">
          <div>
            <h3>Case management</h3>
            <p>Unresolved requests requiring assignment or review</p>
          </div>
        </div>
        {loading ? (
          <EmptyPanel title="Loading case queue">Reading unresolved case requests.</EmptyPanel>
        ) : visibleRequests.length === 0 ? (
          <EmptyPanel title="No unresolved requests">
            {normalizedQuery
              ? "No unresolved request matches the current search."
              : "Every known request is currently resolved."}
          </EmptyPanel>
        ) : (
          <div className="crisis-table">
            <div className="table-head">
              <span>REQUEST</span>
              <span>URGENCY</span>
              <span>STATUS</span>
              <span>AGE</span>
              <span>SUBMITTED</span>
              <span />
            </div>
            {visibleRequests.slice(0, 10).map((request) => (
              <div className="table-row" key={request.id}>
                <span>
                  <i className={`priority ${request.urgency === "Critical" ? "critical" : ""}`} />
                  <b>{request.category}</b>
                  <small>{request.description}</small>
                </span>
                <span>
                  <span className={`badge ${request.urgency === "Critical" ? "coral" : "amber"}`}>
                    <i /> {request.urgency}
                  </span>
                </span>
                <span>{request.status}</span>
                <span>{request.slaAgeHours}h</span>
                <span>{formatDate(request.submittedAt)}</span>
                <span />
              </div>
            ))}
          </div>
        )}
      </section>
    </AuthGuard>
  );
}
