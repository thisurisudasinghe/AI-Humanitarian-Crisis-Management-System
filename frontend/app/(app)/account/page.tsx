"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState, type FormEvent } from "react";
import { Icon } from "@/components/safeway/Icon";
import { useT } from "@/lib/i18n";
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
  const t = useT();
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
        setHouseholdError(caseRequestErrorMessage(cause, t("account.householdLoadError"), t));
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
        throw new Error(t("account.householdSizeError"));
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
      setHouseholdError(caseRequestErrorMessage(cause, t("account.householdSaveError"), t));
    } finally {
      setHouseholdBusy(false);
    }
  }

  return (
    <>
      <div className="reports-page-head">
        <div>
          <p>{t("account.eyebrow")}</p>
          <h1>{t("account.title")}</h1>
          <span>{t("account.intro")}</span>
        </div>
        <Link className="btn btn-outline" href="/contact">
          <Icon name="shield" /> {t("account.getSupport")}
        </Link>
      </div>

      <div className="profile-card-grid">
        <div className="ops-card">
          <span>{t("account.fullName")}</span>
          <b>{user?.fullName ?? "—"}</b>
        </div>
        <div className="ops-card">
          <span>{t("account.email")}</span>
          <b>{user?.email ?? "—"}</b>
        </div>
        <div className="ops-card">
          <span>{t("account.roles")}</span>
          <div className="role-chips">
            {(user?.roles ?? []).map((role) => (
              <span className="badge blue" key={role}>
                {humanizeRole(role, t)}
              </span>
            ))}
          </div>
        </div>
      </div>

      <details className="ops-card" style={{ padding: "14px 22px", marginBottom: 22 }}>
        <summary style={{ cursor: "pointer", fontWeight: 700, color: "var(--midnight)" }}>
          {t("common.details")}
        </summary>
        <div className="profile-card-grid" style={{ marginTop: 12, marginBottom: 0 }}>
          <div className="ops-card">
            <span>{t("account.userId")}</span>
            <b style={{ fontSize: 12 }}>{user?.userId ?? "—"}</b>
          </div>
        </div>
      </details>

      {isCitizen ? (
        <section className="ops-card" style={{ padding: 24, marginBottom: 24 }}>
          <div className="card-heading">
            <div>
              <h3>{t("account.householdTitle")}</h3>
              <p>{t("account.householdIntro")}</p>
            </div>
          </div>
          {!householdLoaded ? <p className="muted">{t("account.householdLoading")}</p> : null}
          {householdError ? <p className="form-error" role="alert">{householdError}</p> : null}
          {householdSaved ? <p className="muted" role="status">{t("account.householdSaved")}</p> : null}
          <form onSubmit={onSaveHousehold} style={{ display: "grid", gap: 12, maxWidth: 560 }}>
            <label>
              <span className="field-label">{t("account.householdSize")}</span>
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
              <span className="field-label">{t("account.accessibility")}</span>
              <textarea
                className="field-input"
                value={accessibilityNeeds}
                onChange={(event) => setAccessibilityNeeds(event.target.value)}
                placeholder={t("account.accessibilityPh")}
              />
            </label>
            <label>
              <span className="field-label">{t("account.contactPhone")}</span>
              <input
                className="field-input"
                value={contactPhone}
                onChange={(event) => setContactPhone(event.target.value)}
                placeholder={t("account.phonePh")}
              />
            </label>
            <div>
              <button className="btn btn-primary" type="submit" disabled={householdBusy || !householdLoaded}>
                {householdBusy ? t("account.saving") : household ? t("account.updateHousehold") : t("account.createHousehold")}
              </button>
            </div>
          </form>
        </section>
      ) : null}

      <section className="ops-card" style={{ padding: 24 }}>
        <div className="card-heading">
          <div>
            <h3>{t("account.sessionTitle")}</h3>
            <p>{t("account.sessionIntro")}</p>
          </div>
        </div>
        <div className="resource-bars">
          <div>
            <span>
              <b>{t("account.signOutTitle")}</b>
              <i>{t("account.signOutDesc")}</i>
            </span>
          </div>
        </div>
        <div className="account-actions" style={{ marginTop: 22 }}>
          <button className="btn btn-outline" type="button" onClick={() => void onLogout()}>
            <Icon name="logout" /> {t("account.signOut")}
          </button>
          {isCitizen ? (
            <Link className="btn btn-primary" href="/report">
              <Icon name="alert" /> {t("account.reportCrisis")}
            </Link>
          ) : null}
        </div>
      </section>
    </>
  );
}
