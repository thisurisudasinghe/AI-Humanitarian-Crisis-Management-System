"use client";

import Link from "next/link";
import { Icon } from "@/components/safeway/Icon";
import { PublicCaseUpdates } from "@/components/safeway/PublicCaseUpdates";
import { useT } from "@/lib/i18n";

export default function FindAssistancePage() {
  const t = useT();
  const assistance: [string, string, string][] = [
    [t("info.services.s2Title") === t("public.find.verifiedAssistance") ? t("public.find.emergencyShelter") : (t("find.shelter") || t("public.find.emergencyShelter")), t("find.shelterCopy") || t("public.find.shelterCopyFallback"), "home"],
    [t("find.food") || t("public.find.foodFallback"), t("find.foodCopy") || t("public.find.foodCopyFallback"), "box"],
    [t("find.medical") || t("public.find.medicalFallback"), t("find.medicalCopy") || t("public.find.medicalCopyFallback"), "heart"],
    [t("find.transport") || t("public.find.transportFallback"), t("find.transportCopy") || t("public.find.transportCopyFallback"), "map"]
  ];

  return (
    <main className="public-info">
      <header className="page-hero">
        <div className="page-hero-lines" />
        <div className="eyebrow">
          <span /> {t("find.eyebrow") || t("public.find.eyebrowFallback")}
        </div>
        <h1>{t("find.title") || t("public.find.titleFallback")}</h1>
        <p>{t("find.intro") || t("public.find.introFallback")}</p>
      </header>

      <div className="page-body">
        <aside className="public-availability-note" role="note">
          <span>
            <Icon name="info" size={20} />
          </span>
          <div>
            <b>{t("find.noteTitle") || t("public.find.noteTitleFallback")}</b>
            <p>{t("find.noteCopy") || t("public.find.noteCopyFallback")}</p>
          </div>
        </aside>

        <section className="value-grid" aria-label={t("find.categoriesAria") || t("public.find.categoriesAriaFallback")}>
          {assistance.map(([title, copy, icon]) => (
            <article key={title}>
              <i><Icon name={icon} /></i>
              <h3>{title}</h3>
              <p>{copy}</p>
            </article>
          ))}
        </section>

        <PublicCaseUpdates />

        <section className="service-choice public-single-choice">
          <div>
            <small>{t("find.ctaSmall") || t("public.find.ctaSmallFallback")}</small>
            <h2>{t("find.ctaTitle") || t("public.find.ctaTitleFallback")}</h2>
          </div>
          <Link className="btn btn-primary" href="/report">
            {t("nav.reportCrisis")} <Icon name="arrow" />
          </Link>
        </section>
      </div>
    </main>
  );
}
