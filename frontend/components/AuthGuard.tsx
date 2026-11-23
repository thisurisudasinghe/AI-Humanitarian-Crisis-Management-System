"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState, type ReactNode } from "react";
import { PortalNotice } from "@/components/safeway/PortalPrimitives";
import { Brand } from "@/components/safeway/Brand";
import { Icon } from "@/components/safeway/Icon";
import { useAuth } from "@/lib/auth";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useT } from "@/lib/i18n";
import { hasAnyRole, type PublicPurpose } from "@/lib/roles";

const activationActionKeys: Record<PublicPurpose, string> = {
  citizen: "portalLanding.roles.citizen.activate",
  volunteer: "portalLanding.roles.volunteer.activate",
  donor: "portalLanding.roles.donor.activate"
};

const activationRoleKeys: Record<PublicPurpose, string> = {
  citizen: "roleLabels.Citizen",
  volunteer: "roleLabels.Volunteer",
  donor: "roleLabels.Donor"
};

export function AuthGuard({
  children,
  allowedRoles,
  activationPurpose
}: {
  children: ReactNode;
  allowedRoles?: readonly string[];
  activationPurpose?: PublicPurpose;
}) {
  const t = useT();
  const { user, ready, enroll } = useAuth();
  const router = useRouter();
  const [activating, setActivating] = useState(false);
  const [activationError, setActivationError] = useState<string | null>(null);

  useEffect(() => {
    if (ready && !user) {
      router.replace("/login");
    }
  }, [ready, user, router]);

  async function onActivate() {
    if (!activationPurpose) return;
    setActivating(true);
    setActivationError(null);

    try {
      const updated = await enroll(activationPurpose);
      if (allowedRoles && !hasAnyRole(updated.roles, allowedRoles)) {
        setActivationError(t("authGuard.activationFailed"));
      }
    } catch (cause) {
      setActivationError(localizedErrorMessage(cause, t));
    } finally {
      setActivating(false);
    }
  }

  if (!ready || !user) {
    return (
      <div className="loading-screen">
        <div>
          <Brand />
          <div className="skeleton-block" style={{ marginTop: 24 }} />
          <p>{t("authGuard.checking")}</p>
        </div>
      </div>
    );
  }

  if (allowedRoles && !hasAnyRole(user.roles, allowedRoles)) {
    if (activationPurpose) {
      return (
        <div className="forbidden-page">
          <div className="activation-card">
            <span className="badge coral">
              <i /> {t("authGuard.activationBadge")}
            </span>
            <h1>{t("authGuard.activationTitle")}</h1>
            <p>
              {t("authGuard.activationCopy", {
                role: t(activationRoleKeys[activationPurpose])
              })}
            </p>
            {activationError ? <PortalNotice tone="error">{activationError}</PortalNotice> : null}
            <div className="activation-card__actions">
              <button
                className="btn btn-primary"
                type="button"
                disabled={activating}
                onClick={() => void onActivate()}
              >
                {activating ? t("authGuard.activating") : t(activationActionKeys[activationPurpose])}
                {activating ? null : <Icon name="arrow" size={16} />}
              </button>
              <Link className="btn btn-outline" href="/dashboard">
                <Icon name="arrowLeft" size={16} /> {t("common.backToPortals")}
              </Link>
            </div>
          </div>
        </div>
      );
    }

    return (
      <div className="forbidden-page">
        <div>
          <span className="badge coral">
            <i /> {t("authGuard.forbiddenBadge")}
          </span>
          <h1>{t("authGuard.title")}</h1>
          <p>{t("authGuard.copy")}</p>
          <Link className="btn btn-primary" href="/dashboard">
            {t("authGuard.back")} <Icon name="arrow" />
          </Link>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
