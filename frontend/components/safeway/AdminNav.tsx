"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useT } from "@/lib/i18n";
import { Icon } from "./Icon";

type AdminSection = {
  href: string;
  labelKey: string;
  icon: string;
};

const sections: AdminSection[] = [
  { href: "/admin", labelKey: "opsX.nav.adminLanding", icon: "grid" },
  { href: "/admin/users", labelKey: "common.adminUsers", icon: "users" },
  { href: "/admin/organisations", labelKey: "common.adminOrgs", icon: "building" },
  { href: "/admin/announcements", labelKey: "common.adminAnnouncements", icon: "megaphone" },
  { href: "/admin/audit", labelKey: "common.adminAudit", icon: "clipboard" },
  {
    href: "/admin/notifications",
    labelKey: "common.adminFailedNotifications",
    icon: "bell"
  },
  { href: "/admin/config", labelKey: "common.adminConfig", icon: "shield" }
];

const outbox: AdminSection = {
  href: "/admin/outbox",
  labelKey: "common.adminOutbox",
  icon: "mail"
};

function isActive(pathname: string, href: string): boolean {
  if (href === "/admin") return pathname === "/admin";
  return pathname === href || pathname.startsWith(`${href}/`);
}

export function AdminNav() {
  const pathname = usePathname();
  const t = useT();

  return (
    <nav className="admin-subnav" aria-label={t("common.adminSectionsAria")}>
      <div className="admin-subnav__group">
        {sections.map((section) => {
          const active = isActive(pathname, section.href);
          return (
            <Link
              key={section.href}
              href={section.href}
              className={active ? "admin-subnav__link active" : "admin-subnav__link"}
              aria-current={active ? "page" : undefined}
            >
              <Icon name={section.icon} size={14} />
              <span>{t(section.labelKey)}</span>
            </Link>
          );
        })}
      </div>
      <div className="admin-subnav__group admin-subnav__group--separate">
        <Link
          href={outbox.href}
          className={
            isActive(pathname, outbox.href)
              ? "admin-subnav__link admin-subnav__link--outbox active"
              : "admin-subnav__link admin-subnav__link--outbox"
          }
          aria-current={isActive(pathname, outbox.href) ? "page" : undefined}
        >
          <Icon name={outbox.icon} size={14} />
          <span>{t(outbox.labelKey)}</span>
        </Link>
      </div>
    </nav>
  );
}
