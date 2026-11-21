"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import { EmptyPanel, IntegrationNotice } from "@/components/safeway/IntegrationNotice";
import { StaffRoles } from "@/lib/roles";

export default function CrisisDetailPage() {
  const params = useParams<{ id: string }>();
  const id = typeof params?.id === "string" ? params.id : "";

  return (
    <AuthGuard allowedRoles={StaffRoles}>
      <Link className="back-link ops-back" href="/operations/crises">
        ← Back to active crises
      </Link>

      <div className="case-header">
        <div>
          <div className="case-kicker">
            <span className="badge amber">
              <i /> AWAITING DATA
            </span>
            <span>{id || "Crisis reference"}</span>
          </div>
          <h1>Crisis record</h1>
          <p>
            <Icon name="pin" size={16} /> Location, severity and reporting time will load from the
            crisis API.
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

      <div className="case-tabs">
        <button className="active" type="button">
          Overview
        </button>
        <button type="button" disabled>
          Reports <i>—</i>
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

      <IntegrationNotice title="Case detail is not connected yet" owner="Dev 2 — crisis & case management">
        This page keeps the designed case workspace intact. Situation details, reports and
        assignments stay empty until the Dev 2 case endpoints can supply them, and nothing is
        invented in the meantime.
      </IntegrationNotice>

      <div className="case-layout">
        <div className="case-main">
          <section className="ops-card situation-card">
            <div className="card-heading">
              <div>
                <h3>Situation overview</h3>
                <p>Verified information from submitted reports</p>
              </div>
              <span className="badge amber">
                <i /> NOT VERIFIED
              </span>
            </div>
            <div className="situation-stats">
              {[
                ["AFFECTED AREA", "—"],
                ["PEOPLE AT RISK", "—"],
                ["REPORTS", "—"],
                ["FIRST REPORTED", "—"]
              ].map(([label, value]) => (
                <div key={label}>
                  <span>{label}</span>
                  <b>{value}</b>
                </div>
              ))}
            </div>
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
                <em>No recommendation yet</em>
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
                <li>Risk signals will be listed when the Dev 3 decision-support service is connected.</li>
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
              Approval is disabled because there is no recommendation to approve. Human approval
              remains mandatory once AI suggestions are available.
            </p>
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
                <b>—</b> coordinated
              </span>
              <em>
                <i style={{ width: "0%" }} />
              </em>
            </div>
            <div className="timeline">
              {[
                ["Report verified", "Pending"],
                ["Response team assignment", "Pending"],
                ["On-site assessment", "Pending"],
                ["Assistance delivery", "Pending"]
              ].map(([label, meta]) => (
                <div key={label}>
                  <i />
                  <span>
                    <b>{label}</b>
                    <small>{meta}</small>
                  </span>
                </div>
              ))}
            </div>
          </section>

          <section className="ops-card assigned-card">
            <div className="card-heading">
              <div>
                <h3>Assigned team</h3>
              </div>
            </div>
            <EmptyPanel title="No team assigned" owner="Dev 2 — crisis & case management">
              Team assignment, lead and ETA appear once the case API is connected.
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
