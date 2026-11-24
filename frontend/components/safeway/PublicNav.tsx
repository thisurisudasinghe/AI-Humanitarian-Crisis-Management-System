"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";
import { useAuth } from "@/lib/auth";
import { useT } from "@/lib/i18n";
import { isStaff } from "@/lib/roles";
import { LanguageSwitcher } from "@/components/i18n/LanguageSwitcher";
import { Icon } from "./Icon";
import { PublicBrand } from "./PublicBrand";

const publicLinks = [
  { key: "nav.home", href: "/", icon: "home" },
  { key: "nav.about", href: "/about", icon: "info" },
  { key: "nav.services", href: "/services", icon: "layers" },
  { key: "nav.howItWorks", href: "/how-it-works", icon: "route" },
  { key: "nav.liveMap", href: "/live-map", icon: "layers" }
] as const;

export function PublicNav() {
  const pathname = usePathname();
  const { user, ready } = useAuth();
  const t = useT();
  const [open, setOpen] = useState(false);

  const workspaceHref =
    ready && user ? (isStaff(user.roles) ? "/operations" : "/dashboard") : "/login";
  const workspaceLabel = ready && user ? t("nav.dashboard") : t("nav.login");

  useEffect(() => {
    setOpen(false);
  }, [pathname]);

  useEffect(() => {
    if (!open) return;

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") setOpen(false);
    }

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [open]);

  function isActive(href: string) {
    if (href === "/") return pathname === "/";
    return pathname === href || pathname.startsWith(`${href}/`);
  }

  return (
    <header className="platform-nav public-nav">
      <PublicBrand compact />

      <button
        className="platform-menu public-menu"
        type="button"
        aria-label={open ? t("common.closeMenu") : t("common.openMenu")}
        aria-controls="public-navigation"
        aria-expanded={open}
        onClick={() => setOpen((value) => !value)}
      >
        <Icon name={open ? "close" : "menu"} size={22} />
      </button>

      <div id="public-navigation" className={`public-nav__panel${open ? " is-open" : ""}`}>
        <nav className="platform-links public-nav__links" aria-label={t("nav.primaryAria")}>
          {publicLinks.map((link) => {
            const active = isActive(link.href);
            return (
              <Link
                key={link.href}
                href={link.href}
                className={active ? "active" : ""}
                aria-current={active ? "page" : undefined}
                onClick={() => setOpen(false)}
              >
                <Icon name={link.icon} size={16} />
                <span>{t(link.key)}</span>
              </Link>
            );
          })}
        </nav>

        <div className="public-nav__actions">
          <LanguageSwitcher variant="nav" dropdown />
          <Link className="platform-login public-action public-action--login" href={workspaceHref}>
            <Icon name="user" size={17} />
            <span>{workspaceLabel}</span>
          </Link>
          <Link className="btn btn-primary public-action public-action--report" href="/report">
            <Icon name="alert" size={17} />
            <span>{t("nav.reportCrisis")}</span>
          </Link>
        </div>
      </div>
    </header>
  );
}
