"use client";

import Link from "next/link";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useState, type FormEvent } from "react";
import { Brand } from "@/components/safeway/Brand";
import { Icon } from "@/components/safeway/Icon";

import { useAuth } from "@/lib/auth";
import { useT } from "@/lib/i18n";
import { type PublicPurpose } from "@/lib/roles";

type FieldErrors = Partial<
  Record<
    "firstName" | "lastName" | "email" | "phoneNumber" | "password" | "confirmPassword" | "purpose" | "consent",
    string
  >
>;

function normalizePhoneNumber(value: string): string | null {
  const trimmed = value.trim();
  const digits = trimmed.replace(/[^\d]/g, "");
  if (/[^0-9\s\-()+]/.test(trimmed)) return null;
  if (digits.length === 10 && digits.startsWith("07")) return `+94${digits.slice(1)}`;
  if (digits.length === 11 && digits.startsWith("947")) return `+94${digits.slice(2)}`;
  return null;
}

function isValidEmail(value: string): boolean {
  const email = value.trim().toLowerCase();
  if (email.length < 3 || email.length > 254) return false;
  const at = email.indexOf("@");
  if (at <= 0 || at !== email.lastIndexOf("@") || at === email.length - 1) return false;
  return email.indexOf(".", at + 1) >= 0;
}

