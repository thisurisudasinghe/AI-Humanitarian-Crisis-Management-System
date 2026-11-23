"use client";

import Link from "next/link";
import { formatDate as formatDateForLocale, resolveLocale } from "@/lib/i18n/core";
import { useParams } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import { EmptyPanel } from "@/components/safeway/IntegrationNotice";
import { useT } from "@/lib/i18n";
import { StaffRoles } from "@/lib/roles";
import {
  caseRequestErrorMessage,
  getCrisisSummary,
  listAreas,
  listCrises,
  listStaffCaseRequests,
  type AreaRecord,
  type CaseRequestListItem,
  type CrisisRecord,
  type CrisisSummaryRecord
} from "@/lib/crisis";

function formatDate(value: string): string {
  const resolved = typeof document === "undefined" ? "en" : resolveLocale(document.documentElement.lang);
  return formatDateForLocale(value, resolved);
}

function totalByStatus(summary?: CrisisSummaryRecord): number {
  if (!summary) return 0;
  return Object.values(summary.byStatus).reduce((total, count) => total + count, 0);
}

export default function CrisisDetailPage() {
  const t = useT();
  const params = useParams<{ id: string }>();
  const id = typeof params?.id === "string" ? params.id : "";

  const [crisis, setCrisis] = useState<CrisisRecord | null>(null);
  const [summary, setSummary] = useState<CrisisSummaryRecord | null>(null);
  const [areas, setAreas] = useState<AreaRecord[]>([]);
  const [requests, setRequests] = useState<CaseRequestListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!id) {
      setError(t("operations.crisisDetail.noReference"));
      setLoading(false);
      return;
    }

    let active = true;

    (async () => {
      try {
        setLoading(true);
        setError("");

        const [crises, areaList, requestPage] = await Promise.all([
          listCrises(),
          listAreas(id),
          listStaffCaseRequests({ crisisId: id })
        ]);

        let summaryResult: CrisisSummaryRecord | null = null;
        try {
          summaryResult = await getCrisisSummary(id);
        } catch {
          summaryResult = null;
        }

        if (active) {
          setCrisis(crises.find((item) => item.id === id) ?? null);
          setSummary(summaryResult);
          setAreas(areaList);
          setRequests(requestPage.items);
        }
      } catch (cause) {
        if (active) {
          setError(caseRequestErrorMessage(cause, t("operations.crisisDetail.loadError"), t));
        }
      } finally {
        if (active) setLoading(false);
      }
    })();

    return () => {
      active = false;
    };
  }, [id]);

  const total = useMemo(() => totalByStatus(summary ?? undefined), [summary]);
  const statusEntries = useMemo(
    () => Object.entries(summary?.byStatus ?? {}).filter(([, count]) => count > 0),
    [summary]
  );

  return (
    <AuthGuard allowedRoles={StaffRoles}>
      <Link className="back-link ops-back" href="/operations/crises">
        {t("operations.crisisDetail.back")}
      </Link>

      <div className="case-header">
        <div>
          <div className="case-kicker">
            <span className={`badge ${crisis?.status === "Active" ? "green" : "amber"}`}>
              <i /> {loading ? t("operations.crisisDetail.loadingBadge") : (crisis?.status ?? t("operations.crisisDetail.notFound"))}
            </span>
            <span>{crisis?.id ?? id}</span>
          </div>
          <h1>{crisis?.name ?? t("operations.crisisDetail.fallbackTitle")}</h1>
          <p>
            <Icon name="pin" size={16} />{" "}
            {crisis
              ? `${crisis.type} · ${t("operations.crisisDetail.started", { date: formatDate(crisis.startedAt) })}`
              : t("operations.crisisDetail.detailsUnavailable")}
          </p>
        </div>
        <div>
          <button className="btn btn-outline" type="button" disabled>
            {t("operations.crisisDetail.moreActions")}
          </button>
          <button className="btn btn-primary" type="button" disabled>
            {t("operations.crisisDetail.assignTeam")}
          </button>
        </div>
      </div>

      {error ? (
        <div className="form-error" role="alert" style={{ marginBottom: 16 }}>
          {error}
        </div>
      ) : null}

      <div className="case-tabs">
        <button className="active" type="button">
          {t("operations.crisisDetail.tabOverview")}
        </button>
        <button type="button" disabled>
          {t("operations.crisisDetail.tabReports")} <i>{total || "—"}</i>
        </button>
        <button type="button" disabled>
          {t("operations.crisisDetail.tabResponse")}
        </button>
        <button type="button" disabled>
          {t("operations.crisisDetail.tabResources")}
        </button>
        <button type="button" disabled>
          {t("operations.shared.decisionHistory")}
        </button>
      </div>

      <div className="case-layout">
        <div className="case-main">
          <section className="ops-card situation-card">
            <div className="card-heading">
              <div>
                <h3>{t("operations.crisisDetail.situationTitle")}</h3>
                <p>{t("operations.crisisDetail.situationSubtitle")}</p>
              </div>
              <span className={`badge ${summary?.overdueTotal ? "coral" : "green"}`}>
                <i /> {summary?.overdueTotal ? t("operations.crisisDetail.overdueBadge") : t("operations.crisisDetail.onTrackBadge")}
              </span>
            </div>
            <div className="situation-stats">
              {[
                [t("operations.crisisDetail.affectedAreas"), loading ? "—" : areas.length],
                [t("operations.crisisDetail.peopleAtRisk"), t("operations.crisisDetail.notCaptured")],
                [t("operations.crisisDetail.reports"), loading ? "—" : total],
                [
                  t("operations.crisisDetail.firstReported"),
                  crisis ? formatDate(crisis.startedAt) : "—"
                ]
              ].map(([label, value]) => (
                <div key={label}>
                  <span>{label}</span>
                  <b>{value}</b>
                </div>
              ))}
            </div>
            {crisis?.description ? (
              <p style={{ padding: "0 22px 20px", color: "var(--muted)", fontSize: 12 }}>
                {crisis.description}
              </p>
            ) : null}
          </section>

          <section className="ops-card ai-review">
            <div className="ai-review-title">
              <span>
                <Icon name="brain" />
              </span>
              <div>
                <i>{t("operations.shared.aiDecisionSupport")}</i>
                <h3>{t("operations.crisisDetail.riskRecommendation")}</h3>
              </div>
              <small>
                <Icon name="shield" size={13} /> {t("operations.shared.requiresHumanReview")}
              </small>
            </div>
            <div className="recommendation">
              <div className="rec-score">
                <span>{t("operations.crisisDetail.recommendedPriority")}</span>
                <b>—</b>
                <em>{t("operations.crisisDetail.noRecommendation")}</em>
              </div>
              <div className="confidence-ring">
                <span>
                  —<small>%</small>
                </span>
              </div>
            </div>
            <div className="reasoning">
              <b>{t("operations.shared.supportingSignals")}</b>
              <ul>
                <li>{t("operations.crisisDetail.recommendationNotice")}</li>
              </ul>
            </div>
            <div className="review-actions">
              <button className="btn btn-outline" type="button" disabled>
                {t("operations.crisisDetail.requestInfo")}
              </button>
              <button className="btn btn-primary" type="button" disabled>
                <Icon name="check" /> {t("operations.crisisDetail.approve")}
              </button>
            </div>
            <p className="metric__label" style={{ padding: "0 22px 20px" }}>
              {t("operations.crisisDetail.humanApprovalNote")}
            </p>
          </section>

          <section className="ops-card" style={{ marginTop: 16 }}>
            <div className="card-heading">
              <div>
                <h3>{t("operations.crisisDetail.submittedReports")}</h3>
                <p>{t("operations.crisisDetail.reportsSubtitle")}</p>
              </div>
            </div>
            {loading ? (
              <EmptyPanel title={t("operations.crisisDetail.loadingReports")}>{t("operations.crisisDetail.loadingReportsBody")}</EmptyPanel>
            ) : requests.length === 0 ? (
              <EmptyPanel title={t("operations.crisisDetail.noLinkedReports")}>
                {t("operations.crisisDetail.noLinkedBody")}
              </EmptyPanel>
            ) : (
              <div className="reports-detail-list">
                {requests.slice(0, 8).map((request) => (
                  <article key={request.id}>
                    <span className={`badge ${request.urgency === "Critical" ? "coral" : "amber"}`}>
                      <i /> {request.urgency}
                    </span>
                    <div className="report-detail-main">
                      <span>
                        <b>{request.category} {t("operations.crisisDetail.reportSuffix")}</b>
                      </span>
                      <p>{request.description}</p>
                      <small>{formatDate(request.submittedAt)}</small>
                    </div>
                    <span className="badge green">
                      <i /> {request.status}
                    </span>
                    <span>{request.isOverdue ? t("operations.crisisDetail.overdue") : t("operations.crisisDetail.onTrack")}</span>
                  </article>
                ))}
              </div>
            )}
          </section>
        </div>

        <aside className="case-side">
          <section className="ops-card">
            <div className="card-heading">
              <div>
                <h3>{t("operations.crisisDetail.responseStatus")}</h3>
                <p>{t("operations.crisisDetail.responseSubtitle")}</p>
              </div>
            </div>
            <div className="response-progress">
              <span>
                <b>{loading ? "—" : (summary?.openTotal ?? 0)}</b> {t("operations.crisisDetail.openSuffix")}
              </span>
              <em>
                <i
                  style={{
                    width: `${total && summary ? Math.round((summary.openTotal / total) * 100) : 0}%`
                  }}
                />
              </em>
            </div>
            <div className="timeline">
              {statusEntries.length === 0 ? (
                <EmptyPanel title={t("operations.crisisDetail.noStatusHistory")}>{t("operations.crisisDetail.noStatusBody")}</EmptyPanel>
              ) : (
                statusEntries.slice(0, 6).map(([label, count], index) => (
                  <div className={index === 0 ? "active" : ""} key={label}>
                    <i />
                    <span>
                      <b>{label}</b>
                      <small>{t("operations.crisisDetail.requestCount", { count })}</small>
                    </span>
                  </div>
                ))
              )}
            </div>
          </section>

          <section className="ops-card assigned-card">
            <div className="card-heading">
              <div>
                <h3>{t("operations.crisisDetail.assignedTeam")}</h3>
              </div>
            </div>
            <EmptyPanel title={t("operations.crisisDetail.noTeam")}>
              {t("operations.crisisDetail.noTeamBody")}
            </EmptyPanel>
          </section>

          <section className="ops-card audit-note">
            <Icon name="shield" />
            <div>
              <b>{t("operations.crisisDetail.governance")}</b>
              <p>{t("operations.crisisDetail.governanceBody")}</p>
              <Link href="/admin/audit">
                {t("operations.shared.viewAuditLog")} <Icon name="arrow" size={14} />
              </Link>
            </div>
          </section>
        </aside>
      </div>
    </AuthGuard>
  );
}
