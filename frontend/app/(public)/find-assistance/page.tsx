"use client";

import Link from "next/link";
import { Icon } from "@/components/safeway/Icon";
import { PublicCaseUpdates } from "@/components/safeway/PublicCaseUpdates";
import { useT } from "@/lib/i18n";

export default function FindAssistancePage() {
  const t = useT();
  const assistance: [string, string, string][] = [
    [t("info.services.s2Title") === "Verified assistance" ? "Emergency shelter" : (t("find.shelter") || "Emergency shelter"), t("find.shelterCopy") || "Verified safe spaces, occupancy and accessibility information.", "home"],
    [t("find.food") || "Food & water", t("find.foodCopy") || "Distribution points, opening hours and current availability.", "box"],
    [t("find.medical") || "Medical support", t("find.medicalCopy") || "Clinics, first-aid points and urgent care guidance.", "heart"],
    [t("find.transport") || "Transport", t("find.transportCopy") || "Safe routes and transport support for evacuation or appointments.", "map"]
  ];

  return (
    <main className="public-info">
      <header className="page-hero">
        <div className="page-hero-lines" />
        <div className="eyebrow">
          <span /> {t("find.eyebrow") || "Nearby support"}
        </div>
        <h1>{t("find.title") || "Find verified assistance near you."}</h1>
        <p>{t("find.intro") || "Shelters, food, healthcare and essential resources will appear here as live availability becomes connected."}</p>
      </header>

      <div className="page-body">
        <aside className="public-availability-note" role="note">
          <span>
            <Icon name="info" size={20} />
          </span>
          <div>
            <b>{t("find.noteTitle") || "Verified public information"}</b>
            <p>{t("find.noteCopy") || "Published alerts and active service centres are shown below when available. For individual shelter allocation or case status, use the signed-in citizen portal."}</p>
          </div>
        </aside>

        <section className="value-grid" aria-label={t("find.categoriesAria") || "Assistance categories"}>
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
            <small>{t("find.ctaSmall") || "NEED URGENT HELP?"}</small>
            <h2>{t("find.ctaTitle") || "Report what is happening so a response team can review it."}</h2>
          </div>
          <Link className="btn btn-primary" href="/report">
            {t("nav.reportCrisis")} <Icon name="arrow" />
          </Link>
        </section>
      </div>
    </main>
  );
}
