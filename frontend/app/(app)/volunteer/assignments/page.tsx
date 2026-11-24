"use client";

import { useEffect, useState } from "react";
import { Icon } from "@/components/safeway/Icon";
import { PortalNotice, PortalState } from "@/components/safeway/PortalPrimitives";
import { VolunteerPage } from "@/components/safeway/VolunteerPage";
import { VolunteerTaskCard } from "@/components/safeway/VolunteerTaskCard";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useI18n } from "@/lib/i18n";
import { getMyVolunteerTasks, type VolunteerTask } from "@/lib/portal";
import { normalizeTaskStatus } from "@/lib/volunteer";

const NOTICE_KEY = "safeway.volunteer.assignmentNotice";

const NOTICE_KEYS: Record<string, string> = {
  accepted: "volunteerX.assignment.acceptSuccess",
  declined: "volunteerX.assignment.declineSuccess",
  completed: "volunteerX.assignment.completeSuccess"
};

export default function VolunteerAssignmentsPage() {
  const { t } = useI18n();
  const [tasks, setTasks] = useState<VolunteerTask[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [refreshToken, setRefreshToken] = useState(0);

  useEffect(() => {
    const pending = window.sessionStorage.getItem(NOTICE_KEY);
    if (pending) {
      window.sessionStorage.removeItem(NOTICE_KEY);
      const key = NOTICE_KEYS[pending];
      if (key) setNotice(t(key));
    }
  }, [t]);

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

  const groups = [
    { status: "Assigned", titleKey: "volunteerX.assignment.groupAssigned", icon: "clipboard" },
    { status: "Accepted", titleKey: "volunteerX.assignment.groupAccepted", icon: "handHeart" },
    { status: "Completed", titleKey: "volunteerX.assignment.groupCompleted", icon: "checkCircle" },
    { status: "Open", titleKey: "volunteerX.assignment.groupOpen", icon: "search" }
  ] as const;

  return (
    <VolunteerPage
      eyebrowKey="volunteerX.assignment.detailEyebrow"
      titleKey="volunteerX.assignment.title"
      descriptionKey="volunteerX.assignment.description"
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
      {notice ? <PortalNotice>{notice}</PortalNotice> : null}
      {error ? (
        <div className="volunteer-error-bar" role="alert">
          <Icon name="info" size={17} />
          <span>{error}</span>
          <button className="btn btn-primary" type="button" onClick={() => setRefreshToken((value) => value + 1)}>
            {t("volunteerX.shared.retry")}
          </button>
        </div>
      ) : null}

      {loading ? (
        <PortalState kind="loading" title={t("volunteerX.assignment.loadingTitle")}>
          {t("volunteerX.assignment.loadingBody")}
        </PortalState>
      ) : error ? (
        <PortalState kind="error" title={t("volunteerX.assignment.errorTitle")}>
          {error}
        </PortalState>
      ) : tasks.length === 0 ? (
        <PortalState kind="empty" title={t("volunteerX.assignment.emptyTitle")} href="/volunteer/opportunities" linkLabel={t("volunteerX.landing.cardFindCta")}>
          {t("volunteerX.assignment.emptyBody")}
        </PortalState>
      ) : (
        <div className="volunteer-assignment-groups">
          {groups.map((group) => {
            const items = tasks.filter((task) => normalizeTaskStatus(task.status) === group.status);
            if (items.length === 0) return null;
            return (
              <section className="volunteer-group" key={group.status} aria-labelledby={`volunteer-group-${group.status}`}>
                <div className="volunteer-group__head">
                  <span aria-hidden="true"><Icon name={group.icon} size={18} /></span>
                  <h2 id={`volunteer-group-${group.status}`}>{t(group.titleKey)}</h2>
                  <span className="badge muted"><i aria-hidden="true" /> {items.length}</span>
                </div>
                <div className="volunteer-task-grid">
                  {items.map((task) => (
                    <VolunteerTaskCard
                      key={task.id}
                      task={task}
                      mode="assignment"
                      href={
                        group.status === "Open"
                          ? `/volunteer/opportunities/${task.id}`
                          : `/volunteer/assignments/${task.id}`
                      }
                    />
                  ))}
                </div>
              </section>
            );
          })}
        </div>
      )}
    </VolunteerPage>
  );
}
