"use client";

import { useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { useAuth } from "@/lib/auth";
import { hasAnyRole } from "@/lib/roles";
import { Skeleton } from "@/components/ui";

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
    if (!ready) {
      return;
    }

    if (!user) {
      router.replace("/login");
    }
  }, [ready, user, router]);

  if (!ready || !user) {
    return (
      <div className="page">
        <h1>Checking your session…</h1>
        <Skeleton rows={4} />
      </div>
    );
  }

  if (allowedRoles && !hasAnyRole(user.roles, allowedRoles)) {
    return (
      <div className="page">
        <h1>Forbidden</h1>
        <p>Your role does not have access to this page.</p>
      </div>
    );
  }

  return <>{children}</>;
}
