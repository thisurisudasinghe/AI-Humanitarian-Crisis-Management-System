"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useT } from "@/lib/i18n";

export function AdminNav() {
  const pathname = usePathname();
  const t = useT();
  const sections = [
    { href: "/admin/users", label: t("common.adminUsers") },
    { href: "/admin/organisations", label: t("common.adminOrgs") },
    { href: "/admin/config", label: t("common.adminConfig") },
    { href: "/admin/announcements", label: t("common.adminAnnouncements") },
    { href: "/admin/audit", label: t("common.adminAudit") },
    { href: "/admin/notifications", label: t("common.adminFailedNotifications") },
    { href: "/admin/outbox", label: t("common.adminOutbox") }
  ];

  return (
    <nav className="admin-tabs" aria-label={t("common.adminSectionsAria")}>
      {sections.map((section) => (
        <Link
          key={section.href}
          href={section.href}
          className={pathname === section.href ? "active" : ""}
          aria-current={pathname === section.href ? "page" : undefined}
        >
          {section.label}
        </Link>
      ))}
    </nav>
  );
}
