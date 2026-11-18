import type { ReactNode } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { DashboardShell } from "@/components/safeway/DashboardShell";

export default function ProtectedLayout({ children }: { children: ReactNode }) {
  return (
    <AuthGuard>
      <DashboardShell>{children}</DashboardShell>
    </AuthGuard>
  );
}
