"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import { EmptyPanel, IntegrationNotice } from "@/components/safeway/IntegrationNotice";
import { StaffRoles } from "@/lib/roles";

export default function CrisesPage() {
  const [query, setQuery] = useState("");

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    setQuery(params.get("q") ?? "");
  }, []);

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

      <IntegrationNotice title="The incident queue is not connected yet" owner="Dev 2 — crisis & case management">
        This queue renders the design&apos;s table structure for the crisis and case endpoints. No
        incidents are shown until the Dev 2 API is available, so severity and progress figures can
        never be mistaken for live data.
      </IntegrationNotice>

      <div className="metric-grid compact">
        {[
          ["Active crises", "alert", "blue"],
          ["Pending verification", "clock", "amber"],
          ["Critical incidents", "shield", "coral"],
          ["Response progress", "chart", "green"]
        ].map(([title, icon, tone]) => (
          <article className={`metric ${tone} is-pending`} key={title}>
            <div>
              <span>{title}</span>
              <b>—</b>
              <small>Awaiting crisis API</small>
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
            <h3>All incidents</h3>
            <p>Priority incidents requiring coordination</p>
          </div>
          <div className="table-actions">
            <span className="badge amber">
              <i /> Awaiting data
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
          <div className="table-empty">
            <EmptyPanel title="No incidents to display" owner="Dev 2 — crisis & case management">
              Incident rows, severity badges, assigned teams and progress bars will populate from
              the crisis and case controllers. This build intentionally ships without sample
              incidents.
            </EmptyPanel>
          </div>
        </div>
      </section>

      <section className="ops-card" style={{ marginTop: 16 }}>
        <div className="card-heading">
          <div>
            <h3>Case management</h3>
            <p>Assignments, verification and decision history</p>
          </div>
        </div>
        <EmptyPanel title="Case workflow is pending" owner="Dev 2 — crisis & case management">
          Case tabs, assignment history and the append-only decision trail will appear with the
          case API. Audit events for identity and donation actions remain available under
          Administration → Audit log.
        </EmptyPanel>
      </section>
    </AuthGuard>
  );
}
