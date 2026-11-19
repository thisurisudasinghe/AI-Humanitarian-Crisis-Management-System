"use client";

import Link from "next/link";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import { EmptyPanel, IntegrationNotice } from "@/components/safeway/IntegrationNotice";
import { StaffRoles } from "@/lib/roles";

export default function AiDecisionSupportPage() {
  return (
    <AuthGuard allowedRoles={StaffRoles}>
      <div className="ops-title">
        <div>
          <p>AI DECISION SUPPORT</p>
          <h1>Recommendations &amp; human review</h1>
          <span>
            Risk signals and suggested responses stay advisory. A named officer always decides.
          </span>
        </div>
        <div>
          <Link className="btn btn-outline" href="/operations/crises">
            <Icon name="alert" /> Incident queue
          </Link>
        </div>
      </div>

      <IntegrationNotice title="Decision support is not connected yet" owner="Dev 3 — AI decision support">
        The recommendation card, confidence indicator and approval controls are ready for the Dev 3
        service. Until it publishes endpoints, this page shows no scores and approval stays
        disabled — the platform never fabricates AI output.
      </IntegrationNotice>

      <section className="ops-card ai-review">
        <div className="ai-review-title">
          <span>
            <Icon name="brain" />
          </span>
          <div>
            <i>AI DECISION SUPPORT</i>
            <h3>Pending review queue</h3>
          </div>
          <small>
            <Icon name="shield" size={13} /> Requires human review
          </small>
        </div>

        <div className="recommendation">
          <div className="rec-score">
            <span>RECOMMENDED PRIORITY</span>
            <b>—</b>
            <em>No active recommendation</em>
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
            <li>Signals will appear here with their source and timestamp.</li>
            <li>Every recommendation will show the data it was derived from.</li>
          </ul>
        </div>

        <div className="suggested">
          <b>Suggested response</b>
          <EmptyPanel title="No suggestions to review" owner="Dev 3 — AI decision support">
            Suggested actions, their rationale and the approving officer will render here once the
            decision-support module is available.
          </EmptyPanel>
        </div>

        <div className="review-actions">
          <button className="btn btn-outline" type="button" disabled>
            Request more information
          </button>
          <button className="btn btn-primary" type="button" disabled>
            <Icon name="check" /> Approve recommendation
          </button>
        </div>
      </section>

      <section className="ops-card" style={{ marginTop: 16 }}>
        <div className="card-heading">
          <div>
            <h3>Human oversight rules</h3>
            <p>How this platform handles automated suggestions</p>
          </div>
        </div>
        <div className="resource-bars">
          <div>
            <span>
              <b>Advisory only</b>
              <i>AI output never triggers a response on its own.</i>
            </span>
          </div>
          <div>
            <span>
              <b>Named approver</b>
              <i>An authorized officer approves or rejects each recommendation.</i>
            </span>
          </div>
          <div>
            <span>
              <b>Append-only record</b>
              <i>Approvals and overrides are written to the audit trail.</i>
            </span>
          </div>
        </div>
      </section>
    </AuthGuard>
  );
}
