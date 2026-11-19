"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import { EmptyPanel, IntegrationNotice } from "@/components/safeway/IntegrationNotice";
import { api, errorMessage } from "@/lib/api";
import type { AuditEventDto, KpiReportDto, PagedResult } from "@/lib/admin";
import { useAuth } from "@/lib/auth";
import { formatShortDate } from "@/lib/format";
import { RoleNames, StaffRoles, hasAnyRole } from "@/lib/roles";

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
          setError(errorMessage(caught));
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
          "Unresolved requests",
          kpi ? String(kpi.unresolvedRequestCount) : "—",
          "Live from the Dev 1 API",
          "alert",
          "blue"
        ],
        [
          "Active campaigns",
          kpi ? String(kpi.activeCampaignCount) : "—",
          "Donation campaigns in progress",
          "heart",
          "green"
        ],
        [
          "Audit events",
          kpi ? String(kpi.auditEventCount) : "—",
          "Append-only governance trail",
          "shield",
          "coral"
        ],
        [
          "New registrations",
          kpi ? String(kpi.newUserCount) : "—",
          "Accounts created this period",
          "chart",
          "amber"
        ]
      ]
    : [
        ["Unresolved requests", "—", "Requires a KPI role", "alert", "blue"],
        ["Active campaigns", "—", "Requires a KPI role", "heart", "green"],
        ["Audit events", "—", "Requires a KPI role", "shield", "coral"],
        ["New registrations", "—", "Requires a KPI role", "chart", "amber"]
      ];

  return (
    <AuthGuard allowedRoles={StaffRoles}>
      <div className="ops-title">
        <div>
          <p>OPERATIONS OVERVIEW</p>
          <h1>Response command centre</h1>
          <span>
            {kpi
              ? `KPI period from ${formatShortDate(kpi.from)} to ${formatShortDate(kpi.to)}`
              : "Live operational metrics appear as modules are connected"}
          </span>
        </div>
        <div>
          <button
            className="btn btn-outline"
            type="button"
            disabled
            title="Brief export will be provided by the reporting module"
          >
            <Icon name="file" /> Export brief
          </button>
          <Link className="btn btn-primary" href="/operations/crises">
            <Icon name="alert" /> Incident queue
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

      <IntegrationNotice title="Crisis, shelter and resource metrics are not connected" owner="Dev 2 and Dev 4">
        Only identity, donation and governance aggregates are live. Operational cards show a dash
        rather than invented counts.
      </IntegrationNotice>

      <div className="ops-grid-main">
        <section className="ops-card crisis-table-card">
          <div className="card-heading">
            <div>
              <h3>Active crisis response</h3>
              <p>Priority incidents requiring coordination</p>
            </div>
            <div className="table-actions">
              <Link href="/operations/crises">
                View all <Icon name="arrow" size={16} />
              </Link>
            </div>
          </div>
          <div className="crisis-table">
            <div className="table-head">
              <span>INCIDENT</span>
              <span>SEVERITY</span>
              <span>ASSIGNED</span>
              <span>PROGRESS</span>
              <span>UPDATED</span>
              <span />
            </div>
            <div className="table-empty">
              <EmptyPanel title="No crisis data source yet" owner="Dev 2 — crisis & case management">
                Incident rows will render from the crisis and case endpoints. No sample incidents
                are shown so the overview cannot be mistaken for live data.
              </EmptyPanel>
            </div>
          </div>
        </section>

        <aside className="ops-card live-map-card">
          <div className="card-heading">
            <div>
              <h3>Live situation map</h3>
              <p>Active incidents &amp; resources</p>
            </div>
            <Link href="/operations/map" aria-label="Open live map">
              <Icon name="arrow" size={16} />
            </Link>
          </div>
          <div className="mini-map">
            <div className="map-roads" />
            <div className="map-overlay">
              Map data not connected
              <small>Incident and resource locations will come from Dev 2 and Dev 4.</small>
            </div>
          </div>
          <div className="map-legend">
            <span>
              <i className="critical" />
              Incident
            </span>
            <span>
              <i />
              Shelter
            </span>
            <span>
              <i className="green" />
              Resource
            </span>
          </div>
        </aside>
      </div>

      <div className="lower-grid">
        <section className="ops-card">
          <div className="card-heading">
            <div>
              <h3>Resource readiness</h3>
              <p>Availability across active inventory</p>
            </div>
            <Link href="/resources">
              Manage <Icon name="arrow" size={15} />
            </Link>
          </div>
          <EmptyPanel title="Inventory is not connected" owner="Dev 4 — logistics & shelter network">
            Readiness bars will use live stock levels once the logistics API is available.
          </EmptyPanel>
        </section>

        <section className="ops-card">
          <div className="card-heading">
            <div>
              <h3>Recent activity</h3>
              <p>Team and system actions</p>
            </div>
            {admin ? <Link href="/admin/audit">View audit log</Link> : null}
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
              <EmptyPanel title="No audit events yet">Append-only events will appear here.</EmptyPanel>
            )
          ) : (
            <EmptyPanel title="Audit trail requires SystemAdmin">
              The append-only audit viewer is restricted by the AdminOnly policy.
            </EmptyPanel>
          )}
        </section>

        <section className="ops-card ai-brief">
          <div className="ai-brief-head">
            <span>
              <Icon name="brain" />
            </span>
            <i>AI DECISION SUPPORT</i>
          </div>
          <h3>Recommendations are not connected yet</h3>
          <p>
            Risk signals and suggested responses will appear after the Dev 3 decision-support
            service is available. Human approval remains mandatory.
          </p>
          <Link href="/operations/ai">
            Review the integration plan <Icon name="arrow" size={16} />
          </Link>
          <small>
            <Icon name="shield" size={13} /> Human approval is required
          </small>
        </section>
      </div>
    </AuthGuard>
  );
}
