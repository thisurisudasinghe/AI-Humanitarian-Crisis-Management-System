"use client";

import Link from "next/link";
import { useState } from "react";
import { useT } from "@/lib/i18n";
import { Icon } from "./Icon";
import { NetworkArtwork } from "./NetworkArtwork";

export type InfoKind = "about" | "services" | "how" | "contact";

export function InfoPageView({ kind }: { kind: InfoKind }) {
  const t = useT();
  const content: Record<InfoKind, [string, string, string]> = {
    about: [t("info.about.eyebrow"), t("info.about.title"), t("info.about.intro")],
    services: [t("info.services.eyebrow"), t("info.services.title"), t("info.services.intro")],
    how: [t("info.how.eyebrow"), t("info.how.title"), t("info.how.intro")],
    contact: [t("info.contact.eyebrow"), t("info.contact.title"), t("info.contact.intro")]
  };
  const [eyebrow, title, intro] = content[kind];

  return (
    <main className="public-info">
      <header className="page-hero">
        <div className="page-hero-lines" />
        <div className="eyebrow">
          <span /> {eyebrow}
        </div>
        <h1>{title}</h1>
        <p>{intro}</p>
      </header>

      {kind === "about" ? <AboutBody /> : null}
      {kind === "services" ? <ServicesBody /> : null}
      {kind === "how" ? <HowBody /> : null}
      {kind === "contact" ? <ContactBody /> : null}
    </main>
  );
}

function AboutBody() {
  const t = useT();
  return (
    <div className="page-body">
      <section className="mission-grid">
        <div>
          <div className="section-kicker">{t("info.about.purposeKicker")}</div>
          <h2>{t("info.about.purposeTitle")}</h2>
        </div>
        <div>
          <p>{t("info.about.purposeP1")}</p>
          <p>{t("info.about.purposeP2")}</p>
        </div>
      </section>

      <section className="value-grid" aria-label={t("info.about.valuesAria")}>
        {(
          [
            [t("info.about.v1Title"), t("info.about.v1Copy"), "eye"],
            [t("info.about.v2Title"), t("info.about.v2Copy"), "shield"],
            [t("info.about.v3Title"), t("info.about.v3Copy"), "map"],
            [t("info.about.v4Title"), t("info.about.v4Copy"), "heart"]
          ] as const
        ).map(([vtitle, copy, icon]) => (
          <article key={vtitle}>
            <i><Icon name={icon} /></i>
            <h3>{vtitle}</h3>
            <p>{copy}</p>
          </article>
        ))}
      </section>

      <section className="human-oversight">
        <NetworkArtwork />
        <div>
          <div className="section-kicker light">{t("info.about.oversightKicker")}</div>
          <h2>{t("info.about.oversightTitle")}</h2>
          <p>{t("info.about.oversightCopy")}</p>
        </div>
      </section>
    </div>
  );
}

function ServicesBody() {
  const t = useT();
  const services: [string, string, string, string][] = [
    [t("info.services.s1Title"), t("info.services.s1Copy"), "alert", t("info.services.publicAccess")],
    [t("info.services.s2Title"), t("info.services.s2Copy"), "search", t("info.services.publicAccess")],
    [t("info.services.s3Title"), t("info.services.s3Copy"), "file", t("info.services.publicAccess")],
    [t("info.services.s4Title"), t("info.services.s4Copy"), "grid", t("info.services.authorizedAccess")],
    [t("info.services.s5Title"), t("info.services.s5Copy"), "box", t("info.services.authorizedAccess")],
    [t("info.services.s6Title"), t("info.services.s6Copy"), "brain", t("info.services.authorizedAccess")]
  ];

  return (
    <div className="page-body">
      <section className="service-catalog" aria-label={t("info.services.catalogAria")}>
        {services.map(([stitle, copy, icon, access], index) => (
          <article key={stitle}>
            <span>0{index + 1}</span>
            <i><Icon name={icon} /></i>
            <small>{access}</small>
            <h2>{stitle}</h2>
            <p>{copy}</p>
          </article>
        ))}
      </section>

      <section className="service-choice">
        <div>
          <small>{t("info.services.choiceSmall")}</small>
          <h2>{t("info.services.choiceTitle")}</h2>
        </div>
        <Link className="btn btn-primary" href="/report">
          {t("nav.reportCrisis")} <Icon name="arrow" />
        </Link>
      </section>
    </div>
  );
}

