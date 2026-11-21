"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import { EmptyPanel } from "@/components/safeway/IntegrationNotice";
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
  return new Date(value).toLocaleString();
}

function totalByStatus(summary?: CrisisSummaryRecord): number {
  if (!summary) return 0;
  return Object.values(summary.byStatus).reduce((total, count) => total + count, 0);
}

export default function CrisisDetailPage() {
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
      setError("No crisis reference was supplied.");
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
          setError(caseRequestErrorMessage(cause, "The crisis record could not be loaded."));
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
        ← Back to active crises
      </Link>

      <div className="case-header">
        <div>
          <div className="case-kicker">
            <span className={`badge ${crisis?.status === "Active" ? "green" : "amber"}`}>
              <i /> {loading ? "LOADING" : (crisis?.status ?? "NOT FOUND")}
            </span>
            <span>{crisis?.id ?? id}</span>
          </div>
          <h1>{crisis?.name ?? "Crisis record"}</h1>
          <p>
            <Icon name="pin" size={16} />{" "}
            {crisis
              ? `${crisis.type} · started ${formatDate(crisis.startedAt)}`
              : "Crisis details are not available in the case service."}
          </p>
        </div>
        <div>
          <button className="btn btn-outline" type="button" disabled>
            More actions
          </button>
          <button className="btn btn-primary" type="button" disabled>
            Assign response team
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
          Overview
        </button>
        <button type="button" disabled>
          Reports <i>{total || "—"}</i>
        </button>
        <button type="button" disabled>
          Response
        </button>
        <button type="button" disabled>
          Resources
        </button>
        <button type="button" disabled>
          Decision history
        </button>
      </div>

      <div className="case-layout">
        <div className="case-main">
          <section className="ops-card situation-card">
            <div className="card-heading">
              <div>
                <h3>Situation overview</h3>
                <p>Verified information from submitted reports</p>
              </div>
              <span className={`badge ${summary?.overdueTotal ? "coral" : "green"}`}>
                <i /> {summary?.overdueTotal ? "OVERDUE REPORTS" : "ON TRACK"}
              </span>
            </div>
            <div className="situation-stats">
              {[
                ["AFFECTED AREAS", loading ? "—" : areas.length],
                ["PEOPLE AT RISK", "Not captured"],
                ["REPORTS", loading ? "—" : total],
                [
                  "FIRST REPORTED",
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
                <i>AI DECISION SUPPORT</i>
                <h3>Risk &amp; priority recommendation</h3>
              </div>
              <small>
                <Icon name="shield" size={13} /> Requires human review
              </small>
            </div>
            <div className="recommendation">
              <div className="rec-score">
                <span>RECOMMENDED PRIORITY</span>
                <b>—</b>
                <em>No recommendation on this page</em>
              </div>
              <div className="confidence-ring">
                <span>
                  —<small>%</small>
                </span>
              </div>
            </div>
            <div className="reasoning">
              <b>Supporting signals</b>
              <ul>
                <li>Recommendations are owned by Dev 3 and are not connected to this case view.</li>
              </ul>
            </div>
            <div className="review-actions">
              <button className="btn btn-outline" type="button" disabled>
                Request more information
              </button>
              <button className="btn btn-primary" type="button" disabled>
                <Icon name="check" /> Approve recommendation
              </button>
            </div>
            <p className="metric__label" style={{ padding: "0 22px 20px" }}>
              Human approval remains mandatory once AI suggestions are available.
            </p>
          </section>

          <section className="ops-card" style={{ marginTop: 16 }}>
            <div className="card-heading">
              <div>
                <h3>Submitted reports</h3>
                <p>Requests linked to this crisis in the case service</p>
              </div>
            </div>
            {loading ? (
              <EmptyPanel title="Loading reports">Reading requests for this crisis.</EmptyPanel>
            ) : requests.length === 0 ? (
              <EmptyPanel title="No linked reports">
                No case requests have been submitted against this crisis yet.
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
                        <b>{request.category} report</b>
                      </span>
                      <p>{request.description}</p>
                      <small>{formatDate(request.submittedAt)}</small>
                    </div>
                    <span className="badge green">
                      <i /> {request.status}
                    </span>
                    <span>{request.isOverdue ? "Overdue" : "On track"}</span>
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
                <h3>Response status</h3>
                <p>Current coordination progress</p>
              </div>
            </div>
            <div className="response-progress">
              <span>
                <b>{loading ? "—" : (summary?.openTotal ?? 0)}</b> open
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
                <EmptyPanel title="No status history">Case status counts will appear here.</EmptyPanel>
              ) : (
                statusEntries.slice(0, 6).map(([label, count], index) => (
                  <div className={index === 0 ? "active" : ""} key={label}>
                    <i />
                    <span>
                      <b>{label}</b>
                      <small>{count} requests</small>
                    </span>
                  </div>
                ))
              )}
            </div>
          </section>

          <section className="ops-card assigned-card">
            <div className="card-heading">
              <div>
                <h3>Assigned team</h3>
              </div>
            </div>
            <EmptyPanel title="No team in case data">
              The request list does not include an assigned team field.
            </EmptyPanel>
          </section>

          <section className="ops-card audit-note">
            <Icon name="shield" />
            <div>
              <b>Governance &amp; audit</b>
              <p>All recommendations, approvals and response actions are recorded.</p>
              <Link href="/admin/audit">
                View audit log <Icon name="arrow" size={14} />
              </Link>
            </div>
          </section>
        </aside>
      </div>
    </AuthGuard>
  );
}
