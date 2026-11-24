"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { formatDate } from "@/lib/i18n/core";
import { useI18n } from "@/lib/i18n";
import type { VolunteerTask } from "@/lib/portal";
import {
  backendTimestamp,
  hasValidPoint,
  mapLink,
  normalizeTaskStatus,
  parseBackendArray,
  skillLabel,
  taskIcon,
  taskTone
} from "@/lib/volunteer";
import { Icon } from "./Icon";

export function VolunteerTaskCard({
  task,
  mode,
  href,
  children
}: {
  task: VolunteerTask;
  mode: "opportunity" | "assignment" | "compact";
  href: string;
  children?: ReactNode;
}) {
  const { t, locale } = useI18n();
  const status = normalizeTaskStatus(task.status);
  const skills = parseBackendArray(task.requiredSkillsJson);
  const validLocation = hasValidPoint(task.lat, task.lng);

  return (
    <article className={`volunteer-task-card volunteer-task-card--${mode}`}>
      <div className="volunteer-task-card__head">
        <span className="volunteer-task-card__icon" aria-hidden="true">
          <Icon name={taskIcon(task)} size={22} />
        </span>
        <div className="volunteer-task-card__title">
          <h3>{task.title}</h3>
          <span className={`badge ${taskTone(status)}`}>
            <i aria-hidden="true" /> {t(`volunteerX.shared.status.${status}`)}
          </span>
        </div>
      </div>

      {task.requestId ? (
        <p className="volunteer-task-card__ref">{t("citizenX.tracking.reference")}: <span title={task.requestId}>{task.requestId}</span></p>
      ) : null}

      <dl className="volunteer-task-card__meta">
        <div>
          <dt><Icon name="clock" size={15} /> {t("volunteerX.assignment.scheduledLabel")}</dt>
          <dd>{task.scheduledAt ? formatDate(backendTimestamp(task.scheduledAt), locale) : t("volunteerX.shared.scheduleTbc")}</dd>
        </div>
        <div>
          <dt><Icon name="pin" size={15} /> {t("volunteerX.opportunity.availabilityLabel")}</dt>
          <dd>
            {validLocation ? (
              <a href={mapLink(task.lat as number, task.lng as number)} target="_blank" rel="noreferrer">
                {t("volunteerX.shared.locationOnMap")} <Icon name="arrow" size={13} />
              </a>
            ) : (
              t("volunteerX.shared.locationNotSpecified")
            )}
          </dd>
        </div>
      </dl>

      {skills.length > 0 ? (
        <p className="volunteer-task-card__skills">
          <Icon name="checkCircle" size={15} />
          {t("volunteerX.shared.skillsLabel", {
            skills: skills.map((skill) => skillLabel(t, skill)).join(", ")
          })}
        </p>
      ) : (
        <p className="volunteer-task-card__skills">
          <Icon name="info" size={15} /> {t("volunteerX.opportunity.noSkills")}
        </p>
      )}

      {mode !== "compact" ? (
        <p className="volunteer-task-card__desc">
          {t("volunteerX.shared.noDescription")}
        </p>
      ) : null}

      <div className="volunteer-task-card__actions">
        <Link className="btn btn-outline" href={href}>
          {mode === "opportunity"
            ? t("volunteerX.opportunity.viewDetails")
            : t("volunteerX.assignment.viewDetails")}
          <Icon name="arrow" size={15} />
        </Link>
        {children}
      </div>
    </article>
  );
}
