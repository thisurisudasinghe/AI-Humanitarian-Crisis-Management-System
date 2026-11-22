"use client";

import { useEffect, useState } from "react";
import { casesApi, type PartnerShare } from "../../../../components/cases/casesApi";
import { EmptyState, ErrorState, LoadingState } from "../../../../components/cases/PageState";

export default function PartnerCasesPage() {
  const [shares, setShares] = useState<PartnerShare[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    casesApi
      .partnerShares()
      .then(setShares)
      .catch((cause: unknown) =>
        setError(cause instanceof Error ? cause.message : "Shared cases could not be loaded.")
      )
      .finally(() => setLoading(false));
  }, []);

  return (
    <div>
      <h1>Cases shared with your organisation</h1>
      <p className="muted">
        This list is scoped by the organisation in your token and contains only active shares.
      </p>
      {loading ? <LoadingState /> : null}
      {error ? <ErrorState message={error} /> : null}
      {!loading && !error && shares.length === 0 ? (
        <EmptyState message="No cases are currently shared with your organisation." />
      ) : null}
      {!loading && !error && shares.length > 0 ? (
        <table>
          <thead>
            <tr>
              <th scope="col">Request</th>
              <th scope="col">Category</th>
              <th scope="col">Status</th>
              <th scope="col">Shared</th>
            </tr>
          </thead>
          <tbody>
            {shares.map((share) => (
              <tr key={share.id}>
                <td>{share.requestId.slice(0, 8)}…</td>
                <td>{share.category ?? "—"}</td>
                <td>{share.status ?? "Unavailable"}</td>
                <td>{new Date(share.sharedAt).toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
    </div>
  );
}