export default function RegisterPage() {
  const { register } = useAuth();
  const t = useT();
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [purpose, setPurpose] = useState<PublicPurpose>("citizen");
  const [consent, setConsent] = useState(false);
  const [complete, setComplete] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});

  const purposes: { value: PublicPurpose; icon: string; title: string; copy: string }[] = [
    { value: "citizen", icon: "file", title: t("auth.purposeCitizen"), copy: t("auth.purposeCitizenCopy") },
    { value: "volunteer", icon: "users", title: t("auth.purposeVolunteer"), copy: t("auth.purposeVolunteerCopy") },
    { value: "donor", icon: "heart", title: t("auth.purposeDonor"), copy: t("auth.purposeDonorCopy") }
  ];

  function validate(): FieldErrors {
    const next: FieldErrors = {};
    const fn = firstName.trim();
    const ln = lastName.trim();
    const em = email.trim();
    const ph = phoneNumber.trim();
    if (!fn) next.firstName = t("validation.firstNameRequired");
    else if (fn.length > 100) next.firstName = t("validation.nameTooLong", { max: 100 });
    if (!ln) next.lastName = t("validation.lastNameRequired");
    else if (ln.length > 100) next.lastName = t("validation.nameTooLong", { max: 100 });
    if (!em) next.email = t("validation.emailRequired");
    else if (!isValidEmail(em)) next.email = t("validation.emailInvalid");
    if (!ph) next.phoneNumber = t("validation.phoneRequired");
    else if (!normalizePhoneNumber(ph)) next.phoneNumber = t("validation.phoneInvalid");
    if (!password) next.password = t("validation.passwordRequired");
    else if (password.length < 8) next.password = t("validation.passwordMin");
    if (!confirmPassword) next.confirmPassword = t("validation.confirmPasswordRequired");
    else if (password !== confirmPassword) next.confirmPassword = t("validation.passwordsNoMatch");
    if (!purpose) next.purpose = t("validation.purposeRequired");
    if (!consent) next.consent = t("validation.consentRequired");
    return next;
  }

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    const nextErrors = validate();
    setFieldErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) return;
    setBusy(true);
    try {
      await register({
        fullName: `${firstName.trim()} ${lastName.trim()}`.trim(),
        email: email.trim(),
        phoneNumber: phoneNumber.trim(),
        password,
        accountPurpose: purpose
      });
      setComplete(true);
    } catch (caught) {
      setError(localizedErrorMessage(caught, t));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-page register-page">
      <div className="login-brand-panel">
        <Brand light />
        <div>
          <div className="eyebrow"><span /> {t("auth.publicAccount")}</div>
          <h1>{t("auth.registerIntroTitle")}</h1>
          <p>{t("auth.registerIntroCopy")}</p>
          <div className="register-benefits-compact">
            <div><Icon name="file" size={16} /><span>{t("auth.benefitTrack")}</span></div>
            <div><Icon name="bell" size={16} /><span>{t("auth.benefitNotifications")}</span></div>
            <div><Icon name="shield" size={16} /><span>{t("auth.benefitSecure")}</span></div>
          </div>
        </div>
        <blockquote>{t("auth.quote")}</blockquote>
      </div>

      <div className="login-form-panel">
        <div style={{ display: "flex", justifyContent: "space-between", gap: 12, alignItems: "center", flexWrap: "wrap" }}>
          <Link className="back-link" href="/">{t("common.backToHome")}</Link>
        </div>
        <div className="login-form register-form">
          <span className="mobile-brand"><Brand /></span>

          {complete ? (
            <div className="register-success-panel">
              <div className="register-success-icon"><Icon name="check" size={28} /></div>
              <h2>{t("auth.accountCreated")}</h2>
              <p>{t("auth.accountCreatedCopy")}</p>
              <Link className="btn btn-primary login-submit" href="/dashboard">
                {t("auth.continueToDashboard")} <Icon name="arrow" />
              </Link>
            </div>
          ) : (
            <>
              <h2>{t("auth.registerTitle")}</h2>
              <p>{t("auth.registerSubtitle")}</p>

              <form onSubmit={onSubmit} noValidate>
                <div className="two-fields">
                  <label>
                    <span className="field-label">{t("auth.firstName")}</span>
                    <input value={firstName} onChange={(event) => setFirstName(event.target.value)} autoComplete="given-name" aria-invalid={fieldErrors.firstName ? "true" : undefined} required />
                    {fieldErrors.firstName ? <span className="field-error">{fieldErrors.firstName}</span> : null}
                  </label>
                  <label>
                    <span className="field-label">{t("auth.lastName")}</span>
                    <input value={lastName} onChange={(event) => setLastName(event.target.value)} autoComplete="family-name" aria-invalid={fieldErrors.lastName ? "true" : undefined} required />
                    {fieldErrors.lastName ? <span className="field-error">{fieldErrors.lastName}</span> : null}
                  </label>
                </div>

                <label>
                  <span className="field-label">{t("auth.email")}</span>
                  <input type="email" placeholder={t("auth.emailPlaceholder")} value={email} onChange={(event) => setEmail(event.target.value)} autoComplete="email" aria-invalid={fieldErrors.email ? "true" : undefined} required />
                  {fieldErrors.email ? <span className="field-error">{fieldErrors.email}</span> : null}
                </label>

                <label>
                  <span className="field-label">{t("auth.phoneNumber")}</span>
                  <input type="tel" placeholder={t("auth.phonePlaceholder")} value={phoneNumber} onChange={(event) => setPhoneNumber(event.target.value)} autoComplete="tel" aria-invalid={fieldErrors.phoneNumber ? "true" : undefined} required />
                  {fieldErrors.phoneNumber ? <span className="field-error">{fieldErrors.phoneNumber}</span> : null}
                </label>

                <fieldset className="portal-purpose register-purpose">
                  <legend><span className="field-label">{t("auth.purpose")}</span></legend>
                  <div className="portal-purpose-grid">
                    {purposes.map((option) => (
                      <label key={option.value} className={purpose === option.value ? "active" : ""}>
                        <input type="radio" name="accountPurpose" value={option.value} checked={purpose === option.value} onChange={() => setPurpose(option.value)} />
                        <i><Icon name={option.icon} size={18} /></i>
                        <span><b>{option.title}</b><small>{option.copy}</small></span>
                      </label>
                    ))}
                  </div>
                  {fieldErrors.purpose ? <span className="field-error">{fieldErrors.purpose}</span> : null}
                </fieldset>

                <label>
                  <span className="field-label">{t("auth.createPassword")}</span>
                  <input type="password" value={password} onChange={(event) => setPassword(event.target.value)} autoComplete="new-password" minLength={8} aria-invalid={fieldErrors.password ? "true" : undefined} required />
                  {fieldErrors.password ? <span className="field-error">{fieldErrors.password}</span> : null}
                </label>

                <label>
                  <span className="field-label">{t("auth.confirmPassword")}</span>
                  <input type="password" value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} autoComplete="new-password" minLength={8} aria-invalid={fieldErrors.confirmPassword ? "true" : undefined} required />
                  {fieldErrors.confirmPassword ? <span className="field-error">{fieldErrors.confirmPassword}</span> : null}
                </label>

                <label className="consent">
                  <input type="checkbox" checked={consent} onChange={(event) => setConsent(event.target.checked)} aria-invalid={fieldErrors.consent ? "true" : undefined} />
                  <span>{t("auth.consent")}</span>
                </label>
                {fieldErrors.consent ? <span className="field-error">{fieldErrors.consent}</span> : null}

                {error ? <div className="form-error" role="alert">{error}</div> : null}

                <button className="btn btn-primary login-submit" type="submit" disabled={busy}>
                  {busy ? t("auth.creatingAccount") : t("common.register")} <Icon name="arrow" />
                </button>
              </form>

              <p className="register">{t("auth.alreadyHaveAccount")} <Link href="/login">{t("auth.signInLink")}</Link></p>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
