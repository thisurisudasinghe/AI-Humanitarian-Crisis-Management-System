"use client";

import { useCallback, useEffect, useState } from "react";
import { Button, Input, Skeleton, Toast } from "@/components/ui";
import { AuthGuard } from "@/components/AuthGuard";
import { api, errorMessage } from "@/lib/api";
import {
  formatDate,
  formatMetric,
  type KpiReportDto
} from "@/lib/admin";
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

export default function KpiPage() {
  const { user } = useAuth();
  const [from, setFrom] = useState("2026-11-01");
  const [to, setTo] = useState("2026-11-30");
  const [report, setReport] = useState<KpiReportDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [notice, setNotice] = useState<Notice>(null);

  const load = useCallback(async () => {
    const params = new URLSearchParams({
      from: `${from}T00:00:00Z`,
      to: `${to}T23:59:59Z`
    });

    return api<KpiReportDto>(`/api/v1/kpis?${params.toString()}`);
  }, [from, to]);

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
              </div>
            </section>

            {report?.isPlaceholder ? (
              <section className="notice">
                Aggregates are provisional until Members 2–4 publish their final
                table and column names. No guessed SQL is used.
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
