"use client";

import Link from "next/link";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import { EmptyPanel, IntegrationNotice } from "@/components/safeway/IntegrationNotice";
import { StaffRoles } from "@/lib/roles";

export default function SheltersPage() {
  return (
    <AuthGuard allowedRoles={StaffRoles}>
      <div className="ops-title">
        <div>
          <p>LOGISTICS &amp; SUPPORT</p>
          <h1>Shelter network</h1>
          <span>Verified safe spaces, occupancy and accessibility at a glance.</span>
        </div>
        <button
          className="btn btn-primary"
          type="button"
          disabled
          title="Shelter creation requires the shelter API"
        >
          <Icon name="home" /> Add shelter
        </button>
      </div>

      <div className="large-tabs">
        <Link href="/resources">
          <Icon name="box" /> Resource inventory
        </Link>
        <Link className="active" href="/shelters">
          <Icon name="home" /> Shelter network
        </Link>
      </div>

      <IntegrationNotice title="Shelter capacity is not connected yet" owner="Dev 4 — logistics & shelter network">
        Occupancy, accessibility features and open/limited states come from the shelter endpoints.
        Nothing is pre-filled here, so a safe space is never shown as available without live data.
      </IntegrationNotice>

      <div className="shelter-layout">
        <section className="ops-card shelter-list">
          <div className="card-heading">
            <div>
              <h3>Shelter availability</h3>
              <p>Live occupancy across verified safe spaces</p>
            </div>
            <Link href="/operations/map">
              Map view <Icon name="arrow" size={15} />
            </Link>
          </div>
          <EmptyPanel title="No shelters to display" owner="Dev 4 — logistics & shelter network">
            Shelter rows will list name, district, occupancy, capacity state and a detail action once
            the shelter API is available.
          </EmptyPanel>
        </section>

        <aside className="ops-card shelter-map">
          <div className="mini-map">
            <div className="map-roads" />
            <div className="map-overlay">
              Shelter map not connected
              <small>Locations will come from the shelter API.</small>
            </div>
          </div>
          <div>
            <b>—</b>
            <span>spaces currently available</span>
          </div>
        </aside>
      </div>

      <section className="ops-card" style={{ marginTop: 16 }}>
        <div className="card-heading">
          <div>
            <h3>Shelter requests</h3>
            <p>Placement requests and referrals</p>
          </div>
        </div>
        <EmptyPanel title="Placement workflow is pending" owner="Dev 4 — logistics & shelter network">
          Placement requests, referrals and accessibility matching will render here.
        </EmptyPanel>
      </section>
    </AuthGuard>
  );
}
