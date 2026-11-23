"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { Icon } from "@/components/safeway/Icon";
import { PublicFooter } from "@/components/safeway/PublicFooter";
import { PublicNav } from "@/components/safeway/PublicNav";
import { errorMessage } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { resendVerificationEmail } from "@/lib/verification";
import { resendView, type ResendState } from "@/lib/verification-state";

const benefits: [string, string, string][] = [
  ["file", "Track every report", "See review and response updates clearly."],
  ["bell", "Receive useful notifications", "Know when important information changes."],
  ["shield", "Protected access", "Your account and report details remain secure."]
];

export default function RegisterPage() {
  const router = useRouter();
  const { register } = useAuth();
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [consent, setConsent] = useState(false);
  const [complete, setComplete] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [resendState, setResendState] = useState<ResendState>("idle");

  async function onResend() {
    setResendState("loading");
    const result = await resendVerificationEmail(email.trim());
    setResendState(result.state);
  }

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    if (password.length < 8) {
      setError("Choose a password with at least 8 characters.");
      return;
    }

    if (password !== confirmPassword) {
      setError("Passwords do not match.");
      return;
    }

    if (!consent) {
      setError("Accept the privacy and account terms to continue.");
      return;
    }

    setBusy(true);

    try {
      await register({
        fullName: `${firstName.trim()} ${lastName.trim()}`.trim(),
        email: email.trim(),
        password
      });
      setComplete(true);
    } catch (caught) {
      setError(errorMessage(caught));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="auth-refined">
      <PublicNav />
      <main className="register-layout">
        <section className="register-intro">
          <div className="eyebrow">
            <span /> Public account
          </div>
          <h1>Keep your reports and assistance updates in one secure place.</h1>
          <p>
            A SAFEWAY account helps you track submitted reports, receive relevant notifications and
            return to verified support information.
          </p>
          <div className="register-benefits">
            {benefits.map(([icon, title, copy]) => (
              <div key={title}>
                <i>
                  <Icon name={icon} />
                </i>
                <span>
                  <b>{title}</b>
                  <small>{copy}</small>
                </span>
              </div>
            ))}
          </div>
        </section>

        <section className="register-card">
          {complete ? (
            <div className="register-success verification-pending">
              <i>
                <Icon name="bell" size={36} />
              </i>
              <div className="section-kicker">One more step</div>
              <h2>Check your email, {firstName.trim() || "friend"}.</h2>
              <p>
                Your Safeway account has been created. We sent a verification link to{" "}
                <strong>{email.trim()}</strong>. Open it within 30 minutes to activate your
                account.
              </p>
              <p className="verification-hint">
                Links stay valid for 30 minutes and can only be used once.
              </p>
              <div className="verification-actions">
                <Link className="btn btn-primary" href="/verify-email">
                  I have a link <Icon name="arrow" />
                </Link>
                <button
                  className="btn btn-outline"
                  type="button"
                  onClick={onResend}
                  disabled={resendState === "loading"}
                >
                  {resendState === "loading" ? "Sending…" : "Resend email"}
                </button>
              </div>
              {resendState !== "idle" ? (
                <p className="verification-feedback" role="status">
                  {resendView(resendState).message}
                </p>
              ) : null}
              <Link className="verification-login-link" href="/login">
                Back to login
              </Link>
            </div>
          ) : (
            <form onSubmit={onSubmit} noValidate>
              <span className="form-step">ACCOUNT SETUP · 1 OF 1</span>
              <h2>Create your account</h2>
              <p>Use contact details you can access safely.</p>

              <div className="two-fields">
                <label>
                  <span className="field-label">First name</span>
                  <input
                    value={firstName}
                    onChange={(event) => setFirstName(event.target.value)}
                    autoComplete="given-name"
                    required
                  />
                </label>
                <label>
                  <span className="field-label">Last name</span>
                  <input
                    value={lastName}
                    onChange={(event) => setLastName(event.target.value)}
                    autoComplete="family-name"
                    required
                  />
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
                  required
                />
              </label>

              <label>
                <span className="field-label">Create password</span>
                <input
                  type="password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  autoComplete="new-password"
                  minLength={8}
                  required
                />
              </label>

              <label>
                <span className="field-label">Confirm password</span>
                <input
                  type="password"
                  value={confirmPassword}
                  onChange={(event) => setConfirmPassword(event.target.value)}
                  autoComplete="new-password"
                  minLength={8}
                  required
                />
              </label>

              <label className="consent">
                <input
                  type="checkbox"
                  checked={consent}
                  onChange={(event) => setConsent(event.target.checked)}
                />
                <span>I agree to the SAFEWAY privacy and account terms for this prototype.</span>
              </label>

              {error ? (
                <div className="form-error" role="alert">
                  {error}
                </div>
              ) : null}

              <button className="btn btn-primary register-submit" type="submit" disabled={busy}>
                {busy ? "Creating account…" : "Create account"} <Icon name="arrow" />
              </button>

              <p className="register-login">
                Already have an account? <Link href="/login">Log in</Link>
              </p>
            </form>
          )}
        </section>
      </main>
      <PublicFooter />
    </div>
  );
}
