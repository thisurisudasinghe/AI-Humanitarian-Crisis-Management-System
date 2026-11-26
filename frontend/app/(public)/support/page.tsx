"use client";

import Link from "next/link";
import { Icon } from "@/components/safeway/Icon";
import { useT } from "@/lib/i18n";

interface GuideLink {
  href: string;
  labelKey: string;
}

interface GuideCard {
  icon: string;
  accent: string;
  titleKey: string;
  bodyKey: string;
  links: GuideLink[];
}

const GUIDE_CARDS: GuideCard[] = [
  {
    icon: "user",
    accent: "blue",
    titleKey: "pw.support.accountTitle",
    bodyKey: "pw.support.accountBody",
    links: [
      { href: "/login", labelKey: "pw.support.loginLink" },
      { href: "/register", labelKey: "pw.support.registerLink" },
      { href: "/account", labelKey: "pw.support.accountSettingsLink" }
    ]
  },
  {
    icon: "shield",
    accent: "coral",
    titleKey: "pw.support.crisisTitle",
    bodyKey: "pw.support.crisisBody",
    links: [
      { href: "/emergency-services", labelKey: "pw.support.emergencyLink" },
      { href: "/report", labelKey: "pw.support.reportLink" },
      { href: "/guest-report", labelKey: "pw.support.guestReportLink" }
    ]
  },
  {
    icon: "pin",
    accent: "green",
    titleKey: "pw.support.assistanceTitle",
    bodyKey: "pw.support.assistanceBody",
    links: [
      { href: "/find-assistance", labelKey: "pw.support.findAssistanceLink" },
      { href: "/public-alerts", labelKey: "pw.support.alertsLink" }
    ]
  },
  {
    icon: "clipboard",
    accent: "amber",
    titleKey: "pw.support.trackingTitle",
    bodyKey: "pw.support.trackingBody",
    links: [
      { href: "/my-reports", labelKey: "pw.support.myReportsLink" },
      { href: "/track-report", labelKey: "pw.support.guestTrackLink" }
    ]
  },
  {
    icon: "users",
    accent: "blue",
    titleKey: "pw.support.volunteerTitle",
    bodyKey: "pw.support.volunteerBody",
    links: [
      { href: "/register", labelKey: "pw.support.volunteerRegisterLink" },
      { href: "/contact", labelKey: "pw.support.contactLink" }
    ]
  },
  {
    icon: "heart",
    accent: "green",
    titleKey: "pw.support.donorTitle",
    bodyKey: "pw.support.donorBody",
    links: [
      { href: "/register", labelKey: "pw.support.donorRegisterLink" },
      { href: "/contact", labelKey: "pw.support.contactLink" }
    ]
  },
  {
    icon: "file",
    accent: "amber",
    titleKey: "pw.support.staffTitle",
    bodyKey: "pw.support.staffBody",
    links: [
      { href: "/staff-access", labelKey: "pw.support.staffLink" },
      { href: "/staff-access/status", labelKey: "pw.support.staffStatusLink" }
    ]
  },
  {
    icon: "help-circle",
    accent: "blue",
    titleKey: "pw.support.faqTitle",
    bodyKey: "pw.support.faqBody",
    links: [
      { href: "/how-it-works", labelKey: "pw.support.howItWorksLink" },
      { href: "/contact", labelKey: "pw.support.contactLink" }
    ]
  }
];

export default function SupportPage() {
  const t = useT();

  return (
    <main className="public-info pw-page">
      <header className="page-hero">
        <div className="page-hero-lines" />
        <div className="eyebrow">
          <span /> {t("pw.support.eyebrow")}
        </div>
        <h1>{t("pw.support.title")}</h1>
        <p>{t("pw.support.intro")}</p>
      </header>

      <div className="page-body">
        <aside className="pw-callout pw-callout--coral" role="note">
          <span className="pw-callout__icon" aria-hidden="true">
            <Icon name="alert" size={22} />
          </span>
          <div>
            <b>{t("pw.support.urgentTitle")}</b>
            <p>{t("pw.support.urgentBody")}</p>
          </div>
          <Link className="btn btn-outline" href="/emergency-services">
            {t("pw.support.emergencyLink")} <Icon name="arrow" size={16} />
          </Link>
        </aside>

        <section className="pw-section" aria-labelledby="pw-support-paths">
          <div className="pw-section__head">
            <h2 id="pw-support-paths">{t("pw.support.pathsTitle")}</h2>
            <p>{t("pw.support.pathsBody")}</p>
          </div>
          <div className="pw-grid">
            {GUIDE_CARDS.map((card) => (
              <article className={`pw-card pw-card--${card.accent}`} key={card.titleKey}>
                <span className="pw-card__icon" aria-hidden="true">
                  <Icon name={card.icon} size={22} />
                </span>
                <h3>{t(card.titleKey)}</h3>
                <p>{t(card.bodyKey)}</p>
                <div className="pw-card__links">
                  {card.links.map((link) => (
                    <Link href={link.href} key={`${card.titleKey}-${link.href}`}>
                      {t(link.labelKey)} <Icon name="arrow" size={15} />
                    </Link>
                  ))}
                </div>
              </article>
            ))}
          </div>
        </section>

        <section className="pw-card pw-card--blue pw-callout-panel" aria-labelledby="pw-support-contact">
          <span className="pw-card__icon" aria-hidden="true">
            <Icon name="mail" size={22} />
          </span>
          <div>
            <h2 id="pw-support-contact">{t("pw.support.contactTitle")}</h2>
            <p>{t("pw.support.contactBody")}</p>
          </div>
          <Link className="btn btn-primary" href="/contact">
            {t("pw.support.contactLink")} <Icon name="arrow" size={16} />
          </Link>
        </section>
      </div>
    </main>
  );
}
