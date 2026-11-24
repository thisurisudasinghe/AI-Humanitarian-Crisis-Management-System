"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { EmptyPanel } from "@/components/safeway/EmptyPanel";
import { Icon } from "@/components/safeway/Icon";
import MapView, { type MapMarker } from "@/components/map/MapView";
import { api } from "@/lib/api";
import {
  type AuditEventDto,
  type KpiReportDto,
  type PagedResult
} from "@/lib/admin";
import { currentMonthRange } from "@/lib/dates";
import { type DonationReportDto } from "@/lib/donations";
import { useAuth } from "@/lib/auth";
import {
  listCrises,
  listStaffCaseRequests,
  type CaseRequestListItem,
  type CrisisRecord,
  type Paged as CasePaged
} from "@/lib/crisis";
import { formatDate, useI18n } from "@/lib/i18n";
import { getRecommendations, type RecommendationDto } from "@/lib/recommendations";
import {
  RoleNames,
  StaffRoles,
  hasAnyRole
} from "@/lib/roles";

const kpiRoles = [
  RoleNames.SystemAdmin,
  RoleNames.EmergencyCoordinator,
  RoleNames.PartnerOfficer
];
const caseRoles = [RoleNames.EmergencyCoordinator, RoleNames.CaseManager];
const volunteerRoles = [
  RoleNames.VolunteerCoordinator,
  RoleNames.EmergencyCoordinator,
  RoleNames.SystemAdmin
];
const donationRoles = [
  RoleNames.SystemAdmin,
  RoleNames.DonorOfficer,
  RoleNames.EmergencyCoordinator,
  RoleNames.PartnerOfficer
];
const allocationRoles = [
  RoleNames.LogisticsManager,
  RoleNames.EmergencyCoordinator,
  RoleNames.SystemAdmin
];

interface VolunteerRow {
  id: string;
  active: boolean;
  verificationStatus: string;
  createdAt?: string;
}

interface AllocationRow {
  id: string;
  status: string;
  allocatedAt?: string;
}

interface ShelterRow {
  id: string;
  name: string;
  capacity: number | string;
  occupancy: number | string;
  freePlaces: number | string;
  lat: number | string;
  lng: number | string;
}

interface ServiceCentreRow {
  id: string;
  name: string;
  address: string;
  lat: number | string;
  lng: number | string;
  openingHours?: string | null;
}

interface AreaRow {
  id: string;
  name: string;
  crisisId: string;
  lat: number | string;
  lng: number | string;
  radiusKm?: number | string | null;
}

type LoadState<T> = {
  value: T | null;
  loading: boolean;
  failed: boolean;
};

type ActivityItem = {
  id: string;
  title: string;
  sourceKey: string;
  at: string;
  href?: string;
};

type ActionItem = {
  key: string;
  labelKey: string;
  sourceKey: string;
  href: string;
  icon: string;
  state: LoadState<unknown>;
  count: number | null;
};

function loadingState<T>(): LoadState<T> {
  return { value: null, loading: true, failed: false };
}

function unavailableState<T>(): LoadState<T> {
  return { value: null, loading: false, failed: true };
}

