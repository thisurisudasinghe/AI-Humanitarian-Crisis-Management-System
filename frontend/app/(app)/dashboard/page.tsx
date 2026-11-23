"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { Icon } from "@/components/safeway/Icon";
import { useAuth } from "@/lib/auth";
import { useI18n } from "@/lib/i18n";
import { hasRole, isStaff, RoleNames } from "@/lib/roles";

type PortalCard = {
  purpose: "citizen" | "volunteer" | "donor";
  role: string;
  icon: string;
  labelKey: string;
  titleKey: string;
  copyKey: string;
  openKey: string;
  activateKey: string;
  href: string;
};

const portalCards: PortalCard[] = [
  {
    purpose: "citizen",
    role: RoleNames.Citizen,
    icon: "heartHandshake",
    labelKey: "portalLanding.roles.citizen.label",
    titleKey: "portalLanding.roles.citizen.title",
    copyKey: "portalLanding.roles.citizen.copy",
    openKey: "portalLanding.roles.citizen.open",
    activateKey: "portalLanding.roles.citizen.activate",
    href: "/portal/citizen"
  },
  {
    purpose: "volunteer",
    role: RoleNames.Volunteer,
    icon: "users",
    labelKey: "portalLanding.roles.volunteer.label",
    titleKey: "portalLanding.roles.volunteer.title",
    copyKey: "portalLanding.roles.volunteer.copy",
    openKey: "portalLanding.roles.volunteer.open",
    activateKey: "portalLanding.roles.volunteer.activate",
    href: "/portal/volunteer"
  },
  {
    purpose: "donor",
    role: RoleNames.Donor,
    icon: "handHeart",
    labelKey: "portalLanding.roles.donor.label",
    titleKey: "portalLanding.roles.donor.title",
    copyKey: "portalLanding.roles.donor.copy",
    openKey: "portalLanding.roles.donor.open",
    activateKey: "portalLanding.roles.donor.activate",
    href: "/portal/donor"
  }
];

export default function DashboardPage() {
  const { t } = useI18n();
  const router = useRouter();
  const { user, ready } = useAuth();

  useEffect(() => {
    if (ready && user && isStaff(user.roles)) {
      router.replace("/operations");
    }
  }, [ready, user, router]);

  if (user && isStaff(user.roles)) {
    return (
      <div className="page">
        <section className="card">
          <h1>{t("dashboard.redirect.title")}</h1>
          <p>
            {t("dashboard.redirect.body")}{" "}
            <Link href="/operations">{t("dashboard.redirect.link")}</Link>.
          </p>
        </section>
      </div>
    );
  }

  const roles = user?.roles ?? [];

  return (
    <section className="portal-landing" aria-labelledby="portal-landing-title">
      <div className="portal-landing__hero">
        <span className="portal-landing__eyebrow">
          <Icon name="shield" size={15} /> {t("dashboard.publicPortal")}
        </span>
        <h1 id="portal-landing-title">{t("portalLanding.welcome")}</h1>
        <p>{t("portalLanding.subtitle")}</p>
      </div>

      <div className="portal-landing__cards">
        {portalCards.map((card) => {
          const active = hasRole(roles, card.role);
          return (
            <article
              className={`portal-choice-card portal-choice-card--${card.purpose}`}
              key={card.purpose}
            >
              <div className="portal-choice-card__top">
                <span className="portal-choice-card__icon" aria-hidden="true">
                  <Icon name={card.icon} size={27} />
                </span>
                <span className={`badge ${active ? "green" : "amber"}`}>
                  <i /> {active ? t("portalLanding.status.active") : t("portalLanding.status.needed")}
                </span>
              </div>

              <div className="portal-choice-card__body">
                <small>{t(card.labelKey)}</small>
                <h2>{t(card.titleKey)}</h2>
                <p>{t(card.copyKey)}</p>
              </div>

              <Link
                className={`btn ${active ? "btn-outline" : "btn-primary"} portal-choice-card__action`}
                href={card.href}
              >
                {active ? t(card.openKey) : t(card.activateKey)}
                <Icon name="arrow" size={16} />
              </Link>
            </article>
          );
        })}
      </div>

      <div className="portal-landing__help">
        <span className="portal-landing__help-icon" aria-hidden="true">
          <Icon name="info" size={18} />
        </span>
        <span>{t("portalLanding.help")}</span>
        <Link href="/find-assistance">
          {t("portalLanding.helpLink")} <Icon name="arrow" size={15} />
        </Link>
      </div>
    </section>
  );
}
