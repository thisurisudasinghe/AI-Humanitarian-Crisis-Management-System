"use client";

import Link from "next/link";
import { useT } from "@/lib/i18n";
import { publicContact } from "@/lib/publicContact";
import { Icon } from "./Icon";
import { PublicBrand } from "./PublicBrand";

export function PublicFooter() {
  const t = useT();
  return (
    <footer className="public-footer">
      <div className="public-footer__main">
        <div className="public-footer__brand">
          <PublicBrand light />
          <p>{t("nav.footerTagline")}</p>
        </div>

        <nav className="public-footer__links" aria-label={t("nav.footerLinksAria")}>
          <Link href="/about">{t("nav.about")}</Link>
          <Link href="/services">{t("nav.services")}</Link>
          <Link href="/contact">{t("nav.contact")}</Link>
        </nav>

        <address className="public-footer__contact">
          {publicContact.channels.map((channel) => (
            <a key={channel.id} href={channel.href}>
              <Icon name={channel.icon} size={15} />
              <span>{channel.displayValue}</span>
            </a>
          ))}
        </address>
      </div>

      <div className="public-footer__bottom">
        <span>{t("nav.footerCopyright")}</span>
      </div>
    </footer>
  );
}
