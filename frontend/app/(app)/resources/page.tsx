"use client";

import Link from "next/link";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import { EmptyPanel, IntegrationNotice } from "@/components/safeway/IntegrationNotice";
import { StaffRoles } from "@/lib/roles";

export default function ResourcesPage() {
  return (
    <AuthGuard allowedRoles={StaffRoles}>
      <div className="ops-title">
        <div>
          <p>LOGISTICS &amp; SUPPORT</p>
          <h1>Resources and shelters</h1>
          <span>Coordinate supplies, availability and safe spaces.</span>
        </div>
        <button
          className="btn btn-primary"
          type="button"
          disabled
          title="Resource creation requires the logistics API"
        >
          <Icon name="box" /> Add resource
        </button>
      </div>

      <div className="large-tabs">
        <Link className="active" href="/resources">
          <Icon name="box" /> Resource inventory
        </Link>
        <Link href="/shelters">
          <Icon name="home" /> Shelter network
        </Link>
      </div>

      <IntegrationNotice title="Inventory is not connected yet" owner="Dev 4 — logistics & shelter network">
        Stock levels, hub assignments and delivery states will come from the logistics endpoints in
        the Shared Contract. No quantities are shown until then, so readiness cannot be misread.
      </IntegrationNotice>

      <div className="metric-grid compact">
        {[
          ["Inventory categories", "box", "blue"],
          ["Available stock", "chart", "green"],
          ["Active requests", "clock", "amber"],
          ["In transit", "map", "blue"]
        ].map(([title, icon, tone]) => (
          <article className={`metric ${tone} is-pending`} key={title}>
            <div>
              <span>{title}</span>
              <b>—</b>
              <small>Awaiting logistics API</small>
            </div>
            <i>
              <Icon name={icon} />
            </i>
          </article>
        ))}
      </div>

      <section className="ops-card inventory">
        <div className="card-heading">
          <div>
            <h3>Resource inventory</h3>
            <p>Current stock across all logistics hubs</p>
          </div>
          <div className="table-actions">
            <span className="badge amber">
              <i /> Awaiting data
            </span>
          </div>
        </div>
        <EmptyPanel title="No inventory to display" owner="Dev 4 — logistics & shelter network">
          Each item card will show hub, quantity, available percentage and a manage action once the
          inventory API is published. This build intentionally ships without sample stock.
        </EmptyPanel>
      </section>

      <section className="ops-card" style={{ marginTop: 16 }}>
        <div className="card-heading">
          <div>
            <h3>Allocations &amp; deliveries</h3>
            <p>Requests, dispatch and in-transit tracking</p>
          </div>
        </div>
        <EmptyPanel title="Allocation workflow is pending" owner="Dev 4 — logistics & shelter network">
          Request queues, reservations, dispatch status and delivery confirmations will appear here.
        </EmptyPanel>
      </section>
    </AuthGuard>
  );
}
