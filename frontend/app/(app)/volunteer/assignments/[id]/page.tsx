"use client";

import dynamic from "next/dynamic";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import type { MapMarker } from "@/components/map/MapView";
import { Icon } from "@/components/safeway/Icon";
import { PortalNotice, PortalSection, PortalState } from "@/components/safeway/PortalPrimitives";
import { VolunteerPage } from "@/components/safeway/VolunteerPage";
import { VolunteerTimeline } from "@/components/safeway/VolunteerTimeline";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useI18n } from "@/lib/i18n";
import { formatDate } from "@/lib/i18n/core";
import {
  completeTask,
  getMyVolunteerTasks,
  respondToTask,
  type VolunteerTask
} from "@/lib/portal";
import {
  backendTimestamp,
  buildTaskTimeline,
  hasValidPoint,
  mapLink,
  normalizeTaskStatus,
  parseBackendArray,
  skillLabel,
  taskIcon,
  taskTone
} from "@/lib/volunteer";

const MapView = dynamic(() => import("@/components/map/MapView"), { ssr: false });
const NOTICE_KEY = "safeway.volunteer.assignmentNotice";

export default function VolunteerAssignmentDetailPage() {
  const params = useParams<{ id: string }>();
  const id = typeof params.id === "string" ? params.id : "";
  const { t, locale } = useI18n();
  const [tasks, setTasks] = useState<VolunteerTask[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const busyRef = useRef(false);
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

  const task = tasks.find((item) => item.id === id) ?? null;
  const status = normalizeTaskStatus(task?.status);
  const skills = task ? parseBackendArray(task.requiredSkillsJson) : [];
  const validLocation = task ? hasValidPoint(task.lat, task.lng) : false;
  const markers: MapMarker[] =
    task && validLocation
      ? [{ id: task.id, lat: task.lat as number, lng: task.lng as number, label: task.title, type: "assignment" }]
      : [];

  async function runAction(kind: "accepted" | "declined" | "completed") {
    if (!task || busyRef.current) {
      if (busyRef.current) setActionError(t("volunteerX.assignment.duplicateError"));
      return;
    }
    busyRef.current = true;
    setBusy(true);
    setActionError(null);
    setNotice(null);

    try {
      if (kind === "accepted") await respondToTask(task.id, true);
      else if (kind === "declined") await respondToTask(task.id, false);
      else await completeTask(task.id);

      const messageKey =
        kind === "accepted"
          ? "volunteerX.assignment.acceptSuccess"
          : kind === "declined"
            ? "volunteerX.assignment.declineSuccess"
            : "volunteerX.assignment.completeSuccess";
      window.sessionStorage.setItem(NOTICE_KEY, kind);
      setNotice(t(messageKey));
      setRefreshToken((value) => value + 1);
    } catch (cause) {
      setActionError(localizedErrorMessage(cause, t));
    } finally {
      busyRef.current = false;
      setBusy(false);
    }
  }

  return (
    <VolunteerPage
      eyebrowKey="volunteerX.assignment.detailEyebrow"
      titleKey="volunteerX.assignment.detailTitle"
      descriptionKey="volunteerX.assignment.description"
      action={
        <Link className="btn btn-outline" href="/volunteer/assignments">
          <Icon name="arrowLeft" size={16} /> {t("volunteerX.assignment.back")}
        </Link>
      }
    >
      {notice ? <PortalNotice>{notice}</PortalNotice> : null}
      {actionError ? <PortalNotice tone="error">{actionError}</PortalNotice> : null}

      {loading ? (
        <PortalState kind="loading" title={t("volunteerX.assignment.loadingTitle")}>
          {t("volunteerX.assignment.loadingBody")}
        </PortalState>
      ) : error ? (
        <div className="volunteer-error-bar" role="alert">
          <Icon name="info" size={17} />
          <span>{error}</span>
          <button className="btn btn-primary" type="button" onClick={() => setRefreshToken((value) => value + 1)}>
            {t("volunteerX.shared.retry")}
          </button>
        </div>
      ) : !task ? (
        <>
          {notice ? (
            <PortalState kind="empty" title={t("volunteerX.assignment.unavailableAfterDecline")}>
              {t("volunteerX.assignment.notFoundBody")}
            </PortalState>
          ) : (
            <PortalState kind="empty" title={t("volunteerX.assignment.notFoundTitle")}>
              {t("volunteerX.assignment.notFoundBody")}
            </PortalState>
          )}
        </>
      ) : (
        <>
          <section className="volunteer-detail-hero">
            <span className="volunteer-detail-hero__icon" aria-hidden="true">
              <Icon name={taskIcon(task)} size={28} />
            </span>
            <div>
              <h2>{task.title}</h2>
              <span className={`badge ${taskTone(status)}`}>
                <i aria-hidden="true" /> {t(`volunteerX.shared.status.${status}`)}
              </span>
            </div>
          </section>

          <div className="volunteer-detail-grid">
            <PortalSection title={t("volunteerX.assignment.instructionsTitle")}>
              <p>{t("volunteerX.assignment.noInstructions")}</p>
              <dl className="volunteer-facts">
                <div>
                  <dt><Icon name="clock" size={16} /> {t("volunteerX.assignment.scheduledLabel")}</dt>
                  <dd>{task.scheduledAt ? formatDate(backendTimestamp(task.scheduledAt), locale) : t("volunteerX.shared.scheduleTbc")}</dd>
                </div>
                <div>
                  <dt><Icon name="checkCircle" size={16} /> {t("volunteerX.assignment.statusLabel")}</dt>
                  <dd>{t(`volunteerX.shared.status.${status}`)}</dd>
                </div>
                {task.completedAt ? (
                  <div>
                    <dt><Icon name="award" size={16} /> {t("volunteerX.assignment.completedLabel")}</dt>
                    <dd>{formatDate(backendTimestamp(task.completedAt), locale)}</dd>
                  </div>
                ) : null}
                <div>
                  <dt><Icon name="pin" size={16} /> {t("volunteerX.opportunity.availabilityLabel")}</dt>
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
            </PortalSection>

            <PortalSection title={t("volunteerX.assignment.requestedSkills")}>
              {skills.length > 0 ? (
                <div className="volunteer-chip-list">
                  {skills.map((skill) => (
                    <span className="volunteer-chip is-static" key={skill}>
                      <Icon name="checkCircle" size={15} /> {skillLabel(t, skill)}
                    </span>
                  ))}
                </div>
              ) : (
                <p>{t("volunteerX.assignment.noSkills")}</p>
              )}
            </PortalSection>
          </div>

          {markers.length > 0 ? (
            <PortalSection title={t("volunteerX.shared.openMap")}>
              <div className="volunteer-map-frame">
                <MapView markers={markers} height={260} />
              </div>
            </PortalSection>
          ) : null}

          <PortalSection title={t("volunteerX.assignment.timelineTitle")}>
            <VolunteerTimeline
              steps={buildTaskTimeline(status, task.completedAt)}
              ariaLabelKey="volunteerX.assignment.timelineTitle"
              noteKey="volunteerX.taskStage.noHistoryNote"
            />
          </PortalSection>

          {status === "Assigned" || status === "Accepted" ? (
            <section className="volunteer-actions-panel" aria-labelledby="volunteer-task-actions-heading">
              <h2 id="volunteer-task-actions-heading">{t("volunteerX.assignment.statusLabel")}</h2>
              <p>{t("volunteerX.assignment.coordinatorNote")}</p>
              <div className="volunteer-actions-panel__buttons">
                {status === "Assigned" ? (
                  <>
                    <button className="btn btn-primary" type="button" disabled={busy} aria-busy={busy} onClick={() => void runAction("accepted")}>
                      <Icon name="check" size={16} /> {t("volunteerX.assignment.accept")}
                    </button>
                    <button className="btn btn-outline" type="button" disabled={busy} aria-busy={busy} onClick={() => void runAction("declined")}>
                      <Icon name="close" size={16} /> {t("volunteerX.assignment.decline")}
                    </button>
                  </>
                ) : (
                  <button className="btn btn-primary" type="button" disabled={busy} aria-busy={busy} onClick={() => void runAction("completed")}>
                    <Icon name="award" size={16} /> {t("volunteerX.assignment.complete")}
                  </button>
                )}
              </div>
            </section>
          ) : null}
        </>
      )}
    </VolunteerPage>
  );
}
