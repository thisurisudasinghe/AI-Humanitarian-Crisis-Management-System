"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import type { Paged, RequestListItem } from "../../../components/cases/casesApi";
import { casesApi } from "../../../components/cases/casesApi";
import { EmptyState, ErrorState, LoadingState } from "../../../components/cases/PageState";
import QueueFilters, { EMPTY_FILTERS, type QueueFilterValues } from "../../../components/cases/QueueFilters";
import StatusBadge from "../../../components/cases/StatusBadge";

export default function CoordinatorRequestsPage() {
  const [filters, setFilters] = useState<QueueFilterValues>(EMPTY_FILTERS);
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<Paged<RequestListItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const pageSize = 20;

  useEffect(() => {
    setLoading(true);
    setError("");
    casesApi
      .staffRequests({
        page,
        pageSize,
        status: filters.status || undefined,
        urgency: filters.urgency || undefined,
        category: filters.category || undefined,
        areaId: filters.areaId || undefined,
        assignedTo: filters.assignedTo || undefined,
        overdue: filters.overdue || undefined,
        q: filters.q || undefined,
        sortBy: filters.sortBy,
        sortDescending: filters.sortDescending
      })
      .then(setResult)
      .catch((cause: unknown) =>
        setError(cause instanceof Error ? cause.message : "Queue could not be loaded.")
      )
      .finally(() => setLoading(false));
  }, [filters, page]);

  return (
    <div>
      <h1>Request queue</h1>
      <QueueFilters value={filters} onChange={(next) => { setFilters(next); setPage(1); }} />
      {loading ? <LoadingState /> : null}
      {error ? <ErrorState message={error} /> : null}
      {!loading && !error && result?.items.length === 0 ? (
        <EmptyState message="No requests match these filters." />
      ) : null}

      {result && result.items.length ? (
        <>
          <table>
            <thead>
              <tr>
                <th>Reference</th><th>Type</th><th>Urgency</th><th>Status</th>
                <th>SLA</th><th>Submitted</th>
              </tr>
            </thead>
            <tbody>
              {result.items.map((item) => (
                <tr key={item.id}>
                  <td><Link href={`/requests/${item.id}`}>{item.id.slice(0, 8)}</Link></td>
                  <td>{item.category}</td>
                  <td>{item.urgency}</td>
                  <td><StatusBadge status={item.status} /></td>
                  <td>{item.slaAgeHours} h{item.isOverdue ? " · overdue" : ""}</td>
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
