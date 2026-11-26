"use client";

import Link from "next/link";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import { useT } from "@/lib/i18n";
import { RoleNames } from "@/lib/roles";

type AdminCard = {
  href: string;
  icon: string;
  titleKey: string;
  descriptionKey: string;
};

const cards: AdminCard[] = [
  {
    href: "/admin/users",
    icon: "users",
    titleKey: "opsX.admin.usersTitle",
    descriptionKey: "opsX.admin.usersDescription"
  },
  {
    href: "/admin/organisations",
    icon: "building",
    titleKey: "opsX.admin.orgsTitle",
    descriptionKey: "opsX.admin.orgsDescription"
  },
  {
    href: "/admin/announcements",
    icon: "megaphone",
    titleKey: "opsX.admin.announcementsTitle",
    descriptionKey: "opsX.admin.announcementsDescription"
  },
  {
    href: "/admin/audit",
    icon: "clipboard",
    titleKey: "opsX.admin.auditTitle",
    descriptionKey: "opsX.admin.auditDescription"
  },
  {
    href: "/admin/notifications",
    icon: "bell",
    titleKey: "opsX.admin.notificationsTitle",
    descriptionKey: "opsX.admin.notificationsDescription"
  },
  {
    href: "/admin/config",
    icon: "shield",
    titleKey: "opsX.admin.settingsTitle",
    descriptionKey: "opsX.admin.settingsDescription"
  }
];

export default function AdminPage() {
  const t = useT();

  return (
    <AuthGuard allowedRoles={[RoleNames.SystemAdmin]}>
      <div className="page admin-landing">
        <header className="page__header">
          <div>
            <h1>{t("opsX.admin.title")}</h1>
            <p>{t("opsX.admin.subtitle")}</p>
          </div>
        </header>

        <section aria-labelledby="admin-general-heading">
          <h2 className="admin-landing__heading" id="admin-general-heading">
            {t("opsX.admin.generalLabel")}
          </h2>
          <div className="admin-landing__grid">
            {cards.map((card) => (
              <Link className="admin-landing__card" href={card.href} key={card.href}>
                <span className="admin-landing__icon">
                  <Icon name={card.icon} size={19} />
                </span>
                <span className="admin-landing__copy">
                  <b>{t(card.titleKey)}</b>
                  <small>{t(card.descriptionKey)}</small>
                </span>
                <span className="admin-landing__arrow">
                  <Icon name="arrow" size={16} />
                </span>
              </Link>
            ))}
          </div>
        </section>

        <section aria-labelledby="admin-oversight-heading" className="admin-landing__section">
          <h2 className="admin-landing__heading" id="admin-oversight-heading">
            {t("opsX.admin.notificationsLabel")}
          </h2>
          <div className="admin-landing__grid">
            <Link className="admin-landing__card admin-landing__card--outbox" href="/admin/outbox">
              <span className="admin-landing__icon">
                <Icon name="mail" size={19} />
              </span>
              <span className="admin-landing__copy">
                <b>{t("common.adminOutbox")}</b>
                <small>{t("admin.outbox.subtitle")}</small>
              </span>
              <span className="admin-landing__arrow">
                <Icon name="arrow" size={16} />
              </span>
            </Link>
          </div>
        </section>
      </div>
    </AuthGuard>
  );
}
