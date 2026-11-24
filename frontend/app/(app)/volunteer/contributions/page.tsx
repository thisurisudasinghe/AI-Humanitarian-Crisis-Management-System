"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { Icon } from "@/components/safeway/Icon";
import { PortalSection, PortalState } from "@/components/safeway/PortalPrimitives";
import { VolunteerPage } from "@/components/safeway/VolunteerPage";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useI18n } from "@/lib/i18n";
import { formatDate } from "@/lib/i18n/core";
import { getMyVolunteerTasks, type VolunteerTask } from "@/lib/portal";
import {
  backendTimestamp,
  completedAssignments,
  taskIcon,
  taskTone
} from "@/lib/volunteer";

export default function VolunteerContributionsPage() {
  const { t, locale } = useI18n();
  const [tasks, setTasks] = useState<VolunteerTask[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [refreshToken, setRefreshToken] = useState(0);

  useEffect(() => {
    let active = true;
    setLoading(true);
    getMyVolunteerTasks(1, 200)
      .then((result) => {
        if (!active) return;
        setTasks(result.items);
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

  const completed = useMemo(() => completedAssignments(tasks), [tasks]);

  return (
    <VolunteerPage
      eyebrowKey="volunteerX.contributions.title"
      titleKey="volunteerX.contributions.title"
      descriptionKey="volunteerX.contributions.description"
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
      <section className="volunteer-contribution-intro" aria-labelledby="volunteer-contribution-heading">
        <span className="volunteer-contribution-intro__icon" aria-hidden="true">
          <Icon name="award" size={28} />
        </span>
        <div>
          <h2 id="volunteer-contribution-heading">{t("volunteerX.contributions.appreciativeTitle")}</h2>
          <p>{t("volunteerX.contributions.appreciativeBody")}</p>
        </div>
        {!loading && !error ? (
          <span className="volunteer-contribution-intro__count">
            {t("volunteerX.contributions.completedCount", { count: completed.length })}
          </span>
        ) : null}
      </section>

      {loading ? (
        <PortalState kind="loading" title={t("volunteerX.contributions.loadingTitle")}>
          {t("volunteerX.contributions.loadingBody")}
        </PortalState>
      ) : error ? (
        <div className="volunteer-error-bar" role="alert">
          <Icon name="info" size={17} />
          <span>{error}</span>
          <button className="btn btn-primary" type="button" onClick={() => setRefreshToken((value) => value + 1)}>
            {t("volunteerX.shared.retry")}
          </button>
        </div>
      ) : completed.length === 0 ? (
        <PortalState kind="empty" title={t("volunteerX.contributions.emptyTitle")} href="/volunteer/opportunities" linkLabel={t("volunteerX.landing.cardFindCta")}>
          {t("volunteerX.contributions.emptyBody")}
        </PortalState>
      ) : (
        <PortalSection title={t("volunteerX.contributions.completedCount", { count: completed.length })}>
          <div className="volunteer-contribution-list">
            {completed.map((task) => (
              <article className="volunteer-contribution-item" key={task.id}>
                <span className="volunteer-contribution-item__icon" aria-hidden="true">
                  <Icon name={taskIcon(task)} size={22} />
                </span>
                <div className="volunteer-contribution-item__body">
                  <h3>{task.title}</h3>
                  <p>
                    <Icon name="calendar" size={15} />
                    {task.completedAt
                      ? `${t("volunteerX.contributions.completedOn")}: ${formatDate(backendTimestamp(task.completedAt), locale)}`
                      : t("volunteerX.taskStage.historyUnavailable")}
                  </p>
                </div>
                <span className={`badge ${taskTone("Completed")}`}>
                  <i aria-hidden="true" /> {t("volunteerX.shared.status.Completed")}
                </span>
                <Link className="btn btn-outline" href={`/volunteer/assignments/${task.id}`}>
                  {t("volunteerX.contributions.viewAssignment")} <Icon name="arrow" size={15} />
                </Link>
              </article>
            ))}
          </div>
        </PortalSection>
      )}

      <div className="volunteer-info-panel" role="note">
        <Icon name="info" size={18} />
        <div>
          <b>{t("volunteerX.contributions.title")}</b>
          <p>{t("volunteerX.contributions.noImpactClaim")}</p>
        </div>
      </div>

      <div className="volunteer-form-actions">
        <Link className="btn btn-outline" href="/portal/volunteer">
          <Icon name="arrowLeft" size={16} /> {t("volunteerX.contributions.back")}
        </Link>
      </div>
    </VolunteerPage>
  );
}
