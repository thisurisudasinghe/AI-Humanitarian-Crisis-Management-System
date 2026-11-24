"use client";

import Link from "next/link";
import { useEffect, useState, type FormEvent } from "react";
import { apiJson } from "@/lib/api";
import { Icon } from "@/components/safeway/Icon";
import { PortalState } from "@/components/safeway/PortalPrimitives";
import { useAuth } from "@/lib/auth";
import { useI18n, useT } from "@/lib/i18n";
import { formatDate } from "@/lib/i18n/core";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { RoleNames } from "@/lib/roles";

const REQUESTABLE_ROLES = [
  { value: RoleNames.EmergencyCoordinator, labelKey: "roleLabels.EmergencyCoordinator" },
  { value: RoleNames.CaseManager, labelKey: "roleLabels.CaseManager" },
  { value: RoleNames.LogisticsManager, labelKey: "roleLabels.LogisticsManager" },
  { value: RoleNames.ShelterAdmin, labelKey: "roleLabels.ShelterAdmin" },
  { value: RoleNames.VolunteerCoordinator, labelKey: "roleLabels.VolunteerCoordinator" },
  { value: RoleNames.DonorOfficer, labelKey: "roleLabels.DonorOfficer" },
  { value: RoleNames.PartnerOfficer, labelKey: "roleLabels.PartnerOfficer" }
] as const;

interface ApplicationReceipt {
  id: string;
  reference: string;
  status: string;
  submittedAt: string;
}

function staffStatusLabel(t: (key: string) => string, status: string): string {
  const key = `pw.staff.statusLabels.${status}`;
  const label = t(key);
  return label === key ? t("pw.common.statusUnknown") : label;
}

