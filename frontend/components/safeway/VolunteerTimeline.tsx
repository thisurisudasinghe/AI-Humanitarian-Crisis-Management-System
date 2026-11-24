"use client";

import { formatDate } from "@/lib/i18n/core";
import { useI18n } from "@/lib/i18n";
import { backendTimestamp, type VolunteerTimelineStep } from "@/lib/volunteer";
import { Icon } from "./Icon";

export function VolunteerTimeline({
  steps,
  ariaLabelKey,
  noteKey
}: {
  steps: VolunteerTimelineStep[];
  ariaLabelKey: string;
  noteKey?: string;
}) {
  const { t, locale } = useI18n();

  return (
    <div className="volunteer-timeline-wrap">
      <ol className="volunteer-timeline" aria-label={t(ariaLabelKey)}>
        {steps.map((step, index) => {
          const icon = step.state === "completed" ? "check" : step.icon;
          return (
            <li
              className={`volunteer-timeline__step is-${step.state}`}
              key={step.key}
              aria-current={step.state === "current" ? "step" : undefined}
            >
              <span className="volunteer-timeline__marker" aria-hidden="true">
                <Icon name={icon} size={19} />
              </span>
              <div className="volunteer-timeline__body">
                <span className="volunteer-timeline__label">
                  {t(step.labelKey)}
                  {step.state === "current" ? (
                    <em>{t("volunteerX.taskStage.current")}</em>
                  ) : null}
                </span>
                <p>{t(step.copyKey)}</p>
                {step.timestamp ? (
                  <small>{formatDate(backendTimestamp(step.timestamp), locale)}</small>
                ) : step.state === "pending" ? (
                  <small>{t("volunteerX.verifyStage.pending")}</small>
                ) : null}
              </div>
              {index < steps.length - 1 ? (
                <span className="volunteer-timeline__connector" aria-hidden="true" />
              ) : null}
            </li>
          );
        })}
      </ol>
      {noteKey ? (
        <p className="volunteer-timeline__note">
          <Icon name="info" size={14} /> {t(noteKey)}
        </p>
      ) : null}
    </div>
  );
}
