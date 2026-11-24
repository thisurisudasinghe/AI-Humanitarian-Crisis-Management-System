"use client";

import Link from "next/link";
import { formatDate as formatDateForLocale, resolveLocale } from "@/lib/i18n/core";
import { useEffect, useMemo, useState } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import { EmptyPanel } from "@/components/safeway/EmptyPanel";
import { useT } from "@/lib/i18n";
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
  const resolved = typeof document === "undefined" ? "en" : resolveLocale(document.documentElement.lang);
  return formatDateForLocale(value, resolved, { dateStyle: "medium" });
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

function urgencyLabel(summary: CrisisSummaryRecord | undefined, t: (key: string) => string): string {
  if (!summary) return t("operations.crises.noData");
  const order = ["Critical", "High", "Medium", "Low"] as const;
  const highest = order.find((level) => (summary.byUrgency[level] ?? 0) > 0);
  return highest ? `${highest} · ${summary.byUrgency[highest]}` : t("operations.crises.noOpen");
}

function urgencyTone(label: string): string {
  if (label.startsWith("Critical")) return "coral";
  if (label.startsWith("High")) return "amber";
  return "green";
}

export default function CrisesPage() {
  const t = useT();
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
          setError(caseRequestErrorMessage(cause, t("operations.crises.loadError"), t));
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
        title: t("operations.crises.metricActiveCrises"),
        icon: "alert",
        tone: "blue",
        value: crises.filter((crisis) => crisis.status === "Active").length,
        detail: t("operations.crises.recorded", { count: crises.length })
      },
      {
        title: t("operations.crises.metricPendingVerification"),
        icon: "clock",
        tone: "amber",
        value: requests.filter((request) => request.status === "Submitted").length,
        detail: t("operations.crises.unresolvedQueue")
      },
      {
        title: t("operations.crises.metricCriticalIncidents"),
        icon: "shield",
        tone: "coral",
        value: requests.filter((request) => request.urgency === "Critical").length,
        detail: t("operations.crises.unresolvedQueue")
      },
      {
        title: t("operations.crises.metricResponseProgress"),
        icon: "chart",
        tone: "green",
        value: `${total ? Math.round((resolved / total) * 100) : 0}%`,
        detail: t("operations.crises.resolvedOf", { resolved, total })
      }
    ];
  }, [crises, requests, summaries, t]);

  return (
    <AuthGuard allowedRoles={StaffRoles}>
      <div className="ops-title">
        <div>
          <p>{t("operations.crises.eyebrow")}</p>
          <h1>{t("operations.crises.incidentQueue")}</h1>
          <span>
            {query
              ? t("operations.crises.showingMatches", { query })
              : t("operations.crises.subtitle")}
          </span>
        </div>
        <div>
          <Link className="btn btn-outline" href="/operations/map">
            <Icon name="map" /> {t("operations.overview.openLiveMap")}
          </Link>
          <Link className="btn btn-primary" href="/report">
            <Icon name="alert" /> {t("operations.crises.newPublicReport")}
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
            <h3>{t("operations.crises.allIncidents")}</h3>
            <p>{t("operations.shared.priorityIncidents")}</p>
          </div>
          <div className="table-actions">
            <span className="badge green">
              <i /> {t("operations.crises.liveCaseData")}
            </span>
          </div>
        </div>
        <div className="crisis-table">
          <div className="table-head">
            <span>{t("operations.shared.incident")}</span>
            <span>{t("operations.shared.severity")}</span>
            <span>{t("operations.shared.assigned")}</span>
            <span>{t("operations.shared.progress")}</span>
            <span>{t("operations.shared.updated")}</span>
            <span />
          </div>
          {loading ? (
            <EmptyPanel title={t("operations.crises.loadingQueue")}>{t("operations.crises.loadingQueueBody")}</EmptyPanel>
          ) : visibleCrises.length === 0 ? (
            <EmptyPanel title={t("operations.crises.noIncidents")}>
              {normalizedQuery
                ? t("operations.crises.noCrisisMatch")
                : t("operations.crises.noCrisisRecorded")}
            </EmptyPanel>
          ) : (
            visibleCrises.map((crisis) => {
              const summary = summaries[crisis.id];
              const severity = urgencyLabel(summary, t);
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
                    {t("operations.crises.openCount", { count: open })}
                    <small>{t("operations.crises.reportedCount", { count: total })}</small>
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
            <h3>{t("operations.crises.caseManagement")}</h3>
            <p>{t("operations.crises.caseSubtitle")}</p>
          </div>
        </div>
        {loading ? (
          <EmptyPanel title={t("operations.crises.loadingCases")}>{t("operations.crises.loadingCasesBody")}</EmptyPanel>
        ) : visibleRequests.length === 0 ? (
          <EmptyPanel title={t("operations.crises.noUnresolved")}>
            {normalizedQuery
              ? t("operations.crises.noUnresolvedMatch")
              : t("operations.crises.allResolved")}
          </EmptyPanel>
        ) : (
          <div className="crisis-table">
            <div className="table-head">
              <span>{t("operations.crises.requestHeader")}</span>
              <span>{t("operations.crises.urgencyHeader")}</span>
              <span>{t("operations.crises.statusHeader")}</span>
              <span>{t("operations.crises.ageHeader")}</span>
              <span>{t("operations.crises.submittedHeader")}</span>
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
