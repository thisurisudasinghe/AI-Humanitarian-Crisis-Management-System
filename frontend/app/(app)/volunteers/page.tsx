"use client";

import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import { EmptyPanel, IntegrationNotice } from "@/components/safeway/IntegrationNotice";
import { StaffRoles } from "@/lib/roles";

export default function VolunteersPage() {
  return (
    <AuthGuard allowedRoles={StaffRoles}>
      <div className="ops-title">
        <div>
          <p>VOLUNTEER COORDINATION</p>
          <h1>Volunteers</h1>
          <span>Skills, availability and deployment across the response network.</span>
        </div>
        <button
          className="btn btn-primary"
          type="button"
          disabled
          title="Volunteer registration requires the volunteer API"
        >
          <Icon name="users" /> Add volunteer
        </button>
      </div>

      <IntegrationNotice title="Volunteer coordination is not connected yet" owner="Dev 4 — volunteer management">
        Registration, skill matching and deployment tracking are part of the volunteer module in the
        Shared Contract. Until its endpoints are published, this workspace shows no volunteer
        records and no availability figures.
      </IntegrationNotice>

      <div className="metric-grid compact">
        {[
          ["Registered volunteers", "users", "blue"],
          ["Available now", "check", "green"],
          ["On deployment", "pin", "amber"],
          ["Skills registered", "chart", "blue"]
        ].map(([title, icon, tone]) => (
          <article className={`metric ${tone} is-pending`} key={title}>
            <div>
              <span>{title}</span>
              <b>—</b>
              <small>Awaiting volunteer API</small>
            </div>
            <i>
              <Icon name={icon} />
            </i>
          </article>
        ))}
      </div>

      <section className="ops-card crisis-table-card">
        <div className="card-heading">
          <div>
            <h3>Volunteer roster</h3>
            <p>Registered people and their current assignment</p>
          </div>
          <div className="table-actions">
            <span className="badge amber">
              <i /> Awaiting data
            </span>
          </div>
        </div>
        <div className="crisis-table">
          <div className="table-head">
            <span>VOLUNTEER</span>
            <span>SKILLS</span>
            <span>AREA</span>
            <span>AVAILABILITY</span>
            <span>STATUS</span>
            <span />
          </div>
          <div className="table-empty">
            <EmptyPanel title="No volunteers to display" owner="Dev 4 — volunteer management">
              Rows will populate from the volunteer API, including skills, availability windows and
              deployment history. No sample volunteers are shown.
            </EmptyPanel>
          </div>
        </div>
      </section>

      <section className="ops-card" style={{ marginTop: 16 }}>
        <div className="card-heading">
          <div>
            <h3>Deployments</h3>
            <p>Assignments, check-ins and hours</p>
          </div>
        </div>
        <EmptyPanel title="Deployment tracking is pending" owner="Dev 4 — volunteer management">
          Assignment, check-in and hours reporting will appear once the volunteer endpoints are
          connected.
        </EmptyPanel>
      </section>
    </AuthGuard>
  );
}
