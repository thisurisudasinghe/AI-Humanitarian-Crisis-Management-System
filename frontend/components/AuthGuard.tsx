"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { useAuth } from "@/lib/auth";
import { hasAnyRole } from "@/lib/roles";
import { Brand } from "@/components/safeway/Brand";
import { Icon } from "@/components/safeway/Icon";
import { useT } from "@/lib/i18n";

export function AuthGuard({
  children,
  allowedRoles
}: {
  children: ReactNode;
  allowedRoles?: readonly string[];
}) {
  const t = useT();
  const { user, ready } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (ready && !user) {
      router.replace("/login");
    }
  }, [ready, user, router]);

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
