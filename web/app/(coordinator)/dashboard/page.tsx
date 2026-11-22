"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import {
  casesApi,
  type KpiReport,
  type RequestListItem,
  type ResourceType,
  type ShortageProposal
} from "../../../components/cases/casesApi";
import { EmptyState, ErrorState, LoadingState } from "../../../components/cases/PageState";
import StatusBadge from "../../../components/cases/StatusBadge";

export default function CoordinatorDashboardPage() {
  const [kpis, setKpis] = useState<KpiReport | null>(null);
  const [shortage, setShortage] = useState<ShortageProposal | null>(null);
  const [escalated, setEscalated] = useState<RequestListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [shortageError, setShortageError] = useState("");

  useEffect(() => {
    const to = new Date();
    const from = new Date(to.getTime() - 30 * 24 * 60 * 60 * 1000);

    Promise.allSettled([
      casesApi.kpis(from.toISOString(), to.toISOString()),
      casesApi.staffRequests({ page: 1, pageSize: 10, status: "Escalated" }),
      casesApi.resourceTypes()
    ])
      .then(async ([kpiResult, escalatedResult, resourceResult]) => {
        if (kpiResult.status === "fulfilled") setKpis(kpiResult.value);
        if (escalatedResult.status === "fulfilled") setEscalated(escalatedResult.value.items);

        if (resourceResult.status === "fulfilled" && resourceResult.value.length) {
          try {
            setShortage(await casesApi.shortageProposal(resourceResult.value[0].id));
          } catch (cause) {
            setShortage(null);
            setShortageError(
              cause instanceof Error
                ? cause.message
                : "Shortage proposal could not be loaded."
            );
          }
        }

        const failures = [kpiResult, escalatedResult, resourceResult].filter(
          (result) => result.status === "rejected"
        );
        if (failures.length) {
          const reason = (failures[0] as PromiseRejectedResult).reason;
          setError(reason instanceof Error ? reason.message : "Dashboard data could not be loaded.");
        }
      })
      .finally(() => setLoading(false));
  }, []);

  return (
    <div>
      <h1>Coordinator dashboard</h1>
      <p><Link href="/coordinator/requests">Open request queue</Link> · <Link href="/crises">Manage crises</Link></p>
      {loading ? <LoadingState /> : null}
      {error ? <ErrorState message={error} /> : null}
      {shortageError ? <ErrorState message={shortageError} /> : null}

      <div className="grid">
        <section className="panel">
          <h2>Open</h2>
          <p className="muted">Awaiting M1 KPI field; DEV2 does not aggregate KPI data client-side.</p>
        </section>
        <section className="panel">
          <h2>Overdue</h2>
          <p className="muted">Awaiting M1 KPI field; DEV2 does not aggregate KPI data client-side.</p>
        </section>
        <section className="panel">
          <h2>Unresolved</h2>
          <strong>{kpis?.unresolvedRequestCount ?? "—"}</strong>
        </section>
        <section className="panel">
          <h2>Average response</h2>
          <strong>{kpis?.averageResponseTimeHours?.toFixed(1) ?? "—"} h</strong>
        </section>
        <section className="panel">
          <h2>AI override rate</h2>
          <strong>{kpis?.aiOverrideRatePercent?.toFixed(1) ?? "—"}%</strong>
        </section>
        <section className="panel">
          <h2>Shortages</h2>
          <strong>{shortage?.items.length ?? "—"}</strong>
          <p className="muted">
            {shortage ? `${shortage.availableQuantity} units available` : "M4 shortage endpoint unavailable"}
          </p>
        </section>
      </div>

      <section className="panel" aria-labelledby="escalated-heading">
        <h2 id="escalated-heading">Escalated requests</h2>
        {escalated.length === 0 ? <EmptyState message="No escalated requests." /> : null}
        <table>
          <thead><tr><th>Reference</th><th>Type</th><th>Status</th><th>Submitted</th></tr></thead>
          <tbody>
            {escalated.map((item) => (
              <tr key={item.id}>
                <td><Link href={`/coordinator/requests/${item.id}`}>{item.id.slice(0, 8)}</Link></td>
                <td>{item.category}</td>
                <td><StatusBadge status={item.status} overdue={item.isOverdue} /></td>
                <td>{new Date(item.submittedAt).toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      <section className="panel" aria-labelledby="workload-heading">
        <h2 id="workload-heading">Workload by assignee</h2>
        <p className="muted">
          External dependency: Member 1 must expose an assignee workload field on the KPI endpoint.
          DEV2 does not aggregate KPI or queue data client-side.
        </p>
      </section>
    </div>
  );
}
