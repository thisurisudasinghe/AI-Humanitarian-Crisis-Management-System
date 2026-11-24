"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { useT } from "@/lib/i18n";
import { RoleNames } from "@/lib/roles";
import { Icon } from "./Icon";
import { PortalHeader } from "./PortalPrimitives";

/**
 * Shared wrapper for focused donor screens. Authentication, role activation
 * and back navigation stay identical to the citizen and volunteer portals.
 */
export function DonorPage({
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
    <AuthGuard allowedRoles={[RoleNames.Donor]} activationPurpose="donor">
      <div className="donor-page">
        {showBack ? (
          <Link className="back-link donor-page__back" href="/portal/donor">
            <Icon name="arrowLeft" size={16} /> {t("donorX.shared.backPortal")}
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
