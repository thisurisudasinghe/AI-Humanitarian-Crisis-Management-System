"use client";

import Link from "next/link";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useEffect, useState } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import { EmptyPanel, IntegrationNotice } from "@/components/safeway/IntegrationNotice";
import { api } from "@/lib/api";
import type { AuditEventDto, KpiReportDto, PagedResult } from "@/lib/admin";
import { useAuth } from "@/lib/auth";
import { formatShortDate } from "@/lib/format";
import { RoleNames, StaffRoles, hasAnyRole } from "@/lib/roles";
import { useT } from "@/lib/i18n";

const kpiRoles = [
  RoleNames.SystemAdmin,
  RoleNames.EmergencyCoordinator,
  RoleNames.PartnerOfficer
];

function monthRange(): { from: string; to: string } {
  const now = new Date();
  const start = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1));
  return { from: start.toISOString(), to: now.toISOString() };
}

export default function OperationsPage() {
  const t = useT();
  const { user } = useAuth();
  const [kpi, setKpi] = useState<KpiReportDto | null>(null);
  const [audit, setAudit] = useState<AuditEventDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const canViewKpis = user ? hasAnyRole(user.roles, kpiRoles) : false;
  const admin = user?.roles.includes(RoleNames.SystemAdmin) ?? false;

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      setError(null);

      try {
        if (canViewKpis) {
          const { from, to } = monthRange();
          const params = new URLSearchParams({ from, to });
          const report = await api<KpiReportDto>(`/api/v1/kpis?${params.toString()}`);
          if (!cancelled) {
            setKpi(report);
          }
        }

        if (admin) {
          const events = await api<PagedResult<AuditEventDto>>(
            "/api/v1/audit-events?page=1&pageSize=4"
          );
          if (!cancelled) {
            setAudit(events.items);
          }
        }
      } catch (caught) {
        if (!cancelled) {
          setError(localizedErrorMessage(caught, t));
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [admin, canViewKpis]);

  const metrics: [string, string, string, string, string][] = canViewKpis
    ? [
        [
          t("operations.overview.metricUnresolvedRequests"),
          kpi ? String(kpi.unresolvedRequestCount) : "—",
          t("operations.overview.metricLiveDevApi"),
          "alert",
          "blue"
        ],
        [
          t("operations.overview.metricActiveCampaigns"),
          kpi ? String(kpi.activeCampaignCount) : "—",
          t("operations.overview.metricDonationCampaigns"),
          "heart",
          "green"
        ],
        [
          t("operations.overview.metricAuditEvents"),
          kpi ? String(kpi.auditEventCount) : "—",
          t("operations.overview.metricGovernanceTrail"),
          "shield",
          "coral"
        ],
        [
          t("operations.overview.metricNewRegistrations"),
          kpi ? String(kpi.newUserCount) : "—",
          t("operations.overview.metricAccountsCreated"),
          "chart",
          "amber"
        ]
      ]
    : [
        [t("operations.overview.metricUnresolvedRequests"), "—", t("operations.overview.requiresKpiRole"), "alert", "blue"],
        [t("operations.overview.metricActiveCampaigns"), "—", t("operations.overview.requiresKpiRole"), "heart", "green"],
        [t("operations.overview.metricAuditEvents"), "—", t("operations.overview.requiresKpiRole"), "shield", "coral"],
        [t("operations.overview.metricNewRegistrations"), "—", t("operations.overview.requiresKpiRole"), "chart", "amber"]
      ];

  return (
    <AuthGuard allowedRoles={StaffRoles}>
      <div className="ops-title">
        <div>
          <p>{t("operations.overview.eyebrow")}</p>
          <h1>{t("operations.overview.title")}</h1>
          <span>
            {kpi
              ? t("operations.overview.kpiPeriod", {
                  from: formatShortDate(kpi.from),
                  to: formatShortDate(kpi.to)
                })
              : t("operations.overview.subtitleFallback")}
          </span>
        </div>
        <div>
          <button
            className="btn btn-outline"
            type="button"
            disabled
            title={t("operations.overview.exportTitle")}
          >
            <Icon name="file" /> {t("operations.overview.exportBrief")}
          </button>
          <Link className="btn btn-primary" href="/operations/crises">
            <Icon name="alert" /> {t("operations.crises.incidentQueue")}
          </Link>
        </div>
      </div>

      {error ? (
        <div className="form-error" role="alert">
          {error}
        </div>
      ) : null}

      <div className="metric-grid">
        {metrics.map(([title, value, meta, icon, tone]) => (
          <article className={`metric ${tone}${value === "—" ? " is-pending" : ""}`} key={title}>
            <div>
              <span>{title}</span>
              <b>{loading && canViewKpis ? "…" : value}</b>
              <small>{meta}</small>
            </div>
            <i>
              <Icon name={icon} />
            </i>
            <div className="metric-line">
              <span style={{ width: value === "—" ? "0%" : "68%" }} />
            </div>
          </article>
        ))}
      </div>

      <IntegrationNotice title={t("operations.overview.integrationTitle")} owner={t("operations.overview.integrationOwner")}>
        {t("operations.overview.integrationBody")}
      </IntegrationNotice>

      <div className="ops-grid-main">
        <section className="ops-card crisis-table-card">
          <div className="card-heading">
            <div>
              <h3>{t("operations.overview.activeCrisis")}</h3>
              <p>{t("operations.shared.priorityIncidents")}</p>
            </div>
            <div className="table-actions">
              <Link href="/operations/crises">
                {t("operations.overview.viewAll")} <Icon name="arrow" size={16} />
              </Link>
            </div>
          </div>
          <div className="crisis-table">
            <div className="table-head">
              <span>{t("operations.shared.incident")}</span>
              <span>{t("operations.shared.severity")}</span>
              <span>{t("operations.shared.assigned")}</span>
              <span>{t("operations.shared.progress")}</span>
              <span>{t("operations.shared.updated")}</span>
              <span />
            </div>
            <div className="table-empty">
              <EmptyPanel title={t("operations.overview.noCrisisTitle")} owner={t("operations.overview.noCrisisOwner")}>
                {t("operations.overview.noCrisisBody")}
              </EmptyPanel>
            </div>
          </div>
        </section>

        <aside className="ops-card live-map-card">
          <div className="card-heading">
            <div>
              <h3>{t("operations.overview.liveMap")}</h3>
              <p>{t("operations.overview.activeIncidentsResources")}</p>
            </div>
            <Link href="/operations/map" aria-label={t("operations.overview.openLiveMap")}>
              <Icon name="arrow" size={16} />
            </Link>
          </div>
          <div className="mini-map">
            <div className="map-roads" />
            <div className="map-overlay">
              {t("operations.overview.mapNotConnected")}
              <small>{t("operations.overview.mapNotConnectedDetail")}</small>
            </div>
          </div>
          <div className="map-legend">
            <span>
              <i className="critical" />
              {t("operations.shared.incident")}
            </span>
            <span>
              <i />
              {t("operations.shared.shelter")}
            </span>
            <span>
              <i className="green" />
              {t("operations.shared.resource")}
            </span>
          </div>
        </aside>
      </div>

      <div className="lower-grid">
        <section className="ops-card">
          <div className="card-heading">
            <div>
              <h3>{t("operations.overview.resourceReadiness")}</h3>
              <p>{t("operations.overview.inventoryAvailability")}</p>
            </div>
            <Link href="/resources">
              {t("operations.overview.manage")} <Icon name="arrow" size={15} />
            </Link>
          </div>
          <EmptyPanel title={t("operations.overview.inventoryTitle")} owner={t("operations.overview.inventoryOwner")}>
            {t("operations.overview.inventoryBody")}
          </EmptyPanel>
        </section>

        <section className="ops-card">
          <div className="card-heading">
            <div>
              <h3>{t("operations.overview.recentActivity")}</h3>
              <p>{t("operations.overview.teamActions")}</p>
            </div>
            {admin ? <Link href="/admin/audit">{t("operations.shared.viewAuditLog")}</Link> : null}
          </div>
          {admin ? (
            audit.length > 0 ? (
              <div className="activity-list">
                {audit.map((event) => (
                  <div key={event.id}>
                    <i className="green">
                      <Icon name="check" size={14} />
                    </i>
                    <span>
                      <b>
                        {event.action} · {event.entityType}
                      </b>
                      <small>
                        {formatShortDate(event.createdAt)}
                        {event.actorUserId ? ` · ${event.actorUserId.slice(0, 8)}` : ""}
                      </small>
                    </span>
                  </div>
                ))}
              </div>
            ) : (
              <EmptyPanel title={t("operations.overview.noAuditTitle")}>{t("operations.overview.noAuditBody")}</EmptyPanel>
            )
          ) : (
            <EmptyPanel title={t("operations.overview.auditRequiresAdmin")}>
              {t("operations.overview.auditRestricted")}
            </EmptyPanel>
          )}
        </section>

        <section className="ops-card ai-brief">
          <div className="ai-brief-head">
            <span>
              <Icon name="brain" />
            </span>
            <i>{t("operations.shared.aiDecisionSupport")}</i>
          </div>
          <h3>{t("operations.overview.aiNotConnected")}</h3>
          <p>
            {t("operations.overview.aiNotConnectedBody")}
          </p>
          <Link href="/operations/ai">
            {t("operations.overview.reviewIntegrationPlan")} <Icon name="arrow" size={16} />
          </Link>
          <small>
            <Icon name="shield" size={13} /> {t("operations.shared.humanApprovalRequired")}
          </small>
        </section>
      </div>
    </AuthGuard>
  );
}
