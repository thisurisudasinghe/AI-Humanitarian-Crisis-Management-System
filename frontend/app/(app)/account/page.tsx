"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState, type FormEvent } from "react";
import { Icon } from "@/components/safeway/Icon";
import { useAuth } from "@/lib/auth";
import {
  caseRequestErrorMessage,
  getMyHousehold,
  saveMyHousehold,
  type HouseholdProfile
} from "@/lib/crisis";
import { humanizeRole } from "@/lib/format";

export default function AccountPage() {
  const router = useRouter();
  const { user, logout } = useAuth();
  const isCitizen = user?.roles.includes("Citizen") ?? false;

  const [household, setHousehold] = useState<HouseholdProfile | null>(null);
  const [size, setSize] = useState("1");
  const [accessibilityNeeds, setAccessibilityNeeds] = useState("");
  const [contactPhone, setContactPhone] = useState("");
  const [householdBusy, setHouseholdBusy] = useState(false);
  const [householdLoaded, setHouseholdLoaded] = useState(!isCitizen);
  const [householdError, setHouseholdError] = useState<string | null>(null);
  const [householdSaved, setHouseholdSaved] = useState(false);

  useEffect(() => {
    if (!isCitizen) {
      setHouseholdLoaded(true);
      return;
    }
    let cancelled = false;
    setHouseholdLoaded(false);
    getMyHousehold()
      .then((profile) => {
        if (cancelled) return;
        setHousehold(profile);
        setSize(String(profile?.size ?? 1));
        setAccessibilityNeeds(profile?.accessibilityNeeds ?? "");
        setContactPhone(profile?.contactPhone ?? "");
        setHouseholdError(null);
      })
      .catch((cause) => {
        if (cancelled) return;
        setHouseholdError(caseRequestErrorMessage(cause, "Household profile could not be loaded."));
      })
      .finally(() => {
        if (!cancelled) setHouseholdLoaded(true);
      });
    return () => {
      cancelled = true;
    };
  }, [isCitizen]);

  async function onLogout() {
    await logout();
    router.replace("/login");
  }

  async function onSaveHousehold(event: FormEvent) {
    event.preventDefault();
    setHouseholdBusy(true);
    setHouseholdError(null);
    setHouseholdSaved(false);
    try {
      const parsedSize = Number(size);
      if (!Number.isInteger(parsedSize) || parsedSize < 1 || parsedSize > 100) {
        throw new Error("Household size must be a whole number between 1 and 100.");
      }
      const saved = await saveMyHousehold({
        size: parsedSize,
        accessibilityNeeds: accessibilityNeeds.trim() || undefined,
        contactPhone: contactPhone.trim() || undefined
      });
      setHousehold({
        id: saved.id,
        size: parsedSize,
        accessibilityNeeds: accessibilityNeeds.trim() || null,
        contactPhone: contactPhone.trim() || null
      });
      setHouseholdSaved(true);
    } catch (cause) {
      setHouseholdError(caseRequestErrorMessage(cause, "Household profile could not be saved."));
    } finally {
      setHouseholdBusy(false);
    }
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

      {isCitizen ? (
        <section className="ops-card" style={{ padding: 24, marginBottom: 24 }}>
          <div className="card-heading">
            <div>
              <h3>Household profile</h3>
              <p>Used for assistance coordination. Saved through the household API.</p>
            </div>
          </div>
          {!householdLoaded ? <p className="muted">Loading household profile…</p> : null}
          {householdError ? <p className="form-error" role="alert">{householdError}</p> : null}
          {householdSaved ? <p className="muted" role="status">Household profile saved.</p> : null}
          <form onSubmit={onSaveHousehold} style={{ display: "grid", gap: 12, maxWidth: 560 }}>
            <label>
              <span className="field-label">Household size</span>
              <input
                className="field-input"
                type="number"
                min={1}
                max={100}
                value={size}
                onChange={(event) => setSize(event.target.value)}
                required
              />
            </label>
            <label>
              <span className="field-label">Accessibility needs</span>
              <textarea
                className="field-input"
                value={accessibilityNeeds}
                onChange={(event) => setAccessibilityNeeds(event.target.value)}
                placeholder="Mobility, medical, language or other needs"
              />
            </label>
            <label>
              <span className="field-label">Contact phone</span>
              <input
                className="field-input"
                value={contactPhone}
                onChange={(event) => setContactPhone(event.target.value)}
                placeholder="Optional phone number"
              />
            </label>
            <div>
              <button className="btn btn-primary" type="submit" disabled={householdBusy || !householdLoaded}>
                {householdBusy ? "Saving…" : household ? "Update household profile" : "Create household profile"}
              </button>
            </div>
          </form>
        </section>
      ) : null}

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
