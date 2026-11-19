"use client";

import { useCallback, useEffect, useState } from "react";
import { Button, Input, Skeleton, Toast } from "@/components/ui";
import { AuthGuard } from "@/components/AuthGuard";
import { api, errorMessage } from "@/lib/api";
import {
  formatDate,
  formatMetric,
  type KpiAreaBreakdown,
  type KpiReportDto
} from "@/lib/admin";
import { formatMoney } from "@/lib/donations";
import {
  RoleNames,
  hasAnyRole,
  type RoleName
} from "@/lib/roles";
import { useAuth } from "@/lib/auth";

const kpiRoles: RoleName[] = [
  RoleNames.SystemAdmin,
  RoleNames.EmergencyCoordinator,
  RoleNames.PartnerOfficer
];

type Notice = { message: string; tone: "error" | "success" } | null;

const guidPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function parseGuid(value: string, label: string): string | undefined {
  const trimmed = value.trim();
  if (!trimmed) {
    return undefined;
  }

  if (!guidPattern.test(trimmed)) {
    throw new Error(`${label} must be a valid GUID.`);
  }

  return trimmed;
}

function AreaTable({ rows }: { rows: KpiAreaBreakdown[] }) {
  if (rows.length === 0) {
    return (
      <p className="metric__label">
        No area breakdown yet. It appears once the owning module publishes its
        schema.
      </p>
    );
  }

  return (
    <div className="table-wrap">
      <table className="table">
        <thead>
          <tr>
            <th scope="col">Area</th>
            <th scope="col">Count</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.areaId}>
              <td>{row.areaName || row.areaId}</td>
              <td>{row.count}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default function KpiPage() {
  const { user } = useAuth();
  const [from, setFrom] = useState("2026-11-01");
  const [to, setTo] = useState("2026-11-30");
  const [crisisEventId, setCrisisEventId] = useState("");
  const [areaId, setAreaId] = useState("");
  const [report, setReport] = useState<KpiReportDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [notice, setNotice] = useState<Notice>(null);

  const crisisInvalid =
    crisisEventId.trim().length > 0 && !guidPattern.test(crisisEventId.trim());
  const areaInvalid =
    areaId.trim().length > 0 && !guidPattern.test(areaId.trim());

  const load = useCallback(async () => {
    const params = new URLSearchParams({
      from: `${from}T00:00:00Z`,
      to: `${to}T23:59:59Z`
    });

    const crisis = parseGuid(crisisEventId, "Crisis event ID");
    if (crisis) {
      params.set("crisisEventId", crisis);
    }

    const area = parseGuid(areaId, "Area ID");
    if (area) {
      params.set("areaId", area);
    }

    return api<KpiReportDto>(`/api/v1/kpis?${params.toString()}`);
  }, [areaId, crisisEventId, from, to]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);

    void load()
      .then((value) => {
        if (!cancelled) {
          setReport(value);
        }
      })
      .catch((caught: unknown) => {
        if (!cancelled) {
          setNotice({ message: errorMessage(caught), tone: "error" });
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [load]);

  const allowed = user ? hasAnyRole(user.roles, kpiRoles) : false;

  return (
    <AuthGuard allowedRoles={kpiRoles}>
      <div className="page">
        <header className="page__header">
          <div>
            <h1>KPIs</h1>
            <p>Cross-module performance indicators for the selected period.</p>
          </div>
          <Button onClick={() => void load().then(setReport).catch((caught: unknown) => setNotice({ message: errorMessage(caught), tone: "error" }))} disabled={loading}>
            Refresh
          </Button>
        </header>

        {!allowed ? null : (
          <>
            <section className="card">
              <div className="filters">
                <Input
                  label="From"
                  type="date"
                  value={from}
                  onChange={(event) => setFrom(event.target.value)}
                />
                <Input
                  label="To"
                  type="date"
                  value={to}
                  onChange={(event) => setTo(event.target.value)}
                />
                <Input
                  label="Crisis event ID"
                  placeholder="Optional GUID"
                  value={crisisEventId}
                  error={
                    crisisInvalid ? "Enter a GUID or leave the field blank." : undefined
                  }
                  onChange={(event) => setCrisisEventId(event.target.value)}
                />
                <Input
                  label="Area ID"
                  placeholder="Optional GUID"
                  value={areaId}
                  error={areaInvalid ? "Enter a GUID or leave the field blank." : undefined}
                  onChange={(event) => setAreaId(event.target.value)}
                />
              </div>
            </section>

            {report?.isPlaceholder ? (
              <section className="notice">
                Identity and donation aggregates are live.
                {report.pendingSections.length > 0
                  ? ` Still provisional: ${report.pendingSections.join(", ")} — waiting on Members 2–4 to publish their table and column names.`
                  : null}{" "}
                No guessed SQL is used.
              </section>
            ) : null}

            <section className="card-grid" aria-label="KPI summary">
              {loading ? (
                <Skeleton rows={4} />
              ) : (
                <>
                  <div className="card">
                    <div className="metric__label">Unresolved requests</div>
                    <div className="metric__value">
                      {report?.unresolvedRequestCount ?? "—"}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">Average response time</div>
                    <div className="metric__value">
                      {formatMetric(report?.averageResponseTimeHours, " h")}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">Resource utilisation</div>
                    <div className="metric__value">
                      {formatMetric(report?.resourceUtilisationPercent, "%")}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">Reserved / dispatched</div>
                    <div className="metric__value">
                      {report?.reservedResources ?? "—"} / {report?.dispatchedResources ?? "—"}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">Shelter occupancy</div>
                    <div className="metric__value">
                      {formatMetric(report?.shelterOccupancyPercent, "%")}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">Shelters at capacity</div>
                    <div className="metric__value">
                      {report?.sheltersAtCapacity ?? "—"}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">AI override rate</div>
                    <div className="metric__value">
                      {formatMetric(report?.aiOverrideRatePercent, "%")}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">Failed notifications</div>
                    <div className="metric__value">
                      {report?.failedNotifications ?? "—"}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">Dead letters</div>
                    <div className="metric__value">
                      {report?.deadLetterCount ?? "—"}
                    </div>
                  </div>
                </>
              )}
            </section>

            <section className="card-grid" aria-label="Identity and donation KPIs">
              {loading ? (
                <Skeleton rows={3} />
              ) : (
                <>
                  <div className="card">
                    <div className="metric__label">Active / total users</div>
                    <div className="metric__value">
                      {report?.activeUserCount ?? "—"} / {report?.totalUserCount ?? "—"}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">New registrations</div>
                    <div className="metric__value">{report?.newUserCount ?? "—"}</div>
                  </div>
                  <div className="card">
                    <div className="metric__label">Audit events</div>
                    <div className="metric__value">{report?.auditEventCount ?? "—"}</div>
                  </div>
                  <div className="card">
                    <div className="metric__label">Active campaigns</div>
                    <div className="metric__value">
                      {report?.activeCampaignCount ?? "—"}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">Flagged donations</div>
                    <div className="metric__value">
                      {report?.suspiciousDonationCount ?? "—"}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">Flagged allocations</div>
                    <div className="metric__value">
                      {report?.suspiciousAllocationCount ?? "—"}
                    </div>
                  </div>
                </>
              )}
            </section>

            <section className="card">
              <h2>Donations by currency</h2>
              <p className="metric__label">
                Organisation-wide totals for the selected period. Amounts in
                different currencies are never combined.
              </p>
              {loading ? (
                <Skeleton rows={3} />
              ) : report && report.donationTotals.length > 0 ? (
                <div className="table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th scope="col">Currency</th>
                        <th scope="col">Monetary</th>
                        <th scope="col">In-kind value</th>
                        <th scope="col">Allocated</th>
                        <th scope="col">Spent</th>
                        <th scope="col">Donations</th>
                        <th scope="col">Donors</th>
                        <th scope="col">Flagged</th>
                      </tr>
                    </thead>
                    <tbody>
                      {report.donationTotals.map((total) => (
                        <tr key={total.currency}>
                          <td>{total.currency}</td>
                          <td>{formatMoney(total.monetaryTotal, total.currency)}</td>
                          <td>
                            {formatMoney(total.materialEstimatedTotal, total.currency)}
                          </td>
                          <td>{formatMoney(total.allocatedTotal, total.currency)}</td>
                          <td>{formatMoney(total.spentTotal, total.currency)}</td>
                          <td>{total.donationCount}</td>
                          <td>{total.donorCount}</td>
                          <td>{total.suspiciousCount}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <p className="metric__label">No donations in this period yet.</p>
              )}
            </section>

            <section className="card">
              <h2>Accounts by role</h2>
              {loading ? (
                <Skeleton rows={3} />
              ) : report && report.usersByRole.length > 0 ? (
                <div className="table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th scope="col">Role</th>
                        <th scope="col">Accounts</th>
                      </tr>
                    </thead>
                    <tbody>
                      {report.usersByRole.map((row) => (
                        <tr key={row.role}>
                          <td>{row.role}</td>
                          <td>{row.count}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <p className="metric__label">No accounts found.</p>
              )}
            </section>

            <section className="card-grid" aria-label="KPI area breakdowns">
              <div className="card">
                <h2>Requests by area</h2>
                {loading ? (
                  <Skeleton rows={3} />
                ) : (
                  <AreaTable rows={report?.requestsByArea ?? []} />
                )}
              </div>
              <div className="card">
                <h2>Shelters by area</h2>
                {loading ? (
                  <Skeleton rows={3} />
                ) : (
                  <AreaTable rows={report?.sheltersByArea ?? []} />
                )}
              </div>
            </section>

            <section className="card">
              <div className="metric__label">Generated at</div>
              <div>{formatDate(report?.generatedAt)}</div>
            </section>
          </>
        )}
      </div>

      <Toast message={notice?.message ?? null} tone={notice?.tone} onDismiss={() => setNotice(null)} />
    </AuthGuard>
  );
}
