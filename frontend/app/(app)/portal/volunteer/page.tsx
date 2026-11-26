"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import {
  PortalHeader,
  PortalSection,
  PortalState
} from "@/components/safeway/PortalPrimitives";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useI18n } from "@/lib/i18n";
import { formatDate } from "@/lib/i18n/core";
import {
  getMyVolunteerTasks,
  getVolunteerPortal,
  type VolunteerPortalData,
  type VolunteerTask
} from "@/lib/portal";
import { RoleNames } from "@/lib/roles";
import {
  activeAssignments,
  backendTimestamp,
  normalizeVerificationStatus,
  verificationTone
} from "@/lib/volunteer";

const ACTION_CARDS = [
  {
    href: "/volunteer/opportunities",
    icon: "search",
    titleKey: "volunteerX.landing.cardFindTitle",
    copyKey: "volunteerX.landing.cardFindCopy",
    ctaKey: "volunteerX.landing.cardFindCta",
    className: "blue"
  },
  {
    href: "/volunteer/assignments",
    icon: "clipboard",
    titleKey: "volunteerX.landing.cardAssignmentsTitle",
    copyKey: "volunteerX.landing.cardAssignmentsCopy",
    ctaKey: "volunteerX.landing.cardAssignmentsCta",
    className: "navy"
  },
  {
    href: "/volunteer/availability",
    icon: "calendar",
    titleKey: "volunteerX.landing.cardAvailabilityTitle",
    copyKey: "volunteerX.landing.cardAvailabilityCopy",
    ctaKey: "volunteerX.landing.cardAvailabilityCta",
    className: "green"
  },
  {
    href: "/volunteer/contributions",
    icon: "award",
    titleKey: "volunteerX.landing.cardContributionsTitle",
    copyKey: "volunteerX.landing.cardContributionsCopy",
    ctaKey: "volunteerX.landing.cardContributionsCta",
    className: "sky"
  }
] as const;