export default function StaffAccessPage() {
  const t = useT();
  const { locale } = useI18n();
  const { user, ready } = useAuth();
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [organization, setOrganization] = useState("");
  const [designation, setDesignation] = useState("");
  const [requestedRole, setRequestedRole] = useState<string>(RoleNames.EmergencyCoordinator);
  const [reason, setReason] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [receipt, setReceipt] = useState<ApplicationReceipt | null>(null);

  useEffect(() => {
    if (!user) return;
    setFullName((current) => current || user.fullName);
    setEmail((current) => current || user.email || "");
  }, [user]);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(null);

    if (!fullName.trim() || !email.trim() || !organization.trim() || !designation.trim() || !reason.trim()) {
      setFormError(t("pw.staff.apply.required"));
      return;
    }

    setBusy(true);
    try {
      const created = await apiJson<ApplicationReceipt>("/api/v1/staff-access/applications", "POST", {
        fullName: fullName.trim(),
        email: email.trim(),
        organization: organization.trim(),
        designation: designation.trim(),
        requestedRole,
        reason: reason.trim()
      });
      setReceipt(created);
    } catch (cause) {
      setFormError(localizedErrorMessage(cause, t));
    } finally {
      setBusy(false);
    }
  }

  if (!ready) {
    return (
      <main className="public-info pw-page">
        <div className="page-body">
          <PortalState kind="loading" title={t("pw.staff.checking")}>{t("pw.staff.checkingBody")}</PortalState>
        </div>
      </main>
    );
  }

  if (!user) {
    return (
      <main className="public-info pw-page">
        <header className="page-hero">
          <div className="page-hero-lines" />
          <div className="eyebrow"><span /> {t("pw.staff.eyebrow")}</div>
          <h1>{t("pw.staff.title")}</h1>
          <p>{t("pw.staff.intro")}</p>
        </header>
        <div className="page-body">
          <PortalState
            kind="empty"
            title={t("pw.staff.signInTitle")}
            href="/login?next=/staff-access"
            linkLabel={t("pw.staff.signInLink")}
          >
            {t("pw.staff.signInBody")}
          </PortalState>
          <section className="pw-card pw-card--blue" aria-labelledby="pw-staff-public-account">
            <h2 id="pw-staff-public-account">{t("pw.staff.publicAccountTitle")}</h2>
            <p>{t("pw.staff.publicAccountBody")}</p>
            <div className="pw-card__links">
              <Link href="/register">{t("pw.staff.createAccountLink")} <Icon name="arrow" size={15} /></Link>
              <Link href="/support">{t("pw.staff.supportLink")} <Icon name="arrow" size={15} /></Link>
            </div>
          </section>
        </div>
      </main>
    );
  }

  return (
    <main className="public-info pw-page">
      <header className="page-hero">
        <div className="page-hero-lines" />
        <div className="eyebrow"><span /> {t("pw.staff.eyebrow")}</div>
        <h1>{t("pw.staff.title")}</h1>
        <p>{t("pw.staff.intro")}</p>
      </header>

      <div className="page-body">
        <aside className="pw-callout pw-callout--blue" role="note">
          <span className="pw-callout__icon" aria-hidden="true">
            <Icon name="info" size={22} />
          </span>
          <div>
            <b>{t("pw.staff.distinctionTitle")}</b>
            <p>{t("pw.staff.distinctionBody")}</p>
          </div>
          <Link href="/support">{t("pw.staff.supportLink")} <Icon name="arrow" size={14} /></Link>
        </aside>

        {receipt ? (
          <article className="pw-card pw-card--green pw-confirmation">
            <span className="pw-card__icon" aria-hidden="true">
              <Icon name="checkCircle" size={26} />
            </span>
            <h2>{t("pw.staff.apply.successTitle")}</h2>
            <p>{t("pw.staff.apply.successBody")}</p>
            <dl className="pw-facts">
              <div>
                <dt>{t("pw.common.reference")}</dt>
                <dd className="pw-facts__reference">{receipt.reference}</dd>
              </div>
              <div>
                <dt>{t("pw.common.status")}</dt>
                <dd>{staffStatusLabel(t, receipt.status)}</dd>
              </div>
              <div>
                <dt>{t("pw.guestReport.submitted")}</dt>
                <dd>{formatDate(receipt.submittedAt, locale)}</dd>
              </div>
            </dl>
            <p className="pw-muted">{t("pw.staff.apply.approvalNote")}</p>
            <div className="pw-card__links pw-card__links--row">
              <Link className="btn btn-primary" href="/staff-access/status">
                {t("pw.staff.apply.statusLink")} <Icon name="arrow" size={16} />
              </Link>
            </div>
          </article>
        ) : (
          <form className="pw-card pw-form" onSubmit={(event) => void onSubmit(event)} noValidate>
            <h2>{t("pw.staff.apply.formTitle")}</h2>
            <p className="pw-form__intro">{t("pw.staff.apply.formIntro")}</p>

            <div className="pw-form-grid">
              <label className="pw-field">
                <span className="pw-field__label">{t("pw.staff.apply.fullName")}</span>
                <input type="text" value={fullName} onChange={(event) => setFullName(event.target.value)} required />
              </label>
              <label className="pw-field">
                <span className="pw-field__label">{t("pw.staff.apply.email")}</span>
                <input type="email" value={email} onChange={(event) => setEmail(event.target.value)} required />
              </label>
              <label className="pw-field">
                <span className="pw-field__label">{t("pw.staff.apply.organization")}</span>
                <input type="text" value={organization} onChange={(event) => setOrganization(event.target.value)} required />
              </label>
              <label className="pw-field">
                <span className="pw-field__label">{t("pw.staff.apply.designation")}</span>
                <input type="text" value={designation} onChange={(event) => setDesignation(event.target.value)} required />
              </label>
            </div>

            <label className="pw-field">
              <span className="pw-field__label">{t("pw.staff.apply.requestedRole")}</span>
              <select value={requestedRole} onChange={(event) => setRequestedRole(event.target.value)}>
                {REQUESTABLE_ROLES.map((role) => (
                  <option value={role.value} key={role.value}>{t(role.labelKey)}</option>
                ))}
              </select>
              <small>{t("pw.staff.apply.roleHint")}</small>
            </label>

            <label className="pw-field">
              <span className="pw-field__label">{t("pw.staff.apply.reason")}</span>
              <textarea rows={5} value={reason} onChange={(event) => setReason(event.target.value)} required />
            </label>

            {formError ? <p className="pw-form-error" role="alert">{formError}</p> : null}

            <button className="btn btn-primary pw-submit" type="submit" disabled={busy}>
              {busy ? t("pw.staff.apply.submitting") : t("pw.staff.apply.submit")}
              <Icon name="arrow" size={16} />
            </button>
          </form>
        )}
      </div>
    </main>
  );
}
