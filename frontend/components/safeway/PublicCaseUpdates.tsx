"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { Icon } from "./Icon";

interface PublicAlert {
  id: string;
  crisisId?: string | null;
  title: string;
  body: string;
  status: string;
  publishedAt?: string | null;
  createdAt: string;
}

interface PublicServiceCentre {
  id: string;
  crisisId?: string | null;
  name: string;
  address: string;
  lat: number;
  lng: number;
  services: string;
  openingHours?: string | null;
}

function formatDate(value?: string | null): string {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : date.toLocaleString();
}

export function PublicCaseUpdates() {
  const [alerts, setAlerts] = useState<PublicAlert[]>([]);
  const [centres, setCentres] = useState<PublicServiceCentre[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;
    Promise.all([
      api<PublicAlert[]>("/api/v1/public/alerts"),
      api<PublicServiceCentre[]>("/api/v1/public/service-centres")
    ])
      .then(([alertRows, centreRows]) => {
        if (!active) return;
        setAlerts(alertRows);
        setCentres(centreRows);
        setError(null);
      })
      .catch(() => {
        if (!active) return;
        setError("Live public updates could not be loaded. You can still submit a report for assistance.");
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, []);

  return (
    <>
      <section className="public-live-updates" aria-label="Verified public alerts">
        <div className="section-head">
          <div>
            <div className="eyebrow dark"><span /> Public alerts</div>
            <h2>Verified alerts from coordinators</h2>
          </div>
        </div>
        {loading ? <p className="muted">Loading alerts…</p> : null}
        {error ? <p className="form-error" role="alert">{error}</p> : null}
        {!loading && !error && alerts.length === 0 ? (
          <p className="muted">No published public alerts right now.</p>
        ) : null}
        <div className="value-grid">
          {alerts.slice(0, 6).map((alert) => (
            <article key={alert.id}>
              <i><Icon name="alert" /></i>
              <h3>{alert.title}</h3>
              <p>{alert.body}</p>
              <p className="muted"><small>Published: {formatDate(alert.publishedAt ?? alert.createdAt)}</small></p>
            </article>
          ))}
        </div>
      </section>

      <section className="public-live-updates" aria-label="Verified service centres">
        <div className="section-head">
          <div>
            <div className="eyebrow dark"><span /> Service centres</div>
            <h2>Approved service centres and support points</h2>
          </div>
        </div>
        {!loading && !error && centres.length === 0 ? (
          <p className="muted">No active service centres are published right now.</p>
        ) : null}
        <div className="value-grid">
          {centres.slice(0, 6).map((centre) => (
            <article key={centre.id}>
              <i><Icon name="pin" /></i>
              <h3>{centre.name}</h3>
              <p>{centre.address}</p>
              <p className="muted"><small>Services: {centre.services || "—"}</small></p>
              <p className="muted"><small>Hours: {centre.openingHours || "—"}</small></p>
            </article>
          ))}
        </div>
      </section>
    </>
  );
}
