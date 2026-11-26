"use client";

import { buildCitizenStages, isExceptionalStatus, type CitizenStageKey } from "@/lib/citizen-status";
import type { RequestStatus, RequestTimelineEntry } from "@/lib/crisis";
import { formatDate } from "@/lib/i18n/core";
import { useI18n } from "@/lib/i18n";
import { Icon } from "./Icon";
import { PortalNotice } from "./PortalPrimitives";

const STAGE_ICONS: Record<CitizenStageKey, string> = {
  submitted: "clipboard",
  review: "search",
  progress: "truck",
  resolved: "checkCircle"
};

export function CitizenStatusTimeline({
  status,
  timeline
}: {
  status: RequestStatus;
  timeline: RequestTimelineEntry[];
}) {
  const { t, locale } = useI18n();
  const stages = buildCitizenStages(status, timeline);
  const exceptional = isExceptionalStatus(status);

  return (
    <div className="citizen-timeline-wrap">
      {exceptional ? (
        <PortalNotice tone="error">
          <strong>{t("citizenX.tracking.exceptionalTitle")}: </strong>
          {t(`citizenX.status.${status}`)} — {t(`citizenX.statusNote.${status}`)}
        </PortalNotice>
      ) : null}

      <ol className="citizen-timeline" aria-label={t("citizenX.tracking.timelineTitle")}>
        {stages.map((stage, index) => {
          const markerIcon =
            stage.state === "completed" ? "checkCircle" : exceptional && stage.state === "exceptional" ? "info" : STAGE_ICONS[stage.key];
          return (
            <li
              className={`citizen-timeline__step is-${stage.state}`}
              key={stage.key}
              aria-current={stage.state === "current" ? "step" : undefined}
            >
              <span className="citizen-timeline__marker" aria-hidden="true">
                <Icon name={markerIcon} size={20} />
              </span>
              <div className="citizen-timeline__body">
                <span className="citizen-timeline__label">
                  {t(`citizenX.stage.${stage.key}Title`)}
                  {stage.state === "current" ? (
                    <em>{t("citizenX.stage.current")}</em>
                  ) : null}
                </span>
                <p>{t(`citizenX.stage.${stage.key}Copy`)}</p>
                <small>
                  {stage.timestamp
                    ? formatDate(stage.timestamp, locale)
                    : stage.state === "pending"
                      ? t("citizenX.stage.pending")
                      : t("citizenX.stage.historyUnavailable")}
                </small>
                {stage.reachedStatuses.length > 0 ? (
                  <span className="citizen-timeline__statuses">
                    {stage.reachedStatuses.map((item) => t(`citizenX.status.${item}`)).join(" · ")}
                  </span>
                ) : null}
              </div>
              {index < stages.length - 1 ? <span className="citizen-timeline__connector" aria-hidden="true" /> : null}
            </li>
          );
        })}
      </ol>
    </div>
  );
}
