"use client";

import Link from "next/link";
import { Badge } from "@/components/ui";
import { useAuth } from "@/lib/auth";
import { RoleNames, hasAnyRole } from "@/lib/roles";

const kpiRoles = [
  RoleNames.SystemAdmin,
  RoleNames.EmergencyCoordinator,
  RoleNames.PartnerOfficer
];

export default function DashboardPage() {
  const { user } = useAuth();
  const admin = user?.roles.includes(RoleNames.SystemAdmin) ?? false;
  const kpis = user ? hasAnyRole(user.roles, kpiRoles) : false;

  return (
    <div className="page">
      <header className="page__header">
        <div>
          <h1>Dashboard</h1>
          <p>Welcome back{user?.fullName ? `, ${user.fullName}` : ""}.</p>
        </div>
      </header>

      <section className="card-grid" aria-label="Account summary">
        <div className="card">
          <div className="metric__label">Signed in as</div>
          <div className="metric__value">{user?.email ?? "—"}</div>
        </div>
        <div className="card">
          <div className="metric__label">User ID</div>
          <div className="metric__value metric__value--small">
            {user?.userId ?? "—"}
          </div>
        </div>
        <div className="card">
          <div className="metric__label">Roles</div>
          <div className="actions" style={{ marginTop: "0.5rem" }}>
            {(user?.roles ?? []).map((role) => (
              <Badge key={role}>{role}</Badge>
            ))}
          </div>
        </div>
      </section>

      <section className="card">
        <h2>Quick access</h2>
        <div className="actions">
          {kpis ? <Link href="/kpis">View KPI report</Link> : null}
          {admin ? <Link href="/admin/users">Manage users</Link> : null}
          {admin ? <Link href="/admin/organisations">Manage organisations</Link> : null}
          {admin ? <Link href="/admin/audit">Review audit trail</Link> : null}
          {!kpis && !admin ? (
            <p>Case, resource and shelter modules will appear here as members ship them.</p>
          ) : null}
        </div>
      </section>

      <section className="notice">
        AI recommendations support human decision-making; they never approve critical
        actions automatically.
      </section>
    </div>
  );
}
