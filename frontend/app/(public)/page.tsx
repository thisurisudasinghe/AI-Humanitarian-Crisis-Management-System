"use client";

import Link from "next/link";
import { Icon } from "@/components/safeway/Icon";
import { NetworkArtwork } from "@/components/safeway/NetworkArtwork";
import { useT } from "@/lib/i18n";

export default function HomePage() {
  const t = useT();
  const trustPoints: [string, string, string][] = [
    ["shield", t("home.trust1Title"), t("home.trust1Copy")],
    ["brain", t("home.trust2Title"), t("home.trust2Copy")],
    ["heart", t("home.trust3Title"), t("home.trust3Copy")]
  ];
  const steps: [string, string, string, string][] = [
    ["01", t("home.step1Title"), t("home.step1Copy"), "alert"],
    ["02", t("home.step2Title"), t("home.step2Copy"), "shield"],
    ["03", t("home.step3Title"), t("home.step3Copy"), "users"]
  ];

  return (
    <main className="public-home">
      <section className="centered-hero public-hero">
        <div className="hero-orbit hero-orbit-left" />
        <div className="hero-orbit hero-orbit-right" />
        <div className="centered-hero-copy">
          <div className="eyebrow"><span /> {t("home.eyebrow")}</div>
          <h1>
            <span className="public-hero__line">{t("home.heroTitle1")}</span>{" "}
            <span className="public-hero__line">{t("home.heroTitle2")} <em>{t("home.heroTitleEm")}</em></span>
          </h1>
          <p>{t("home.heroCopy")}</p>
          <div className="centered-actions">
            <Link className="btn btn-white public-hero__cta" href="/report">
              <Icon name="alert" size={18} />
              {t("nav.reportCrisis")}
              <Icon name="arrow" size={18} />
            </Link>
          </div>
          <p className="public-hero__emergency">
            <Icon name="info" size={15} />
            {t("home.heroEmergency")}
          </p>
        </div>
        <NetworkArtwork />
        <div className="hero-trust-bar">
          {trustPoints.map(([icon, title, copy]) => (
            <span key={title}><Icon name={icon} /><b>{title}</b><small>{copy}</small></span>
          ))}
        </div>
      </section>

      <div className="public-hero__fade" aria-hidden="true" />

      <section className="home-intro">
        <div>
          <div className="section-kicker">{t("home.sectionKicker")}</div>
          <h2>{t("home.sectionTitle")}<span>{t("home.sectionTitleSpan")}</span></h2>
        </div>
        <div><p>{t("home.sectionCopy")}</p></div>
      </section>

      <section className="home-pathways" aria-label={t("public.home.pathwaysAria")}>
        <article className="pathway pathway-public">
          <span>{t("home.forCommunities")}</span>
          <i><Icon name="heart" /></i>
          <h3>{t("home.communityTitle")}</h3>
          <p>{t("home.communityCopy")}</p>
        </article>
        <article className="pathway pathway-operations">
          <span>{t("home.forTeams")}</span>
          <i><Icon name="grid" /></i>
          <h3>{t("home.teamTitle")}</h3>
          <p>{t("home.teamCopy")}</p>
        </article>
      </section>

      <section className="home-steps">
        <div className="home-steps-heading">
          <div className="section-kicker light">{t("home.howMoves")}</div>
          <h2>{t("home.threeSteps")}<br />{t("home.oneResponse")}</h2>
        </div>
        {steps.map(([number, title, copy, icon]) => (
          <article key={number}>
            <span>{number}</span>
            <i><Icon name={icon} /></i>
            <h3>{title}</h3>
            <p>{copy}</p>
          </article>
        ))}
      </section>

      <section className="home-cta public-guidance" aria-label={t("public.home.guidanceAria")}>
        <span><Icon name="info" size={26} /></span>
        <div>
          <small>{t("home.guidanceSmall")}</small>
          <h2>{t("home.guidanceTitle")}</h2>
          <p>{t("home.guidanceCopy")}</p>
        </div>
      </section>
    </main>
  );
}
