"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import type { ReactNode } from "react";
import { useAuth } from "@/lib/auth";
import { RoleNames, hasAnyRole } from "@/lib/roles";
import { Button } from "@/components/ui";

const kpiRoles = [
  RoleNames.SystemAdmin,
  RoleNames.EmergencyCoordinator,
  RoleNames.PartnerOfficer
];

export function AppShell({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();
  const router = useRouter();

  const admin = user?.roles.includes(RoleNames.SystemAdmin) ?? false;
  const kpis = user ? hasAnyRole(user.roles, kpiRoles) : false;

  async function onLogout() {
    await logout();
    router.push("/login");
  }

  return (
    <div className="app-shell">
      <a className="skip-link" href="#main-content">
        Skip to main content
      </a>

      <header className="app-header">
        <Link href="/dashboard" className="brand">
          Crisis Management
        </Link>
        <div className="app-header__user">
          <span>{user?.fullName ?? "Signed out"}</span>
          <Button variant="secondary" onClick={onLogout}>
            Log out
          </Button>
        </div>
      </header>

      <div className="app-body">
        <nav className="sidebar" aria-label="Main navigation">
          <Link href="/dashboard">Dashboard</Link>
          {kpis ? <Link href="/kpis">KPIs</Link> : null}
          {admin ? <span className="sidebar__heading">Administration</span> : null}
          {admin ? <Link href="/admin/users">Users</Link> : null}
          {admin ? <Link href="/admin/organisations">Organisations</Link> : null}
          {admin ? <Link href="/admin/config">Configuration</Link> : null}
          {admin ? <Link href="/admin/audit">Audit trail</Link> : null}
        </nav>

        <main id="main-content" className="main-content">
          {children}
        </main>
      </div>

      <footer className="app-footer">
        AI-Powered Humanitarian Crisis Management System
      </footer>
    </div>
  );
}