export default function VolunteerPortalPage() {
  const { t, locale } = useI18n();
  const [portal, setPortal] = useState<VolunteerPortalData | null>(null);
  const [tasks, setTasks] = useState<VolunteerTask[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [refreshToken, setRefreshToken] = useState(0);

  const load = useCallback(async () => {
    const [statusResult, taskResult] = await Promise.all([
      getVolunteerPortal(),
      getMyVolunteerTasks(1, 50)
    ]);
    return { statusResult, taskResult };
  }, []);

  useEffect(() => {
    let active = true;
    if (refreshToken === 0) setLoading(true);
    else setRefreshing(true);

    load()
      .then(({ statusResult, taskResult }) => {
        if (!active) return;
        setPortal(statusResult);
        setTasks(taskResult.items);
        setError(null);
      })
      .catch((cause) => {
        if (active) setError(localizedErrorMessage(cause, t));
      })
      .finally(() => {
        if (!active) return;
        setLoading(false);
        setRefreshing(false);
      });

    return () => {
      active = false;
    };
  }, [load, refreshToken, t]);

  const status = normalizeVerificationStatus(
    portal?.profile?.verificationStatus,
    Boolean(portal?.profile)
  );
  const upcoming = activeAssignments(tasks).slice(0, 3);
  const readinessTitleKey =
    status === "Verified"
      ? "volunteerX.landing.readyTitle"
      : status === "Pending"
        ? "volunteerX.landing.pendingTitle"
        : status === "Rejected"
          ? "volunteerX.landing.rejectedTitle"
          : status === "Unknown"
            ? "volunteerX.landing.unknownTitle"
            : "volunteerX.landing.getReadyTitle";

  return (
    <AuthGuard allowedRoles={[RoleNames.Volunteer]} activationPurpose="volunteer">
      <div className="volunteer-landing">
        <PortalHeader
          eyebrow={t("volunteerX.landing.eyebrow")}
          title={t("volunteerX.landing.title")}
          description={t("volunteerX.landing.support")}
          action={
            <button
              className="btn btn-outline"
              type="button"
              onClick={() => setRefreshToken((value) => value + 1)}
              disabled={refreshing}
              aria-busy={refreshing}
            >
              <Icon name="clock" size={16} />
              {refreshing ? t("volunteerX.shared.refresh") : t("volunteerX.shared.refresh")}
            </button>
          }
        />

        {error ? (
          <div className="volunteer-error-bar" role="alert">
            <Icon name="info" size={17} />
            <span>{error}</span>
            <button
              className="btn btn-primary"
              type="button"
              onClick={() => setRefreshToken((value) => value + 1)}
            >
              {t("volunteerX.shared.retry")}
            </button>
          </div>
        ) : null}

        <section className={`volunteer-readiness volunteer-readiness--${verificationTone(status)}`}>
          <span className="volunteer-readiness__icon" aria-hidden="true">
            <Icon name={status === "Verified" ? "shield" : status === "None" ? "user" : "clipboard"} size={26} />
          </span>
          <div className="volunteer-readiness__body">
            {loading ? (
              <>
                <h2>{t("volunteerX.landing.readinessLoadingTitle")}</h2>
                <p>{t("volunteerX.landing.readinessLoadingBody")}</p>
              </>
            ) : status === "None" ? (
              <>
                <h2>{t("volunteerX.landing.getReadyTitle")}</h2>
                <p>{t("volunteerX.landing.getReadyBody")}</p>
              </>
            ) : (
              <>
                <h2>{t(readinessTitleKey)}</h2>
                <p>
                  {t("volunteerX.landing.readyBody", {
                    status: status === "Unknown" ? t("volunteerX.shared.status.Unknown") : t(`volunteerX.shared.status.${status}`),
                    note: t(`volunteerX.shared.statusNote.${status}`)
                  })}
                </p>
              </>
            )}
          </div>
          {!loading ? (
            <div className="volunteer-readiness__actions">
              {status === "None" ? (
                <Link className="btn btn-primary" href="/volunteer/profile">
                  {t("volunteerX.landing.completeProfile")} <Icon name="arrow" size={16} />
                </Link>
              ) : (
                <>
                  <Link className="btn btn-primary" href="/volunteer/profile">
                    <Icon name="user" size={16} /> {t("volunteerX.landing.editProfile")}
                  </Link>
                  <Link className="btn btn-outline" href="/volunteer/verification">
                    {t("volunteerX.landing.verificationDetails")}
                  </Link>
                </>
              )}
            </div>
          ) : null}
        </section>

        <section className="volunteer-actions" aria-label={t("volunteerX.landing.actionsAria")}>
          {ACTION_CARDS.map((card) => (
            <Link className={`volunteer-action volunteer-action--${card.className}`} href={card.href} key={card.href}>
              <span className="volunteer-action__icon" aria-hidden="true">
                <Icon name={card.icon} size={26} />
              </span>
              <span className="volunteer-action__content">
                <b>{t(card.titleKey)}</b>
                <span>{t(card.copyKey)}</span>
              </span>
              <span className="volunteer-action__cta">
                {t(card.ctaKey)} <Icon name="arrow" size={17} />
              </span>
            </Link>
          ))}
        </section>

        <PortalSection
          className="volunteer-upcoming-section"
          title={t("volunteerX.landing.upcomingTitle")}
          description={t("volunteerX.landing.upcomingDescription")}
          action={
            <Link className="portal-text-link" href="/volunteer/assignments">
              {t("volunteerX.landing.viewAll")}
            </Link>
          }
        >
          {loading ? (
            <PortalState kind="loading" title={t("volunteerX.landing.upcomingLoadingTitle")}>
              {t("volunteerX.landing.upcomingLoadingBody")}
            </PortalState>
          ) : error ? (
            <PortalState kind="error" title={t("volunteerX.landing.upcomingErrorTitle")}>
              {error}
            </PortalState>
          ) : upcoming.length === 0 ? (
            <PortalState kind="empty" title={t("volunteerX.landing.upcomingEmptyTitle")} href="/volunteer/opportunities" linkLabel={t("volunteerX.landing.cardFindCta")}>
              {t("volunteerX.landing.upcomingEmptyBody")}
            </PortalState>
          ) : (
            <div className="volunteer-upcoming-list">
              {upcoming.map((task) => (
                <article className="volunteer-upcoming-row" key={task.id}>
                  <span className="volunteer-upcoming-row__icon" aria-hidden="true">
                    <Icon name="clipboard" size={18} />
                  </span>
                  <div className="volunteer-upcoming-row__main">
                    <b>{task.title}</b>
                    <small>{task.scheduledAt ? formatDate(backendTimestamp(task.scheduledAt), locale) : t("volunteerX.shared.scheduleTbc")}</small>
                  </div>
                  <span className={`badge ${task.status.toLowerCase() === "assigned" ? "amber" : "blue"}`}>
                    <i aria-hidden="true" /> {t(`volunteerX.shared.status.${task.status}`)}
                  </span>
                  <Link href={`/volunteer/assignments/${task.id}`}>
                    {t("volunteerX.assignment.viewDetails")} <Icon name="arrow" size={14} />
                  </Link>
                </article>
              ))}
            </div>
          )}
        </PortalSection>

        <p className="volunteer-landing__note">
          <Icon name="bell" size={16} /> {t("volunteerX.landing.personalNote")}
        </p>
      </div>
    </AuthGuard>
  );
}
