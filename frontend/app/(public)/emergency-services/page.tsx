"use client";

import Link from "next/link";
import { Icon } from "@/components/safeway/Icon";
import { useT } from "@/lib/i18n";
import {
  emergencyContacts,
  emergencySourceList,
  emergencySourcesFor
} from "@/lib/publicEmergency";

const SAFETY_STEPS = [
  { icon: "phone", key: "pw.emergency.stepCall" },
  { icon: "shield", key: "pw.emergency.stepSafe" },
  { icon: "info", key: "pw.emergency.stepDetails" },
  { icon: "users", key: "pw.emergency.stepFollow" }
];

export default function EmergencyServicesPage() {
  const t = useT();

  return (
    <main className="public-info pw-page">
      <header className="page-hero">
        <div className="page-hero-lines" />
        <div className="eyebrow">
          <span /> {t("pw.emergency.eyebrow")}
        </div>
        <h1>{t("pw.emergency.title")}</h1>
        <p>{t("pw.emergency.intro")}</p>
      </header>

      <div className="page-body">
        <aside className="pw-callout pw-callout--coral" role="note">
          <span className="pw-callout__icon" aria-hidden="true">
            <Icon name="alert" size={22} />
          </span>
          <div>
            <b>{t("pw.emergency.limitationTitle")}</b>
            <p>{t("pw.emergency.limitation")}</p>
          </div>
        </aside>

        <section className="pw-section" aria-labelledby="pw-emergency-contacts">
          <div className="pw-section__head">
            <h2 id="pw-emergency-contacts">{t("pw.emergency.contactsTitle")}</h2>
            <p>{t("pw.emergency.contactsBody")}</p>
          </div>

          <div className="pw-grid pw-grid--contacts">
            {emergencyContacts.map((contact) => (
              <article className="pw-card pw-card--coral pw-contact-card" key={contact.id}>
                <span className="pw-card__icon" aria-hidden="true">
                  <Icon name={contact.icon} size={22} />
                </span>
                <h3>{t(contact.labelKey)}</h3>
                <p>{t(contact.descriptionKey)}</p>
                <a className="pw-contact-card__number" href={contact.primary.href}>
                  <Icon name="phone" size={18} />
                  <strong>{contact.primary.display}</strong>
                  <span className="pw-contact-card__call">{t("pw.emergency.call")}</span>
                </a>
                {contact.fallbacks.length > 0 ? (
                  <div className="pw-contact-card__fallbacks">
                    <b>{t("pw.emergency.fallbackTitle")}</b>
                    {contact.fallbacks.map((fallback) => (
                      <a href={fallback.href} key={fallback.href}>
                        <Icon name="phone" size={14} /> {fallback.display}
                      </a>
                    ))}
                    <small>{t("pw.emergency.fallbackNote")}</small>
                  </div>
                ) : null}
                <div className="pw-contact-card__sources">
                  {emergencySourcesFor(contact).map((source) => (
                    <a href={source.url} key={source.id} target="_blank" rel="noreferrer">
                      {source.name} <Icon name="arrow" size={13} />
                    </a>
                  ))}
                </div>
              </article>
            ))}
          </div>
        </section>

        <section className="pw-section" aria-labelledby="pw-emergency-safety">
          <div className="pw-section__head">
            <h2 id="pw-emergency-safety">{t("pw.emergency.safetyTitle")}</h2>
            <p>{t("pw.emergency.safetyBody")}</p>
          </div>
          <div className="pw-grid pw-grid--steps">
            {SAFETY_STEPS.map((step) => (
              <article className="pw-card pw-card--blue pw-step-card" key={step.key}>
                <span className="pw-card__icon" aria-hidden="true">
                  <Icon name={step.icon} size={20} />
                </span>
                <p>{t(step.key)}</p>
              </article>
            ))}
          </div>
        </section>

        <section className="pw-card pw-card--green pw-callout-panel" aria-labelledby="pw-emergency-next">
          <div>
            <h2 id="pw-emergency-next">{t("pw.emergency.nextTitle")}</h2>
            <p>{t("pw.emergency.nextBody")}</p>
          </div>
          <div className="pw-card__links pw-card__links--row">
            <Link className="btn btn-primary" href="/report">
              {t("pw.emergency.reportLink")} <Icon name="arrow" size={16} />
            </Link>
            <Link className="btn btn-outline" href="/guest-report">
              {t("pw.emergency.guestReportLink")} <Icon name="arrow" size={16} />
            </Link>
            <Link className="btn btn-outline" href="/track-report">
              {t("pw.emergency.trackLink")} <Icon name="arrow" size={16} />
            </Link>
          </div>
        </section>

        <section className="pw-section" aria-labelledby="pw-emergency-sources">
          <div className="pw-section__head">
            <h2 id="pw-emergency-sources">{t("pw.emergency.sourcesTitle")}</h2>
            <p>{t("pw.emergency.sourcesBody")}</p>
          </div>
          <ul className="pw-source-list">
            {emergencySourceList.map((source) => (
              <li key={source.id}>
                <span>{source.name}</span>
                <a href={source.url} target="_blank" rel="noreferrer">
                  {t("pw.emergency.openSource")} <Icon name="arrow" size={14} />
                </a>
              </li>
            ))}
          </ul>
        </section>
      </div>
    </main>
  );
}
