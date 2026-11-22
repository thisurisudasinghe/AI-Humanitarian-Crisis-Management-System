"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { useAuth } from "@/lib/auth";
import { hasAnyRole } from "@/lib/roles";
import { Brand } from "@/components/safeway/Brand";
import { Icon } from "@/components/safeway/Icon";

export function AuthGuard({
  children,
  allowedRoles
}: {
  children: ReactNode;
  allowedRoles?: readonly string[];
}) {
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
          <p>Checking your session…</p>
        </div>
      </div>
    );
  }

  if (allowedRoles && !hasAnyRole(user.roles, allowedRoles)) {
    return (
      <div className="forbidden-page">
        <div>
          <span className="badge coral">
            <i /> 403 · Forbidden
          </span>
          <h1>This workspace is not available for your role.</h1>
          <p>
            Your account is signed in, but it does not hold one of the roles this page requires.
            Ask a system administrator if you believe you should have access.
          </p>
          <Link className="btn btn-primary" href="/dashboard">
            Back to dashboard <Icon name="arrow" />
          </Link>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
