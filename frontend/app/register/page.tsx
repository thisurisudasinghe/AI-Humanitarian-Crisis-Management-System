"use client";

import Link from "next/link";
import { useState, type FormEvent } from "react";
import { Brand } from "@/components/safeway/Brand";
import { Icon } from "@/components/safeway/Icon";
import { errorMessage } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { type PublicPurpose } from "@/lib/roles";

const purposes: { value: PublicPurpose; icon: string; title: string; copy: string }[] = [
  {
    value: "citizen",
    icon: "file",
    title: "Get help",
    copy: "Report a crisis and track the response."
  },
  {
    value: "volunteer",
    icon: "users",
    title: "Volunteer",
    copy: "Offer skills, availability and support."
  },
  {
    value: "donor",
    icon: "heart",
    title: "Support relief",
    copy: "Record donations and follow their impact."
  }
];

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

  function validate(): FieldErrors {
    const next: FieldErrors = {};
    const fn = firstName.trim();
    const ln = lastName.trim();
    const em = email.trim();
    const ph = phoneNumber.trim();

    if (!fn) next.firstName = "First name is required.";
    else if (fn.length > 100) next.firstName = "First name must be 100 characters or fewer.";

    if (!ln) next.lastName = "Last name is required.";
    else if (ln.length > 100) next.lastName = "Last name must be 100 characters or fewer.";

    if (!em) next.email = "Email address is required.";
    else if (!isValidEmail(em)) next.email = "Enter a valid email address.";

    if (!ph) next.phoneNumber = "Phone number is required.";
    else if (!normalizePhoneNumber(ph))
      next.phoneNumber = "Enter a valid Sri Lankan phone number (07XXXXXXXX or +947XXXXXXXX).";

    if (!password) next.password = "Password is required.";
    else if (password.length < 8) next.password = "Password must be at least 8 characters.";

    if (!confirmPassword) next.confirmPassword = "Confirm your password.";
    else if (password !== confirmPassword) next.confirmPassword = "Passwords do not match.";

    if (!purpose) next.purpose = "Select how you will use SAFEWAY.";

    if (!consent) next.consent = "Accept the privacy notice and account terms to continue.";

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
      setError(errorMessage(caught));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-page register-page">
      <div className="login-brand-panel">
        <Brand light />
        <div>
          <div className="eyebrow">
            <span /> Public account
          </div>
          <h1>Keep your reports and assistance updates in one secure place.</h1>
          <p>
            A SAFEWAY account helps you track submitted reports, receive relevant notifications, and
            return to verified support information.
          </p>
          <div className="register-benefits-compact">
            <div><Icon name="file" size={16} /><span>Track every report clearly.</span></div>
            <div><Icon name="bell" size={16} /><span>Receive useful notifications.</span></div>
            <div><Icon name="shield" size={16} /><span>Protected, secure access.</span></div>
          </div>
        </div>
        <blockquote>“Connecting people to help. Coordinating action. Restoring hope.”</blockquote>
      </div>

      <div className="login-form-panel">
        <Link className="back-link" href="/">
          ← Back to home
        </Link>
        <div className="login-form register-form">
          <span className="mobile-brand">
            <Brand />
          </span>

          {complete ? (
            <div className="register-success-panel">
              <div className="register-success-icon">
                <Icon name="check" size={28} />
              </div>
              <h2>Account created</h2>
              <p>Your SAFEWAY account is ready and you are signed in.</p>
              <Link className="btn btn-primary login-submit" href="/dashboard">
                Continue to dashboard <Icon name="arrow" />
              </Link>
            </div>
          ) : (
            <>
              <h2>Create your account</h2>
              <p>Use contact details you can access safely.</p>

              <form onSubmit={onSubmit} noValidate>
                <div className="two-fields">
                  <label>
                    <span className="field-label">First name</span>
                    <input
                      value={firstName}
                      onChange={(event) => setFirstName(event.target.value)}
                      autoComplete="given-name"
                      aria-invalid={fieldErrors.firstName ? "true" : undefined}
                      required
                    />
                    {fieldErrors.firstName ? <span className="field-error">{fieldErrors.firstName}</span> : null}
                  </label>
                  <label>
                    <span className="field-label">Last name</span>
                    <input
                      value={lastName}
                      onChange={(event) => setLastName(event.target.value)}
                      autoComplete="family-name"
                      aria-invalid={fieldErrors.lastName ? "true" : undefined}
                      required
                    />
                    {fieldErrors.lastName ? <span className="field-error">{fieldErrors.lastName}</span> : null}
                  </label>
                </div>

                <label>
                  <span className="field-label">Email address</span>
                  <input
                    type="email"
                    placeholder="you@example.org"
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
                    autoComplete="email"
                    aria-invalid={fieldErrors.email ? "true" : undefined}
                    required
                  />
                  {fieldErrors.email ? <span className="field-error">{fieldErrors.email}</span> : null}
                </label>

                <label>
                  <span className="field-label">Phone number</span>
                  <input
                    type="tel"
                    placeholder="07X XXXXXXX or +947X XXXXXXX"
                    value={phoneNumber}
                    onChange={(event) => setPhoneNumber(event.target.value)}
                    autoComplete="tel"
                    aria-invalid={fieldErrors.phoneNumber ? "true" : undefined}
                    required
                  />
                  {fieldErrors.phoneNumber ? <span className="field-error">{fieldErrors.phoneNumber}</span> : null}
                </label>

                <fieldset className="portal-purpose register-purpose">
                  <legend>
                    <span className="field-label">How will you use SAFEWAY?</span>
                  </legend>
                  <div className="portal-purpose-grid">
                    {purposes.map((option) => (
                      <label key={option.value} className={purpose === option.value ? "active" : ""}>
                        <input
                          type="radio"
                          name="accountPurpose"
                          value={option.value}
                          checked={purpose === option.value}
                          onChange={() => setPurpose(option.value)}
                        />
                        <i>
                          <Icon name={option.icon} size={18} />
                        </i>
                        <span>
                          <b>{option.title}</b>
                          <small>{option.copy}</small>
                        </span>
                      </label>
                    ))}
                  </div>
                  {fieldErrors.purpose ? <span className="field-error">{fieldErrors.purpose}</span> : null}
                </fieldset>

                <label>
                  <span className="field-label">Create password</span>
                  <input
                    type="password"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    autoComplete="new-password"
                    minLength={8}
                    aria-invalid={fieldErrors.password ? "true" : undefined}
                    required
                  />
                  {fieldErrors.password ? <span className="field-error">{fieldErrors.password}</span> : null}
                </label>

                <label>
                  <span className="field-label">Confirm password</span>
                  <input
                    type="password"
                    value={confirmPassword}
                    onChange={(event) => setConfirmPassword(event.target.value)}
                    autoComplete="new-password"
                    minLength={8}
                    aria-invalid={fieldErrors.confirmPassword ? "true" : undefined}
                    required
                  />
                  {fieldErrors.confirmPassword ? <span className="field-error">{fieldErrors.confirmPassword}</span> : null}
                </label>

                <label className="consent">
                  <input
                    type="checkbox"
                    checked={consent}
                    onChange={(event) => setConsent(event.target.checked)}
                    aria-invalid={fieldErrors.consent ? "true" : undefined}
                  />
                  <span>I agree to the SAFEWAY privacy notice and account terms.</span>
                </label>
                {fieldErrors.consent ? <span className="field-error">{fieldErrors.consent}</span> : null}

                {error ? (
                  <div className="form-error" role="alert">
                    {error}
                  </div>
                ) : null}

                <button className="btn btn-primary login-submit" type="submit" disabled={busy}>
                  {busy ? "Creating account..." : "Create account"} <Icon name="arrow" />
                </button>
              </form>

              <p className="register">
                Already have an account? <Link href="/login">Sign in</Link>
              </p>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
