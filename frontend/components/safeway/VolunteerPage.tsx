"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { useT } from "@/lib/i18n";
import { RoleNames } from "@/lib/roles";
import { Icon } from "./Icon";
import { PortalHeader } from "./PortalPrimitives";

export function VolunteerPage({
  eyebrowKey,
  titleKey,
  descriptionKey,
  children,
  action,
  showBack = true
}: {
  eyebrowKey: string;
  titleKey: string;
  descriptionKey: string;
  children: ReactNode;
  action?: ReactNode;
  showBack?: boolean;
}) {
  const t = useT();

  return (
    <AuthGuard allowedRoles={[RoleNames.Volunteer]} activationPurpose="volunteer">
      <div className="volunteer-page">
        {showBack ? (
          <Link className="back-link volunteer-page__back" href="/portal/volunteer">
            <Icon name="arrowLeft" size={16} /> {t("volunteerX.shared.backPortal")}
          </Link>
        ) : null}
        <PortalHeader
          eyebrow={t(eyebrowKey)}
          title={t(titleKey)}
          description={t(descriptionKey)}
          action={action}
        />
        {children}
      </div>
    </AuthGuard>
  );
}