function finiteNumber(value: unknown): number | null {
  if (value === null || value === undefined) return null;
  if (typeof value === "string" && !value.trim()) return null;
  const parsed = typeof value === "number" ? value : Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function buildMarker(
  id: string,
  lat: number | null,
  lng: number | null,
  label: string,
  type: string,
  description: string
): MapMarker | null {
  if (
    lat === null ||
    lng === null ||
    lat < -90 ||
    lat > 90 ||
    lng < -180 ||
    lng > 180
  ) {
    return null;
  }
  return { id, lat, lng, label, type, description };
}

function crisisTypeKey(type: string): string {
  const normalized = type.toLowerCase().replace(/[\s-]/g, "");
  if (normalized === "flood") return "map.markerTypes.flood";
  if (normalized === "landslide") return "map.markerTypes.landslide";
  if (normalized === "fire") return "map.markerTypes.fire";
  if (normalized.includes("weather")) return "map.markerTypes.severeWeather";
  if (normalized === "drought") return "map.markerTypes.drought";
  return "map.markerTypes.otherCrisis";
}

export default function OperationsPage() {
  const { t, locale } = useI18n();
  const { user } = useAuth();
  const roles = user?.roles ?? [];
  const rolesKey = roles.join("|");

  const canViewKpis = hasAnyRole(roles, kpiRoles);
  const canViewCases = hasAnyRole(roles, caseRoles);
  const canViewVolunteers = hasAnyRole(roles, volunteerRoles);
  const canViewDonations = hasAnyRole(roles, donationRoles);
  const canViewAllocations = hasAnyRole(roles, allocationRoles);
  const isAdmin = roles.includes(RoleNames.SystemAdmin);

  const [kpi, setKpi] = useState<LoadState<KpiReportDto>>(loadingState);
  const [crises, setCrises] = useState<LoadState<CrisisRecord[]>>(loadingState);
  const [requests, setRequests] = useState<LoadState<CasePaged<CaseRequestListItem>>>(loadingState);
  const [volunteers, setVolunteers] = useState<LoadState<VolunteerRow[]>>(loadingState);
  const [donations, setDonations] = useState<LoadState<DonationReportDto>>(loadingState);
  const [allocations, setAllocations] = useState<LoadState<CasePaged<AllocationRow>>>(loadingState);
  const [recommendations, setRecommendations] = useState<LoadState<RecommendationDto[]>>(loadingState);
  const [audit, setAudit] = useState<LoadState<AuditEventDto[]>>(loadingState);
  const [map, setMap] = useState<LoadState<MapMarker[]>>(loadingState);

  useEffect(() => {
    let cancelled = false;

    function load<T>(
      setter: (state: LoadState<T>) => void,
      enabled: boolean,
      run: () => Promise<T>
    ) {
      if (!enabled) {
        setter(unavailableState<T>());
        return;
      }
      setter(loadingState<T>());
      void run()
        .then((value) => {
          if (!cancelled) setter({ value, loading: false, failed: false });
        })
        .catch(() => {
          if (!cancelled) setter(unavailableState<T>());
        });
    }

    load(
      setKpi,
      canViewKpis,
      async () => {
        const { from, to } = currentMonthRange();
        const query = new URLSearchParams({ from, to });
        return api<KpiReportDto>(`/api/v1/kpis?${query.toString()}`);
      }
    );
    load(setCrises, canViewCases, () => listCrises());
    load(
      setRequests,
      canViewCases,
      () => listStaffCaseRequests({ status: "Submitted", pageSize: 1 })
    );
    load(setVolunteers, canViewVolunteers, () => api<VolunteerRow[]>("/api/v1/volunteers"));
    load(
      setDonations,
      canViewDonations,
      async () => {
        const { from, to } = currentMonthRange();
        const query = new URLSearchParams({ from, to });
        return api<DonationReportDto>(`/api/v1/donations/report?${query.toString()}`);
      }
    );
    load(
      setAllocations,
      canViewAllocations,
      () => api<CasePaged<AllocationRow>>("/api/v1/allocations")
    );
    load(
      setRecommendations,
      roles.length > 0,
      () => getRecommendations()
    );
    load(
      setAudit,
      isAdmin,
      async () => {
        const page = await api<PagedResult<AuditEventDto>>(
          "/api/v1/audit-events?page=1&pageSize=4"
        );
        return page.items;
      }
    );

    return () => {
      cancelled = true;
    };
  }, [canViewAllocations, canViewCases, canViewDonations, canViewKpis, canViewVolunteers, isAdmin, rolesKey]);

  useEffect(() => {
    let cancelled = false;
    setMap(loadingState<MapMarker[]>());

    async function loadMap() {
      let crisisNames = new Map<string, string>();
      try {
        const rows = await listCrises();
        crisisNames = new Map(rows.map((row) => [row.id, row.name]));
      } catch {
        // Area markers remain useful without crisis names.
      }

      const tasks = [
        {
          key: "shelters",
          run: async () => {
            const rows = await api<ShelterRow[]>("/api/v1/shelters/public");
            return rows
              .map((row) =>
                buildMarker(
                  row.id,
                  finiteNumber(row.lat),
                  finiteNumber(row.lng),
                  row.name,
                  "shelter",
                  t("operations.map.shelterApproved", {
                    free: String(row.freePlaces),
                    capacity: String(row.capacity)
                  })
                )
              )
              .filter((marker): marker is MapMarker => marker !== null);
          }
        },
        {
          key: "centres",
          run: async () => {
            const rows = await api<ServiceCentreRow[]>("/api/v1/public/service-centres");
            return rows
              .map((row) =>
                buildMarker(
                  row.id,
                  finiteNumber(row.lat),
                  finiteNumber(row.lng),
                  row.name,
                  "service-centre",
                  [row.address, row.openingHours].filter(Boolean).join(" • ")
                )
              )
              .filter((marker): marker is MapMarker => marker !== null);
          }
        },
        {
          key: "areas",
          run: async () => {
            const rows = await api<AreaRow[]>("/api/v1/operations/areas");
            return rows
              .map((row) =>
                buildMarker(
                  row.id,
                  finiteNumber(row.lat),
                  finiteNumber(row.lng),
                  row.name,
                  "area",
                  t("operations.map.areaDetail", {
                    radius: row.radiusKm
                      ? t("operations.map.areaRadiusPart", { radius: String(row.radiusKm) })
                      : "",
                    crisis: crisisNames.get(row.crisisId) ?? row.crisisId
                  })
                )
              )
              .filter((marker): marker is MapMarker => marker !== null);
          }
        }
      ];

      const results = await Promise.allSettled(tasks.map((task) => task.run()));
      if (cancelled) return;
      const markers: MapMarker[] = [];
      let failed = false;
      results.forEach((result) => {
        if (result.status === "fulfilled") markers.push(...result.value);
        else failed = true;
      });
      setMap({ value: markers, loading: false, failed });
    }

    void loadMap();
    return () => {
      cancelled = true;
    };
  }, [t]);

  function numberLabel(state: LoadState<unknown>, value: number | null | undefined): string {
    if (state.loading) return "…";
    if (state.failed || value === null || value === undefined) {
      return t("opsX.common.unavailable");
    }
    return String(value);
  }

  const kpiReport = kpi.value && !kpi.value.isPlaceholder ? kpi.value : null;

  const metrics = [
    {
      key: "requests",
      label: t("operations.overview.metricUnresolvedRequests"),
      meta: t("opsX.overview.sourceKpis"),
      icon: "alert",
      tone: "blue",
      display: numberLabel(kpi, kpiReport?.unresolvedRequestCount)
    },
    {
      key: "campaigns",
      label: t("operations.overview.metricActiveCampaigns"),
      meta: t("opsX.overview.sourceCampaigns"),
      icon: "heart",
      tone: "green",
      display: numberLabel(kpi, kpiReport?.activeCampaignCount)
    },
    {
      key: "audit",
      label: t("operations.overview.metricAuditEvents"),
      meta: t("opsX.overview.sourceAudit"),
      icon: "shield",
      tone: "coral",
      display: numberLabel(kpi, kpiReport?.auditEventCount)
    },
    {
      key: "registrations",
      label: t("operations.overview.metricNewRegistrations"),
      meta: t("opsX.overview.sourceKpis"),
      icon: "chart",
      tone: "amber",
      display: numberLabel(kpi, kpiReport?.newUserCount)
    }
  ];

  const actions: ActionItem[] = [];
  if (canViewCases) {
    actions.push({
      key: "requests",
      labelKey: "opsX.overview.actionPendingRequests",
      sourceKey: "opsX.overview.sourceRequests",
      href: "/operations/crises?status=Submitted",
      icon: "clipboard",
      state: requests,
      count: requests.value?.total ?? null
    });
  }
  if (canViewVolunteers) {
    actions.push({
      key: "volunteers",
      labelKey: "opsX.overview.actionVolunteerVerification",
      sourceKey: "opsX.overview.sourceVolunteers",
      href: "/volunteers",
      icon: "users",
      state: volunteers,
      count:
        volunteers.value === null
          ? null
          : volunteers.value.filter(
              (row) => row.active && row.verificationStatus.toLowerCase() !== "verified"
            ).length
    });
  }
  if (canViewDonations) {
    actions.push({
      key: "donations",
      labelKey: "opsX.overview.actionDonationReview",
      sourceKey: "opsX.overview.sourceDonations",
      href: "/donations",
      icon: "heart",
      state: donations,
      count: donations.value?.suspiciousDonationCount ?? null
    });
  }
  if (canViewAllocations) {
    actions.push({
      key: "allocations",
      labelKey: "opsX.overview.actionResourceAllocations",
      sourceKey: "opsX.overview.sourceResources",
      href: "/resources",
      icon: "box",
      state: allocations,
      count:
        allocations.value === null
          ? null
          : allocations.value.items.filter((row) => row.status === "Allocated").length
    });
  }
  actions.push({
    key: "recommendations",
    labelKey: "opsX.overview.actionAiRecommendations",
    sourceKey: "opsX.overview.sourceRecommendations",
    href: "/operations/ai",
    icon: "brain",
    state: recommendations,
    count: recommendations.value?.length ?? null
  });

  const activeCrises = (crises.value ?? [])
    .filter((crisis) => crisis.status === "Active")
    .slice(0, 5);

  const activity: ActivityItem[] = [];
  if (audit.value) {
    audit.value.forEach((event) =>
      activity.push({
        id: `audit-${event.id}`,
        title: `${event.action} · ${event.entityType}`,
        sourceKey: "opsX.overview.sourceAudit",
        at: event.createdAt,
        href: "/admin/audit"
      })
    );
  }
  if (crises.value) {
    crises.value.forEach((crisis) =>
      activity.push({
        id: `crisis-${crisis.id}`,
        title: crisis.name,
        sourceKey: "opsX.overview.crisisTitle",
        at: crisis.startedAt,
        href: "/operations/crises"
      })
    );
  }
  if (requests.value) {
    requests.value.items.forEach((request) =>
      activity.push({
        id: `request-${request.id}`,
        title: t(`citizenX.category.${request.category}`, {
          fallback: request.category
        }),
        sourceKey: "opsX.overview.sourceRequests",
        at: request.submittedAt,
        href: "/operations/crises"
      })
    );
  }
  if (recommendations.value) {
    recommendations.value.slice(0, 3).forEach((item) =>
      activity.push({
        id: `recommendation-${item.id}`,
        title: item.type,
        sourceKey: "opsX.overview.sourceRecommendations",
        at: item.createdAt,
        href: "/operations/ai"
      })
    );
  }
  const recentActivity = activity
    .filter((item) => !Number.isNaN(Date.parse(item.at)))
    .sort((left, right) => Date.parse(right.at) - Date.parse(left.at))
    .slice(0, 5);

  return (
    <AuthGuard allowedRoles={StaffRoles}>
      <div className="overview-page">
        <header className="overview-header">
          <div>
            <h1>{t("operations.overview.title")}</h1>
            <p>{t("opsX.overview.subtitle")}</p>
            {kpiReport ? (
              <small>
                {t("opsX.overview.kpiPeriod", {
                  from: formatDate(kpiReport.from, locale),
                  to: formatDate(kpiReport.to, locale)
                })}
              </small>
            ) : null}
          </div>
          <Link className="btn btn-outline" href="/reports">
            <Icon name="chart" size={16} /> {t("opsX.nav.reportsKpis")}
          </Link>
        </header>

        <section aria-labelledby="overview-metrics-heading" className="overview-section">
          <div className="overview-section__heading">
            <h2 id="overview-metrics-heading">{t("opsX.overview.kpiSection")}</h2>
          </div>
          <div className="metric-grid">
            {metrics.map((metric) => (
              <article className={`metric ${metric.tone}`} key={metric.key}>
                <div>
                  <span>{metric.label}</span>
                  <b>{metric.display}</b>
                  <small>{metric.meta}</small>
                </div>
                <i>
                  <Icon name={metric.icon} />
                </i>
              </article>
            ))}
          </div>
        </section>

        <div className="overview-columns">
          <section aria-labelledby="overview-actions-heading" className="ops-card overview-card">
            <div className="card-heading">
              <div>
                <h2 id="overview-actions-heading">{t("opsX.overview.actionsTitle")}</h2>
                <p>{t("opsX.overview.actionsSubtitle")}</p>
              </div>
            </div>
            {actions.length === 0 ? (
              <EmptyPanel title={t("opsX.overview.actionsEmpty")}>
                {t("opsX.overview.actionsEmptyBody")}
              </EmptyPanel>
            ) : (
              <div className="overview-action-list">
                {actions.map((action) => (
                  <Link className="overview-action" href={action.href} key={action.key}>
                    <span className="overview-action__icon">
                      <Icon name={action.icon} size={18} />
                    </span>
                    <span className="overview-action__copy">
                      <b>{t(action.labelKey)}</b>
                      <small>{t(action.sourceKey)}</small>
                    </span>
                    <strong>{numberLabel(action.state, action.count)}</strong>
                    <Icon name="arrow" size={16} />
                  </Link>
                ))}
              </div>
            )}
          </section>

          <section aria-labelledby="overview-crises-heading" className="ops-card overview-card">
            <div className="card-heading">
              <div>
                <h2 id="overview-crises-heading">{t("opsX.overview.crisisTitle")}</h2>
                <p>{t("opsX.overview.crisisSubtitle")}</p>
              </div>
              <Link href="/operations/crises">
                {t("operations.overview.viewAll")} <Icon name="arrow" size={15} />
              </Link>
            </div>
            {!canViewCases || crises.failed ? (
              <EmptyPanel title={t("opsX.overview.crisisEmpty")}>
                {crises.failed
                  ? t("opsX.common.unavailable")
                  : t("opsX.overview.crisisEmptyBody")}
              </EmptyPanel>
            ) : crises.loading ? (
              <p className="overview-loading">{t("opsX.common.loading")}</p>
            ) : activeCrises.length === 0 ? (
              <EmptyPanel title={t("opsX.overview.crisisEmpty")}>
                {t("opsX.overview.crisisEmptyBody")}
              </EmptyPanel>
            ) : (
              <div className="crisis-table overview-crisis-table">
                <div className="table-head">
                  <span>{t("operations.shared.incident")}</span>
                  <span>{t("operations.shared.incidentType")}</span>
                  <span>{t("opsX.overview.started")}</span>
                  <span>{t("opsX.common.status")}</span>
                  <span />
                </div>
                {activeCrises.map((crisis) => (
                  <div className="table-row" key={crisis.id}>
                    <span>
                      <b>{crisis.name}</b>
                      {crisis.description ? <small>{crisis.description}</small> : null}
                    </span>
                    <span>{t(crisisTypeKey(crisis.type))}</span>
                    <span>{formatDate(crisis.startedAt, locale)}</span>
                    <span>{crisis.status}</span>
                    <span>
                      <Link
                        href={`/operations/crises/${crisis.id}`}
                        aria-label={`${t("opsX.common.view")} ${crisis.name}`}
                      >
                        <Icon name="arrow" size={15} />
                      </Link>
                    </span>
                  </div>
                ))}
              </div>
            )}
          </section>
        </div>

        <div className="overview-columns overview-columns--lower">
          <section aria-labelledby="overview-map-heading" className="ops-card overview-card">
            <div className="card-heading">
              <div>
                <h2 id="overview-map-heading">{t("opsX.overview.mapTitle")}</h2>
                <p>{t("opsX.overview.mapSubtitle")}</p>
              </div>
              <Link href="/operations/map">
                {t("opsX.overview.openFullMap")} <Icon name="arrow" size={15} />
              </Link>
            </div>
            <div className="overview-map">
              <MapView markers={map.value ?? []} height={280} showLegend={false} />
            </div>
            {map.failed ? (
              <p className="overview-map__note">
                <Icon name="info" size={14} /> {t("opsX.overview.mapErrors")}
              </p>
            ) : null}
          </section>

          <section aria-labelledby="overview-activity-heading" className="ops-card overview-card">
            <div className="card-heading">
              <div>
                <h2 id="overview-activity-heading">{t("opsX.overview.activityTitle")}</h2>
                <p>{t("opsX.overview.activitySubtitle")}</p>
              </div>
              {isAdmin ? (
                <Link href="/admin/audit">
                  {t("operations.shared.viewAuditLog")} <Icon name="arrow" size={15} />
                </Link>
              ) : null}
            </div>
            {audit.loading && recentActivity.length === 0 ? (
              <p className="overview-loading">{t("opsX.common.loading")}</p>
            ) : recentActivity.length === 0 ? (
              <EmptyPanel title={t("opsX.overview.activityEmpty")}>
                {t("opsX.overview.activityEmptyBody")}
              </EmptyPanel>
            ) : (
              <div className="activity-list">
                {recentActivity.map((item) => {
                  const content = (
                    <>
                      <i className="green">
                        <Icon name="clock" size={14} />
                      </i>
                      <span>
                        <b>{item.title}</b>
                        <small>
                          {t(item.sourceKey)} · {formatDate(item.at, locale)}
                        </small>
                      </span>
                    </>
                  );
                  return item.href ? (
                    <Link href={item.href} key={item.id}>
                      {content}
                    </Link>
                  ) : (
                    <div key={item.id}>{content}</div>
                  );
                })}
              </div>
            )}
          </section>
        </div>
      </div>
    </AuthGuard>
  );
}
