"use client";

import { useI18n } from "@/lib/i18n";
import { formatDonationDate, type DonorTimelineStep } from "@/lib/donor";
import { Icon } from "./Icon";

/** Honest donation stages; no date is printed unless the backend supplied one. */
export function DonorTimeline({
  steps,
  ariaLabelKey = "donorX.detail.timelineTitle",
  noteKey
}: {
  steps: DonorTimelineStep[];
  ariaLabelKey?: string;
  noteKey?: string;
}) {
  const { t, locale } = useI18n();

  return (
    <div className="donor-timeline-wrap">
      <ol className="donor-timeline" aria-label={t(ariaLabelKey)}>
        {steps.map((step, index) => {
          const icon = step.state === "completed" ? "check" : step.icon;
          return (
            <li
              className={`donor-timeline__step is-${step.state}`}
              key={step.key}
              aria-current={step.state === "current" ? "step" : undefined}
            >
              <span className="donor-timeline__marker" aria-hidden="true">
                <Icon name={icon} size={19} />
              </span>
              <div className="donor-timeline__body">
                <span className="donor-timeline__label">
                  {t(step.labelKey)}
                  {step.state === "current" ? <em>{t("donorX.shared.current")}</em> : null}
                </span>
                <p>{t(step.copyKey)}</p>
                {step.timestamp ? (
                  <small>{formatDonationDate(step.timestamp, locale)}</small>
                ) : step.state === "pending" ? (
                  <small>{t("donorX.shared.pending")}</small>
                ) : null}
              </div>
              {index < steps.length - 1 ? (
                <span className="donor-timeline__connector" aria-hidden="true" />
              ) : null}
            </li>
          );
        })}
      </ol>
      {noteKey ? (
        <p className="donor-timeline__note">
          <Icon name="info" size={14} /> {t(noteKey)}
        </p>
      ) : null}
    </div>
  );
}
