"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { Icon } from "@/components/safeway/Icon";
import { PublicFooter } from "@/components/safeway/PublicFooter";
import { PublicNav } from "@/components/safeway/PublicNav";
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
  const [purpose, setPurpose] = useState<PublicPurpose>("citizen");
  const [consent, setConsent] = useState(false);
  const [complete, setComplete] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

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
            <div className="register-success">
              <i>
                <Icon name="check" size={36} />
              </i>
              <div className="section-kicker">Account ready</div>
              <h2>Welcome to SAFEWAY.</h2>
              <p>
                Your {purpose === "citizen" ? "citizen" : purpose} account has been created and you
                are signed in.
              </p>
              <Link className="btn btn-primary" href="/dashboard">
                Continue to dashboard <Icon name="arrow" />
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

              <fieldset className="portal-purpose">
                <legend>
                  <span className="field-label">How will you use SAFEWAY?</span>
                </legend>
                <div className="portal-purpose-grid">
                  {purposes.map((option) => (
                    <label
                      key={option.value}
                      className={purpose === option.value ? "active" : ""}
                    >
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
                <small>Your selection is a request. Your account roles are set by the server.</small>
              </fieldset>

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
                <span>I agree to the SAFEWAY privacy notice and account terms.</span>
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
