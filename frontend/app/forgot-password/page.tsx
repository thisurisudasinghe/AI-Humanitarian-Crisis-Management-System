"use client";

import Link from "next/link";
import { useState, type FormEvent } from "react";
import { Brand } from "@/components/safeway/Brand";
import { Icon } from "@/components/safeway/Icon";
import { apiJson, errorMessage } from "@/lib/api";

type ForgotPasswordResponse = {
  message: string;
  emailDeliveryConfigured: boolean;
};

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<ForgotPasswordResponse | null>(null);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setResult(null);

    const normalized = email.trim();
    if (!normalized) {
      setError("Enter the email address for your account.");
      return;
    }

    setBusy(true);
    try {
      const response = await apiJson<ForgotPasswordResponse>(
        "/api/v1/auth/forgot-password",
        "POST",
        { email: normalized }
      );
      setResult(response);
    } catch (caught) {
      setError(errorMessage(caught));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-page">
      <div className="login-brand-panel">
        <Brand light />
        <div>
          <div className="eyebrow">
            <span /> Account recovery
          </div>
          <h1>Reset your password securely.</h1>
          <p>
            Enter your account email and we&apos;ll send a secure, time-limited link if an account
            exists.
          </p>
        </div>
        <blockquote>“Connecting people to help. Coordinating action. Restoring hope.”</blockquote>
      </div>

      <div className="login-form-panel">
        <Link className="back-link" href="/login">
          ← Back to login
        </Link>
        <div className="login-form">
          <span className="mobile-brand">
            <Brand />
          </span>
          <h2>Forgot your password?</h2>
          <p>We&apos;ll email you a link to create a new password.</p>

          <form onSubmit={onSubmit} noValidate>
            <label>
              <span className="field-label">Email address</span>
              <input
                type="email"
                placeholder="you@example.org"
                autoComplete="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                required
              />
            </label>

            {error ? (
              <div className="form-error" role="alert">
                {error}
              </div>
            ) : null}

            {result ? (
              <div
                className={result.emailDeliveryConfigured ? "form-success" : "form-warning"}
                role="status"
              >
                {result.message}
              </div>
            ) : null}

            <button className="btn btn-primary login-submit" type="submit" disabled={busy}>
              {busy ? "Sending link..." : "Send reset link"} <Icon name="arrow" />
            </button>
          </form>

          <p className="register">
            Remembered your password? <Link href="/login">Back to login</Link>
          </p>
          <div className="secure-line">
            <Icon name="shield" size={16} /> For security, we don&apos;t confirm whether an email is
            registered.
          </div>
        </div>
      </div>
    </div>
  );
}
