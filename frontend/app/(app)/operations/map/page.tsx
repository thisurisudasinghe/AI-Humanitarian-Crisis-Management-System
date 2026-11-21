"use client";

import Link from "next/link";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import { EmptyPanel, IntegrationNotice } from "@/components/safeway/IntegrationNotice";
import { StaffRoles } from "@/lib/roles";

export default function OperationsMapPage() {
  return (
    <AuthGuard allowedRoles={StaffRoles}>
      <div className="ops-title">
        <div>
          <p>LIVE MAP</p>
          <h1>Situation map</h1>
          <span>Incidents, shelters and resources on one shared operational picture.</span>
        </div>
        <div>
          <Link className="btn btn-outline" href="/shelters">
            <Icon name="home" /> Shelter network
          </Link>
          <Link className="btn btn-primary" href="/resources">
            <Icon name="box" /> Resources
          </Link>
        </div>
      </div>

      <IntegrationNotice title="Map tiles and geospatial data are not connected" owner="Dev 2 and Dev 4">
        The designed map frame is in place, but live markers require incident locations from Dev 2
        and shelter/resource coordinates from Dev 4. No placeholder markers are drawn, so the map
        cannot be mistaken for a live view.
      </IntegrationNotice>

      <div className="ops-grid-main">
        <section className="ops-card live-map-card">
          <div className="card-heading">
            <div>
              <h3>Regional overview</h3>
              <p>Active incidents &amp; resources</p>
            </div>
            <span className="badge amber">
              <i /> Offline
            </span>
          </div>
          <div className="mini-map" style={{ minHeight: 420 }}>
            <div className="map-roads" />
            <div className="map-overlay">
              Map data not connected
              <small>Markers will appear when location APIs are available.</small>
            </div>
          </div>
          <div className="map-legend">
            <span>
              <i className="critical" />
              Incident
            </span>
            <span>
              <i />
              Shelter
            </span>
            <span>
              <i className="green" />
              Resource
            </span>
          </div>
        </section>

        <aside className="ops-card">
          <div className="card-heading">
            <div>
              <h3>Map layers</h3>
              <p>Available once geospatial data is connected</p>
            </div>
          </div>
          <div className="resource-bars">
            <div>
              <span>
                <b>Incidents</b>
                <i>Awaiting Dev 2 locations</i>
              </span>
            </div>
            <div>
              <span>
                <b>Shelters</b>
                <i>Awaiting Dev 4 capacity data</i>
              </span>
            </div>
            <div>
              <span>
                <b>Resources</b>
                <i>Awaiting Dev 4 inventory data</i>
              </span>
            </div>
            <div>
              <span>
                <b>Safe routes</b>
                <i>Awaiting verified route information</i>
              </span>
            </div>
          </div>
          <EmptyPanel title="No geospatial endpoint yet" owner="Dev 2 and Dev 4">
            Layer toggles and map filters will bind to the published location endpoints.
          </EmptyPanel>
        </aside>
      </div>
    </AuthGuard>
  );
}
