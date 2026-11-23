"use client";

import Link from "next/link";
import { useT } from "@/lib/i18n";
import { publicContact } from "@/lib/publicContact";
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

  return (
    <div className="page-body contact-layout">
      <section className="contact-options" aria-label={t("info.contact.infoTitle")}>
        {publicContact.channels.map((channel) => (
          <article key={channel.id}>
            <i><Icon name={channel.icon} size={19} /></i>
            <span>
              <small>{t("info.contact.detailsKicker")}</small>
              <h3>{t(channel.labelKey)}</h3>
              <p className={channel.value ? undefined : "contact-placeholder"}>
                {channel.value ?? t(channel.placeholderKey)}
              </p>
            </span>
          </article>
        ))}
      </section>

      <section className="contact-info-card">
        <div className="section-kicker">{t("info.contact.quickLinksTitle")}</div>
        <h2>{t("info.contact.infoTitle")}</h2>
        {publicContact.verified ? null : <p>{t("info.contact.detailsNotice")}</p>}
        <div className="contact-info-links">
          <Link className="btn btn-primary" href="/report">
            <Icon name="alert" size={17} /> {t("nav.reportCrisis")}
          </Link>
          <Link className="btn btn-outline" href="/find-assistance">
            <Icon name="search" size={17} /> {t("nav.findAssistance")}
          </Link>
        </div>
        <p className="contact-info-copy">{t("info.contact.quickLinksCopy")}</p>
      </section>
    </div>
  );
}
