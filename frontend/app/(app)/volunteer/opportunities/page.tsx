"use client";

import { useEffect, useMemo, useState } from "react";
import { Icon } from "@/components/safeway/Icon";
import { PortalState } from "@/components/safeway/PortalPrimitives";
import { VolunteerPage } from "@/components/safeway/VolunteerPage";
import { VolunteerTaskCard } from "@/components/safeway/VolunteerTaskCard";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useI18n } from "@/lib/i18n";
import { getVolunteerPortal, type VolunteerTask } from "@/lib/portal";
import { backendTimestamp, parseBackendArray, skillLabel } from "@/lib/volunteer";

type DateFilter = "any" | "today" | "week";

function matchesDateFilter(value: string | null | undefined, filter: DateFilter): boolean {
  if (filter === "any") return true;
  if (!value) return false;
  const taskDate = new Date(backendTimestamp(value) ?? value);
  if (Number.isNaN(taskDate.getTime())) return false;
  const now = new Date();
  if (filter === "today") {
    return taskDate.toDateString() === now.toDateString();
  }
  const end = new Date(now);
  end.setDate(end.getDate() + 7);
  return taskDate >= now && taskDate <= end;
}

export default function VolunteerOpportunitiesPage() {
  const { t } = useI18n();
  const [opportunities, setOpportunities] = useState<VolunteerTask[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [skill, setSkill] = useState("all");
  const [dateFilter, setDateFilter] = useState<DateFilter>("any");
  const [refreshToken, setRefreshToken] = useState(0);

  useEffect(() => {
    let active = true;
    setLoading(true);
    getVolunteerPortal()
      .then((result) => {
        if (!active) return;
        setOpportunities(result.opportunities);
        setError(null);
      })
      .catch((cause) => {
        if (active) setError(localizedErrorMessage(cause, t));
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [refreshToken, t]);

  const availableSkills = useMemo(() => {
    const values = new Set<string>();
    opportunities.forEach((task) => parseBackendArray(task.requiredSkillsJson).forEach((item) => values.add(item)));
    return Array.from(values).sort((a, b) => a.localeCompare(b));
  }, [opportunities]);

  const visible = useMemo(() => {
    const needle = search.trim().toLowerCase();
    return opportunities.filter((task) => {
      const skills = parseBackendArray(task.requiredSkillsJson);
      const haystack = `${task.title} ${skills.join(" ")}`.toLowerCase();
      if (needle && !haystack.includes(needle)) return false;
      if (skill !== "all" && !skills.includes(skill)) return false;
      return matchesDateFilter(task.scheduledAt, dateFilter);
    });
  }, [dateFilter, opportunities, search, skill]);

  return (
    <VolunteerPage
      eyebrowKey="volunteerX.opportunity.detailEyebrow"
      titleKey="volunteerX.opportunity.title"
      descriptionKey="volunteerX.opportunity.description"
      action={
        <button
          className="btn btn-outline"
          type="button"
          onClick={() => setRefreshToken((value) => value + 1)}
          disabled={loading}
          aria-busy={loading}
        >
          <Icon name="clock" size={16} /> {t("volunteerX.shared.refresh")}
        </button>
      }
    >
      {error ? (
        <div className="volunteer-error-bar" role="alert">
          <Icon name="info" size={17} />
          <span>{error}</span>
          <button className="btn btn-primary" type="button" onClick={() => setRefreshToken((value) => value + 1)}>
            {t("volunteerX.shared.retry")}
          </button>
        </div>
      ) : null}

      <section className="volunteer-filters" aria-label={t("volunteerX.opportunity.title")}>
        <div className="volunteer-search">
          <label htmlFor="volunteer-opportunity-search">{t("volunteerX.opportunity.searchLabel")}</label>
          <div>
            <Icon name="search" size={17} />
            <input
              id="volunteer-opportunity-search"
              type="search"
              value={search}
              placeholder={t("volunteerX.opportunity.searchPlaceholder")}
              onChange={(event) => setSearch(event.target.value)}
            />
          </div>
        </div>
        <div>
          <label htmlFor="volunteer-opportunity-skill">{t("volunteerX.opportunity.filterSkillLabel")}</label>
          <select id="volunteer-opportunity-skill" value={skill} onChange={(event) => setSkill(event.target.value)}>
            <option value="all">{t("volunteerX.opportunity.filterAllSkills")}</option>
            {availableSkills.map((value) => (
              <option value={value} key={value}>{skillLabel(t, value)}</option>
            ))}
          </select>
        </div>
        <div>
          <label htmlFor="volunteer-opportunity-date">{t("volunteerX.opportunity.filterDateLabel")}</label>
          <select
            id="volunteer-opportunity-date"
            value={dateFilter}
            onChange={(event) => setDateFilter(event.target.value as DateFilter)}
          >
            <option value="any">{t("volunteerX.opportunity.filterAnyDate")}</option>
            <option value="today">{t("volunteerX.opportunity.filterToday")}</option>
            <option value="week">{t("volunteerX.opportunity.filterWeek")}</option>
          </select>
        </div>
      </section>

      {loading ? (
        <PortalState kind="loading" title={t("volunteerX.opportunity.loadingTitle")}>
          {t("volunteerX.opportunity.loadingBody")}
        </PortalState>
      ) : error ? (
        <PortalState kind="error" title={t("volunteerX.opportunity.errorTitle")}>
          {error}
        </PortalState>
      ) : opportunities.length === 0 ? (
        <PortalState kind="empty" title={t("volunteerX.opportunity.emptyTitle")} href="/volunteer/availability" linkLabel={t("volunteerX.landing.cardAvailabilityCta")}>
          {t("volunteerX.opportunity.emptyBody")}
        </PortalState>
      ) : visible.length === 0 ? (
        <PortalState kind="empty" title={t("volunteerX.opportunity.emptyTitle")}>
          {t("volunteerX.opportunity.emptyBody")}
        </PortalState>
      ) : (
        <>
          <p className="volunteer-results-count">
            {t("volunteerX.opportunity.results", { count: visible.length })}
          </p>
          <div className="volunteer-task-grid">
            {visible.map((task) => (
              <VolunteerTaskCard key={task.id} task={task} mode="opportunity" href={`/volunteer/opportunities/${task.id}`} />
            ))}
          </div>
        </>
      )}

      <div className="volunteer-info-panel" role="note">
        <Icon name="info" size={18} />
        <div>
          <b>{t("volunteerX.opportunity.coordinatorNoteTitle")}</b>
          <p>{t("volunteerX.shared.coordinatorControlled")}</p>
        </div>
      </div>
    </VolunteerPage>
  );
}
