"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import type { Paged, RequestListItem } from "../../../components/cases/casesApi";
import { casesApi } from "../../../components/cases/casesApi";
import { EmptyState, ErrorState, LoadingState } from "../../../components/cases/PageState";
import StatusBadge from "../../../components/cases/StatusBadge";

export default function MyRequestsPage() {
  const [result, setResult] = useState<Paged<RequestListItem> | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const pageSize = 20;

  useEffect(() => {
    setLoading(true);
    casesApi
      .myRequests(page, pageSize)
      .then(setResult)
      .catch((cause: unknown) =>
        setError(cause instanceof Error ? cause.message : "Requests could not be loaded.")
      )
      .finally(() => setLoading(false));
  }, [page]);

  return (
    <div>
      <h1>My requests</h1>
      <p><Link href="/requests/new">Submit a new request</Link></p>
      {loading ? <LoadingState /> : null}
      {error ? <ErrorState message={error} /> : null}
      {!loading && !error && result?.items.length === 0 ? (
        <EmptyState message="You have not submitted any requests yet." />
      ) : null}

      {result && result.items.length ? (
        <>
          <table>
            <thead>
              <tr><th>Reference</th><th>Type</th><th>Urgency</th><th>Status</th><th>Submitted</th></tr>
            </thead>
            <tbody>
              {result.items.map((item) => (
                <tr key={item.id}>
                  <td><Link href={`/requests/${item.id}`}>{item.id.slice(0, 8)}</Link></td>
                  <td>{item.category}</td>
                  <td>{item.urgency}</td>
                  <td><StatusBadge status={item.status} overdue={item.isOverdue} /></td>
                  <td>{new Date(item.submittedAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <div>
            <button type="button" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>
              Previous
            </button>
            <span> Page {result.page} of {Math.max(1, Math.ceil(result.total / result.pageSize))} </span>
            <button
              type="button"
              disabled={page * result.pageSize >= result.total}
              onClick={() => setPage((value) => value + 1)}
            >
              Next
            </button>
          </div>
        </>
      ) : null}
    </div>
  );
}
