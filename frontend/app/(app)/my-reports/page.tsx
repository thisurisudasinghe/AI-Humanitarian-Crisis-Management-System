"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { CitizenRequestCard } from "@/components/safeway/CitizenRequestCard";
import { Icon } from "@/components/safeway/Icon";
import { PortalHeader, PortalState } from "@/components/safeway/PortalPrimitives";
import {
  OPEN_REQUEST_STATUSES,
  RESOLVED_REQUEST_STATUSES,
  caseRequestErrorMessage,
  listMyCaseRequests,
  loadCaseRequestDetails,
  type CaseRequestDetail,
  type CaseRequestListItem
} from "@/lib/crisis";
import { useI18n } from "@/lib/i18n";

type FilterKey = "all" | "open" | "resolved";

export default function MyReportsPage() {
  const { t } = useI18n();
  const [filter, setFilter] = useState<FilterKey>("all");
  const [reports, setReports] = useState<CaseRequestListItem[]>([]);
  const [details, setDetails] = useState<Record<string, CaseRequestDetail | null>>({});
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [refreshToken, setRefreshToken] = useState(0);

  useEffect(() => {
    let active = true;
    if (refreshToken === 0) setLoading(true);
    else setRefreshing(true);

    listMyCaseRequests()
      .then(async (result) => {
        const loadedDetails = await loadCaseRequestDetails(result.items, 12);
        if (!active) return;
        setReports(result.items);
        setDetails(loadedDetails);
        setError(null);
      })
      .catch((cause) => {
        if (active) setError(caseRequestErrorMessage(cause, t("citizenX.tracking.errorTitle"), t));
      })
      .finally(() => {
        if (!active) return;
        setLoading(false);
        setRefreshing(false);
      });

    return () => {
      active = false;
    };
  }, [t, refreshToken]);

  const visibleReports = useMemo(() => {
    if (filter === "open") return reports.filter((report) => OPEN_REQUEST_STATUSES.includes(report.status));
    if (filter === "resolved") return reports.filter((report) => RESOLVED_REQUEST_STATUSES.includes(report.status));
    return reports;
  }, [filter, reports]);

  const filters: { key: FilterKey; label: string; icon: string }[] = [
    { key: "all", label: t("citizenX.tracking.filterAll"), icon: "file" },
    { key: "open", label: t("citizenX.tracking.filterOpen"), icon: "clock" },
    { key: "resolved", label: t("citizenX.tracking.filterResolved"), icon: "checkCircle" }
  ];

  return (
    <>
      <PortalHeader
        eyebrow={t("citizenX.tracking.eyebrow")}
        title={t("citizenX.tracking.title")}
        description={t("citizenX.tracking.description")}
        action={
          <div className="citizen-header-actions">
            <button
              className="btn btn-outline"
              type="button"
              onClick={() => setRefreshToken((value) => value + 1)}
              disabled={loading || refreshing}
              aria-busy={refreshing}
            >
              <Icon name="clock" size={16} />
              {refreshing ? t("citizenX.tracking.refreshing") : t("citizenX.tracking.refresh")}
            </button>
            <Link className="btn btn-primary" href="/report">
              <Icon name="alert" size={16} /> {t("citizenX.tracking.newReport")}
            </Link>
          </div>
        }
      />

      <div className="citizen-filter-row" role="group" aria-label={t("citizenX.tracking.title")}>
        {filters.map((item) => (
          <button
            className={filter === item.key ? "active" : ""}
            type="button"
            key={item.key}
            onClick={() => setFilter(item.key)}
            aria-pressed={filter === item.key}
          >
            <Icon name={item.icon} size={16} /> {item.label}
          </button>
        ))}
      </div>

      {error ? (
        <div className="citizen-error-bar" role="alert">
          <Icon name="info" size={18} />
          <span>{error}</span>
          <button className="btn btn-outline" type="button" onClick={() => setRefreshToken((value) => value + 1)}>
            {t("citizenX.tracking.refresh")}
          </button>
        </div>
      ) : null}

      {loading ? (
        <PortalState kind="loading" title={t("citizenX.tracking.loadingTitle")}>
          {t("citizenX.tracking.loadingBody")}
        </PortalState>
      ) : visibleReports.length === 0 ? (
        <PortalState kind="empty" title={t("citizenX.tracking.emptyTitle")} href="/report" linkLabel={t("citizenX.tracking.newReport")}>
          {error ? t("citizenX.tracking.errorTitle") : t("citizenX.tracking.emptyBody")}
        </PortalState>
      ) : (
        <section className="citizen-request-grid" aria-label={t("citizenX.tracking.title")}>
          {visibleReports.map((report) => (
            <CitizenRequestCard key={report.id} report={report} detail={details[report.id]} />
          ))}
        </section>
      )}
    </>
  );
}
