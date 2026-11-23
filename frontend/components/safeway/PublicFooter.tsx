"use client";

import Link from "next/link";
import { useT } from "@/lib/i18n";
import { publicContact } from "@/lib/publicContact";
import { PublicBrand } from "./PublicBrand";

export function PublicFooter() {
  const t = useT();
  return (
    <footer className="public-footer">
      <div className="public-footer__main">
        <div className="public-footer__brand">
          <PublicBrand light />
          <p>{t("nav.footerTagline")}</p>
          <section className="public-footer__contact" aria-label={t("info.contact.infoTitle")}>
            <b>{t("info.contact.infoTitle")}</b>
            {publicContact.channels.map((channel) => (
              <span key={channel.id}>
                {t(channel.labelKey)}: {channel.value ?? t("info.contact.notPublishedPlaceholder")}
              </span>
            ))}
            <Link href="/contact">{t("nav.contact")}</Link>
          </section>
        </div>

        <nav aria-label={t("nav.footerExploreAria")}>
          <b>{t("nav.explore")}</b>
          <Link href="/about">{t("nav.aboutSafeway")}</Link>
          <Link href="/services">{t("nav.services")}</Link>
          <Link href="/how-it-works">{t("nav.howItWorksLink")}</Link>
        </nav>

        <nav aria-label={t("nav.footerHelpAria")}>
          <b>{t("nav.getHelp")}</b>
          <Link href="/report">{t("nav.reportCrisis")}</Link>
          <Link href="/find-assistance">{t("nav.findAssistance")}</Link>
          <Link href="/contact">{t("nav.contact")}</Link>
        </nav>
      </div>

      <div className="public-footer__bottom">
        <span>{t("nav.footerCopyright")}</span>
        <Link href="/contact">{t("nav.contactSupport")}</Link>
      </div>
    </footer>
  );
}
