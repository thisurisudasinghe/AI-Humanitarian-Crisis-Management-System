"use client";

import Link from "next/link";
import { Icon } from "@/components/safeway/Icon";
import { useT } from "@/lib/i18n";
import {
  emergencyContacts
} from "@/lib/publicEmergency";

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
              </article>
            ))}
          </div>
        </section>

        <section className="pw-card pw-card--green pw-callout-panel" aria-labelledby="pw-emergency-request">
          <div>
            <h2 id="pw-emergency-request">{t("pw.emergency.nextTitle")}</h2>
            <p>{t("pw.emergency.nextBody")}</p>
          </div>
          <div className="pw-card__links pw-card__links--row">
            <Link className="btn btn-primary" href="/guest-report">
              {t("pw.emergency.guestReportLink")} <Icon name="arrow" size={16} />
            </Link>
            <Link className="btn btn-outline" href="/track-report">
              {t("pw.emergency.trackLink")} <Icon name="arrow" size={16} />
            </Link>
          </div>
        </section>
      </div>
    </main>
  );
}
