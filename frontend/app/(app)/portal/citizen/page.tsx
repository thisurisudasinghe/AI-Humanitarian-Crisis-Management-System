"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import {
  PortalHeader,
  PortalSection,
  PortalState
} from "@/components/safeway/PortalPrimitives";
import { statusTone } from "@/lib/citizen-status";
import { caseRequestErrorMessage, listMyCaseRequests, type CaseRequestListItem } from "@/lib/crisis";
import { useI18n } from "@/lib/i18n";
import { formatDate } from "@/lib/i18n/core";
import { emergencyContacts } from "@/lib/publicEmergency";
import { RoleNames } from "@/lib/roles";

const EMERGENCY_LABEL_KEYS: Record<string, string> = {
  police: "citizenX.landing.police",
  ambulance: "citizenX.landing.ambulance",
  fire: "citizenX.landing.fire"
};

const ACTION_CARDS = [
  {
    href: "/report",
    icon: "alert",
    titleKey: "reportTitle",
    copyKey: "reportCopy",
    ctaKey: "reportCta",
    className: "primary"
  },
  {
    href: "/find-assistance",
    icon: "pin",
    titleKey: "assistanceTitle",
    copyKey: "assistanceCopy",
    ctaKey: "assistanceCta",
    className: "blue"
  },
  {
    href: "/my-reports",
    icon: "clipboard",
    titleKey: "trackTitle",
    copyKey: "trackCopy",
    ctaKey: "trackCta",
    className: "green"
  },
  {
    href: "/public-alerts",
    icon: "megaphone",
    titleKey: "alertsTitle",
    copyKey: "alertsCopy",
    ctaKey: "alertsCta",
    className: "amber"
  }
] as const;

export default function CitizenPortalPage() {
  const { t, locale } = useI18n();
  const [reports, setReports] = useState<CaseRequestListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    listMyCaseRequests(1, 5)
      .then((result) => {
        if (!active) return;
        setReports(result.items);
        setError(null);
      })
      .catch((cause) => {
        if (active) setError(caseRequestErrorMessage(cause, t("citizenX.landing.recentErrorTitle"), t));
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [t]);

  return (
    <AuthGuard allowedRoles={[RoleNames.Citizen]} activationPurpose="citizen">
      <div className="citizen-landing">
        <PortalHeader
          eyebrow={t("citizenX.landing.eyebrow")}
          title={t("citizenX.landing.title")}
          description={t("citizenX.landing.support")}
        />

        <section className="citizen-urgent" aria-labelledby="citizen-urgent-heading">
          <span className="citizen-urgent__icon" aria-hidden="true">
            <Icon name="shield" size={24} />
          </span>
          <div className="citizen-urgent__body">
            <h2 id="citizen-urgent-heading">{t("citizenX.landing.urgentTitle")}</h2>
            <p>{t("citizenX.landing.urgentBody")}</p>
            <div className="citizen-urgent__numbers">
              {emergencyContacts.slice(0, 3).map((contact) => (
                <a href={contact.primary.href} key={contact.id}>
                  <Icon name="phone" size={16} />
                  <span>{t(EMERGENCY_LABEL_KEYS[contact.id])}</span>
                  <strong>{contact.primary.display}</strong>
                </a>
              ))}
            </div>
            <small>{t("citizenX.landing.officialNumbers")}</small>
          </div>
        </section>

        <section className="citizen-actions" aria-label={t("citizenX.landing.actionsAria")}>
          {ACTION_CARDS.map((card) => (
            <Link className={`citizen-action citizen-action--${card.className}`} href={card.href} key={card.href}>
              <span className="citizen-action__icon" aria-hidden="true">
                <Icon name={card.icon} size={26} />
              </span>
              <span className="citizen-action__content">
                <b>{t(`citizenX.landing.${card.titleKey}`)}</b>
                <span>{t(`citizenX.landing.${card.copyKey}`)}</span>
              </span>
              <span className="citizen-action__cta">
                {t(`citizenX.landing.${card.ctaKey}`)} <Icon name="arrow" size={17} />
              </span>
            </Link>
          ))}
        </section>

        <PortalSection
          className="citizen-recent-section"
          title={t("citizenX.landing.recentTitle")}
          description={t("citizenX.landing.recentDescription")}
          action={
            <Link className="portal-text-link" href="/my-reports">
              {t("citizenX.landing.viewAll")}
            </Link>
          }
        >
          {loading ? (
            <PortalState kind="loading" title={t("citizenX.landing.recentLoadingTitle")}>
              {t("citizenX.landing.recentLoadingBody")}
            </PortalState>
          ) : error ? (
            <PortalState kind="error" title={t("citizenX.landing.recentErrorTitle")}>
              {error}
            </PortalState>
          ) : reports.length === 0 ? (
            <PortalState kind="empty" title={t("citizenX.landing.recentEmptyTitle")} href="/report" linkLabel={t("citizenX.landing.reportCta")}>
              {t("citizenX.landing.recentEmptyBody")}
            </PortalState>
          ) : (
            <div className="citizen-recent-list">
              {reports.map((report) => (
                <article className="citizen-recent-row" key={report.id}>
                  <span className="citizen-recent-row__icon" aria-hidden="true">
                    <Icon name="file" size={18} />
                  </span>
                  <div className="citizen-recent-row__main">
                    <b>{t(`citizenX.category.${report.category}`)}</b>
                    <span className="citizen-recent-row__ref">
                      {t("citizenX.tracking.reference")}: <span title={report.id}>{report.id}</span>
                    </span>
                    <small>{formatDate(report.submittedAt, locale)}</small>
                  </div>
                  <span className={`badge ${statusTone(report.status)}`}>
                    <i aria-hidden="true" /> {t(`citizenX.status.${report.status}`)}
                  </span>
                  <Link className="citizen-recent-row__link" href={`/my-reports/${report.id}`}>
                    {t("citizenX.tracking.viewDetails")} <Icon name="arrow" size={15} />
                  </Link>
                </article>
              ))}
            </div>
          )}
        </PortalSection>

        <p className="citizen-landing__note">
          <Icon name="bell" size={16} /> {t("citizenX.landing.personalNote")}
        </p>
      </div>
    </AuthGuard>
  );
}
