"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { Icon } from "@/components/safeway/Icon";
import { useAuth } from "@/lib/auth";
import { humanizeRole } from "@/lib/format";

export default function AccountPage() {
  const router = useRouter();
  const { user, logout } = useAuth();

  async function onLogout() {
    await logout();
    router.replace("/login");
  }

  return (
    <>
      <div className="reports-page-head">
        <div>
          <p>ACCOUNT SETTINGS</p>
          <h1>Your SAFEWAY account</h1>
          <span>Profile, roles and session details from the identity service.</span>
        </div>
        <Link className="btn btn-outline" href="/contact">
          <Icon name="shield" /> Get support
        </Link>
      </div>

      <div className="profile-card-grid">
        <div className="ops-card">
          <span>Full name</span>
          <b>{user?.fullName ?? "—"}</b>
        </div>
        <div className="ops-card">
          <span>Email</span>
          <b>{user?.email ?? "—"}</b>
        </div>
        <div className="ops-card">
          <span>User ID</span>
          <b style={{ fontSize: 12 }}>{user?.userId ?? "—"}</b>
        </div>
        <div className="ops-card">
          <span>Roles</span>
          <div className="role-chips">
            {(user?.roles ?? []).map((role) => (
              <span className="badge blue" key={role}>
                {humanizeRole(role)}
              </span>
            ))}
          </div>
        </div>
      </div>

      <section className="ops-card" style={{ padding: 24 }}>
        <div className="card-heading">
          <div>
            <h3>Session security</h3>
            <p>How this browser keeps your session</p>
          </div>
        </div>
        <div className="resource-bars">
          <div>
            <span>
              <b>Access token</b>
              <i>Held in memory only</i>
            </span>
          </div>
          <div>
            <span>
              <b>Refresh token</b>
              <i>Stored in this tab&apos;s sessionStorage</i>
            </span>
          </div>
          <div>
            <span>
              <b>Sign out</b>
              <i>Revokes the refresh token and clears the session</i>
            </span>
          </div>
        </div>
        <div className="account-actions" style={{ marginTop: 22 }}>
          <button className="btn btn-outline" type="button" onClick={() => void onLogout()}>
            <Icon name="logout" /> Sign out
          </button>
          <Link className="btn btn-primary" href="/report">
            <Icon name="alert" /> Report a crisis
          </Link>
        </div>
      </section>
    </>
  );
}
