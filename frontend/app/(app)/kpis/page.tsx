"use client";

import { useCallback, useEffect, useState } from "react";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { Button, Input, Skeleton, Toast } from "@/components/ui";
import { AuthGuard } from "@/components/AuthGuard";
import { api, apiBlob } from "@/lib/api";
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
import { useT } from "@/lib/i18n";
import { currentMonthRange } from "@/lib/dates";
import { useDebouncedValue } from "@/lib/useDebouncedValue";

const kpiRoles: RoleName[] = [
  RoleNames.SystemAdmin,
  RoleNames.EmergencyCoordinator,
  RoleNames.PartnerOfficer
];

type Notice = { message: string; tone: "error" | "success" } | null;

const guidPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function parseGuid(value: string, invalidMessage: string): string | undefined {
  const trimmed = value.trim();
  if (!trimmed) {
    return undefined;
  }

  if (!guidPattern.test(trimmed)) {
    throw new Error(invalidMessage);
  }

  return trimmed;
}

function AreaTable({ rows }: { rows: KpiAreaBreakdown[] }) {
  const t = useT();
  if (rows.length === 0) {
    return (
      <p className="metric__label">
        {t("kpis.noAreaData")}
      </p>
    );
  }

  return (
    <div className="table-wrap">
      <table className="table">
        <thead>
          <tr>
            <th scope="col">{t("kpis.area")}</th>
            <th scope="col">{t("kpis.count")}</th>
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
  const t = useT();
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [crisisEventId, setCrisisEventId] = useState("");
  const [areaId, setAreaId] = useState("");
  const [report, setReport] = useState<KpiReportDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [exporting, setExporting] = useState(false);
  const [notice, setNotice] = useState<Notice>(null);

  const debouncedCrisisEventId = useDebouncedValue(crisisEventId);
  const debouncedAreaId = useDebouncedValue(areaId);

  useEffect(() => {
    const range = currentMonthRange();
    setFrom(range.from);
    setTo(range.to);
  }, []);

  const crisisInvalid =
    crisisEventId.trim().length > 0 && !guidPattern.test(crisisEventId.trim());
  const areaInvalid =
    areaId.trim().length > 0 && !guidPattern.test(areaId.trim());

  const buildParams = useCallback((): URLSearchParams | null => {
    if (!from || !to) {
      return null;
    }

    const params = new URLSearchParams({
      from: `${from}T00:00:00Z`,
      to: `${to}T23:59:59Z`
    });

    const crisis = parseGuid(debouncedCrisisEventId, t("kpis.invalidGuid", { label: t("kpis.crisisEventId") }));
    if (crisis) {
      params.set("crisisEventId", crisis);
    }

    const area = parseGuid(debouncedAreaId, t("kpis.invalidGuid", { label: t("kpis.areaId") }));
    if (area) {
      params.set("areaId", area);
    }

    return params;
  }, [debouncedAreaId, debouncedCrisisEventId, from, to, t]);

  const load = useCallback(async (): Promise<KpiReportDto | null> => {
    const params = buildParams();

    if (!params) {
      return null;
    }

    return api<KpiReportDto>(`/api/v1/kpis?${params.toString()}`);
  }, [buildParams]);

  async function handleExport() {
    const params = buildParams();

    if (!params) {
      return;
    }

    setExporting(true);

    try {
      const { blob, fileName } = await apiBlob(
        `/api/v1/kpis/export?${params.toString()}`
      );

      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = fileName ?? "kpi-report.csv";
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);

      setNotice({ message: t("kpis.downloadSuccess"), tone: "success" });
    } catch (caught) {
      setNotice({ message: localizedErrorMessage(caught, t), tone: "error" });
    } finally {
      setExporting(false);
    }
  }

  useEffect(() => {
    let cancelled = false;
    setLoading(true);

    void load()
      .then((value) => {
        if (!cancelled && value) {
          setReport(value);
        }
      })
      .catch((caught: unknown) => {
        if (!cancelled) {
          setNotice({ message: localizedErrorMessage(caught, t), tone: "error" });
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
            <h1>{t("kpis.title")}</h1>
            <p>{t("kpis.subtitle")}</p>
          </div>
          <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
            <Button
              variant="secondary"
              onClick={() => void handleExport()}
              disabled={loading || exporting || !report}
            >
              {exporting ? t("kpis.exporting") : t("kpis.exportCsv")}
            </Button>
            <Button onClick={() => void load().then(setReport).catch((caught: unknown) => setNotice({ message: localizedErrorMessage(caught, t), tone: "error" }))} disabled={loading}>
              {t("kpis.refresh")}
            </Button>
          </div>
        </header>

        {!allowed ? null : (
          <>
            <section className="card">
              <div className="filters">
                <Input
                  label={t("kpis.from")}
                  type="date"
                  value={from}
                  onChange={(event) => setFrom(event.target.value)}
                />
                <Input
                  label={t("kpis.to")}
                  type="date"
                  value={to}
                  onChange={(event) => setTo(event.target.value)}
                />
                <Input
                  label={t("kpis.crisisEventId")}
                  placeholder={t("kpis.optionalGuid")}
                  value={crisisEventId}
                  error={
                    crisisInvalid ? t("kpis.guidError") : undefined
                  }
                  onChange={(event) => setCrisisEventId(event.target.value)}
                />
                <Input
                  label={t("kpis.areaId")}
                  placeholder={t("kpis.optionalGuid")}
                  value={areaId}
                  error={areaInvalid ? t("kpis.guidError") : undefined}
                  onChange={(event) => setAreaId(event.target.value)}
                />
              </div>
            </section>

            {report?.isPlaceholder ? (
              <section className="notice">
                {t("kpis.placeholderNotice")}
              </section>
            ) : null}

            <section className="card-grid" aria-label={t("kpis.summaryAriaLabel")}>
              {loading ? (
                <Skeleton rows={4} />
              ) : (
                <>
                  <div className="card">
                    <div className="metric__label">{t("kpis.unresolvedRequests")}</div>
                    <div className="metric__value">
                      {report?.unresolvedRequestCount ?? "—"}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">{t("kpis.averageResponseTime")}</div>
                    <div className="metric__value">
                      {formatMetric(report?.averageResponseTimeHours, " h")}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">{t("kpis.resourceUtilisation")}</div>
                    <div className="metric__value">
                      {formatMetric(report?.resourceUtilisationPercent, "%")}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">{t("kpis.reservedDispatched")}</div>
                    <div className="metric__value">
                      {report?.reservedResources ?? "—"} / {report?.dispatchedResources ?? "—"}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">{t("kpis.shelterOccupancy")}</div>
                    <div className="metric__value">
                      {formatMetric(report?.shelterOccupancyPercent, "%")}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">{t("kpis.sheltersAtCapacity")}</div>
                    <div className="metric__value">
                      {report?.sheltersAtCapacity ?? "—"}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">{t("kpis.aiOverrideRate")}</div>
                    <div className="metric__value">
                      {formatMetric(report?.aiOverrideRatePercent, "%")}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">{t("kpis.totalNotifications")}</div>
                    <div className="metric__value">
                      {report?.totalNotificationCount ?? "—"}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">{t("kpis.sentNotifications")}</div>
                    <div className="metric__value">
                      {report?.sentNotificationCount ?? "—"}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">{t("kpis.failedNotifications")}</div>
                    <div className="metric__value">
                      {report?.failedNotifications ?? "—"}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">{t("kpis.deadLetters")}</div>
                    <div className="metric__value">
                      {report?.deadLetterCount ?? "—"}
                    </div>
                  </div>
                </>
              )}
            </section>

            <section className="card-grid" aria-label={t("kpis.identityDonationAriaLabel")}>
              {loading ? (
                <Skeleton rows={3} />
              ) : (
                <>
                  <div className="card">
                    <div className="metric__label">{t("kpis.activeTotalUsers")}</div>
                    <div className="metric__value">
                      {report?.activeUserCount ?? "—"} / {report?.totalUserCount ?? "—"}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">{t("kpis.newRegistrations")}</div>
                    <div className="metric__value">{report?.newUserCount ?? "—"}</div>
                  </div>
                  <div className="card">
                    <div className="metric__label">{t("kpis.auditEvents")}</div>
                    <div className="metric__value">{report?.auditEventCount ?? "—"}</div>
                  </div>
                  <div className="card">
                    <div className="metric__label">{t("kpis.activeCampaigns")}</div>
                    <div className="metric__value">
                      {report?.activeCampaignCount ?? "—"}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">{t("kpis.flaggedDonations")}</div>
                    <div className="metric__value">
                      {report?.suspiciousDonationCount ?? "—"}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">{t("kpis.flaggedAllocations")}</div>
                    <div className="metric__value">
                      {report?.suspiciousAllocationCount ?? "—"}
                    </div>
                  </div>
                </>
              )}
            </section>

            <section className="card">
              <h2>{t("kpis.donationsByCurrency")}</h2>
              <p className="metric__label">
                {t("kpis.donationsHint")}
              </p>
              {loading ? (
                <Skeleton rows={3} />
              ) : report && report.donationTotals.length > 0 ? (
                <div className="table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th scope="col">{t("kpis.currency")}</th>
                        <th scope="col">{t("kpis.monetary")}</th>
                        <th scope="col">{t("kpis.inKindValue")}</th>
                        <th scope="col">{t("kpis.allocated")}</th>
                        <th scope="col">{t("kpis.spent")}</th>
                        <th scope="col">{t("kpis.donations")}</th>
                        <th scope="col">{t("kpis.donors")}</th>
                        <th scope="col">{t("kpis.flagged")}</th>
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
                <p className="metric__label">{t("kpis.noDonations")}</p>
              )}
            </section>

            <section className="card">
              <h2>{t("kpis.accountsByRole")}</h2>
              {loading ? (
                <Skeleton rows={3} />
              ) : report && report.usersByRole.length > 0 ? (
                <div className="table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th scope="col">{t("kpis.role")}</th>
                        <th scope="col">{t("kpis.accounts")}</th>
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
                <p className="metric__label">{t("kpis.noAccounts")}</p>
              )}
            </section>

            <section className="card-grid" aria-label={t("kpis.areaAriaLabel")}>
              <div className="card">
                <h2>{t("kpis.requestsByArea")}</h2>
                {loading ? (
                  <Skeleton rows={3} />
                ) : (
                  <AreaTable rows={report?.requestsByArea ?? []} />
                )}
              </div>
              <div className="card">
                <h2>{t("kpis.sheltersByArea")}</h2>
                {loading ? (
                  <Skeleton rows={3} />
                ) : (
                  <AreaTable rows={report?.sheltersByArea ?? []} />
                )}
              </div>
            </section>

            <section className="card">
              <div className="metric__label">{t("kpis.generatedAt")}</div>
              <div>{formatDate(report?.generatedAt)}</div>
            </section>
          </>
        )}
      </div>

      <Toast message={notice?.message ?? null} tone={notice?.tone} onDismiss={() => setNotice(null)} />
    </AuthGuard>
  );
}