function HowBody() {
  const t = useT();
  const steps: [string, string, string, string, string][] = [
    ["01", t("info.how.s1Label"), t("info.how.s1Title"), t("info.how.s1Copy"), "alert"],
    ["02", t("info.how.s2Label"), t("info.how.s2Title"), t("info.how.s2Copy"), "shield"],
    ["03", t("info.how.s3Label"), t("info.how.s3Title"), t("info.how.s3Copy"), "brain"],
    ["04", t("info.how.s4Label"), t("info.how.s4Title"), t("info.how.s4Copy"), "users"],
    ["05", t("info.how.s5Label"), t("info.how.s5Title"), t("info.how.s5Copy"), "bell"]
  ];

  return (
    <div className="page-body">
      <section className="process-list" aria-label={t("info.how.processAria")}>
        {steps.map(([number, label, ptitle, copy, icon]) => (
          <article key={number}>
            <span>{number}</span>
            <i><Icon name={icon} /></i>
            <div>
              <small>{label}</small>
              <h2>{ptitle}</h2>
              <p>{copy}</p>
            </div>
          </article>
        ))}
      </section>

      <section className="process-cta">
        <div>
          <Icon name="shield" size={30} />
          <span>
            <small>{t("info.how.principleSmall")}</small>
            <h2>{t("info.how.principleTitle")}</h2>
          </span>
        </div>
        <Link className="btn btn-white" href="/report">
          {t("nav.reportCrisis")} <Icon name="arrow" />
        </Link>
      </section>
    </div>
  );
}

function ContactBody() {
  const t = useT();
  const [sent, setSent] = useState(false);

  return (
    <div className="page-body contact-layout">
      <section className="contact-options">
        {(
          [
            ["phone", t("info.contact.c1Label"), t("info.contact.c1Title"), t("info.contact.c1Copy")],
            ["file", t("info.contact.c2Label"), t("info.contact.c2Title"), t("info.contact.c2Copy")],
            ["shield", t("info.contact.c3Label"), t("info.contact.c3Title"), t("info.contact.c3Copy")]
          ] as const
        ).map(([icon, label, ctitle, copy]) => (
          <article key={label}>
            <i><Icon name={icon} /></i>
            <span>
              <small>{label}</small>
              <h3>{ctitle}</h3>
              <p>{copy}</p>
            </span>
          </article>
        ))}
      </section>

      <section className="contact-form">
        {sent ? (
          <div className="contact-success" aria-live="polite">
            <i><Icon name="check" size={30} /></i>
            <h2>{t("info.contact.sentTitle")}</h2>
            <p>{t("info.contact.sentCopy")}</p>
            <button type="button" onClick={() => setSent(false)}>{t("info.contact.writeAnother")}</button>
          </div>
        ) : (
          <form onSubmit={(event) => { event.preventDefault(); setSent(true); }}>
            <div className="section-kicker">{t("info.contact.formKicker")}</div>
            <h2>{t("info.contact.formTitle")}</h2>
            <div className="two-fields">
              <label>
                <span className="field-label">{t("info.contact.firstName")}</span>
                <input placeholder={t("info.contact.firstNamePh")} autoComplete="given-name" required />
              </label>
              <label>
                <span className="field-label">{t("info.contact.lastName")}</span>
                <input placeholder={t("info.contact.lastNamePh")} autoComplete="family-name" required />
              </label>
            </div>
            <label>
              <span className="field-label">{t("info.contact.email")}</span>
              <input type="email" placeholder={t("info.contact.emailPh")} autoComplete="email" required />
            </label>
            <label>
              <span className="field-label">{t("info.contact.topicLabel")}</span>
              <select defaultValue={t("info.contact.topicGeneral")}>
                <option>{t("info.contact.topicGeneral")}</option>
                <option>{t("info.contact.topicReport")}</option>
                <option>{t("info.contact.topicAccount")}</option>
              </select>
            </label>
            <label>
              <span className="field-label">{t("info.contact.messageLabel")}</span>
              <textarea rows={5} placeholder={t("info.contact.messagePh")} required />
            </label>
            <button className="btn btn-primary" type="submit">
              {t("info.contact.send")} <Icon name="arrow" />
            </button>
          </form>
        )}
      </section>
    </div>
  );
}
