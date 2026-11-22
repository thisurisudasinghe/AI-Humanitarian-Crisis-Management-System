"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { casesApi, type AlertItem } from "../../../components/cases/casesApi";
import { EmptyState, ErrorState, LoadingState } from "../../../components/cases/PageState";

export default function PublicAlertsPage() {
  const [alerts, setAlerts] = useState<AlertItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    casesApi
      .publicAlerts()
      .then((items) => setAlerts(items.filter((item) => item.status === "Published")))
      .catch((cause: unknown) =>
        setError(cause instanceof Error ? cause.message : "Alerts could not be loaded.")
      )
      .finally(() => setLoading(false));
  }, []);

  return (
    <div>
      <h1>Public alerts</h1>
      <section className="panel" aria-labelledby="safety-guidance-heading">
        <h2 id="safety-guidance-heading">Safety guidance</h2>
        <p>Follow official instructions, keep a charged phone nearby, and avoid flooded or damaged roads.</p>
        <p>
          <Link href="/service-centres">Find service centres and shelters</Link> ·{" "}
          <Link href="/requests/new">Request assistance</Link>
        </p>
      </section>

      {loading ? <LoadingState /> : null}
      {error ? <ErrorState message={error} /> : null}
      {!loading && !error && alerts.length === 0 ? (
        <EmptyState message="No published alerts right now." />
      ) : null}
      {!loading && !error ? (
        <ul>
          {alerts.map((alert) => (
            <li key={alert.id} className="panel">
              <h2>{alert.title}</h2>
              <p>{alert.body}</p>
              <p className="muted">
                Published {alert.publishedAt ? new Date(alert.publishedAt).toLocaleString() : "recently"}
              </p>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
