"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

const sections = [
  { href: "/admin/users", label: "Users" },
  { href: "/admin/organisations", label: "Organisations" },
  { href: "/admin/config", label: "Configuration" },
  { href: "/admin/audit", label: "Audit trail" },
  { href: "/admin/outbox", label: "Outbox dead letters" }
];

/** Cross-navigation for the SystemAdmin section. */
export function AdminNav() {
  const pathname = usePathname();

  return (
    <nav className="admin-tabs" aria-label="Administration sections">
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
