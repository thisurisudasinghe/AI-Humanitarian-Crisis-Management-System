"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Suspense, useState, type FormEvent } from "react";
import { Brand } from "@/components/safeway/Brand";
import { Icon } from "@/components/safeway/Icon";
import { apiJson, errorMessage } from "@/lib/api";

function ResetPasswordForm() {
  const searchParams = useSearchParams();
  const token = (searchParams.get("token") ?? "").trim();
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showNew, setShowNew] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    if (!token) {
      setError("This password reset link is missing or invalid. Request a new link.");
      return;
    }

    if (newPassword.length < 8) {
      setError("Password must be at least 8 characters.");
      return;
    }

    if (newPassword !== confirmPassword) {
      setError("Passwords do not match.");
      return;
    }

    setBusy(true);
    try {
      await apiJson<void>("/api/v1/auth/reset-password", "POST", {
        token,
        newPassword,
        confirmPassword
      });
      setSuccess(true);
      setNewPassword("");
      setConfirmPassword("");
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
          <h1>Choose a new password.</h1>
          <p>
            Create a strong password for your SAFEWAY account. Reset links are single-use and
            time-limited for your security.
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
          <h2>Set a new password</h2>
          <p>Enter and confirm your new password below.</p>

          {!token ? (
            <div className="form-error" role="alert">
              This password reset link is missing or invalid. Please request a new password reset
              link.
            </div>
          ) : null}

          {success ? (
            <div className="form-success" role="status">
              Your password has been reset. You can now sign in with your new password.
            </div>
          ) : (
            <form onSubmit={onSubmit} noValidate>
              <label>
                <span className="field-label">New password</span>
                <span className="password-field">
                  <input
                    type={showNew ? "text" : "password"}
                    autoComplete="new-password"
                    value={newPassword}
                    onChange={(event) => setNewPassword(event.target.value)}
                    minLength={8}
                    required
                    disabled={!token || busy}
                  />
                  <button
                    type="button"
                    className="password-toggle"
                    onClick={() => setShowNew((value) => !value)}
                    aria-label={showNew ? "Hide new password" : "Show new password"}
                    aria-pressed={showNew}
                  >
                    <Icon name={showNew ? "eyeOff" : "eye"} size={18} />
                  </button>
                </span>
              </label>

              <label>
                <span className="field-label">Confirm new password</span>
                <span className="password-field">
                  <input
                    type={showConfirm ? "text" : "password"}
                    autoComplete="new-password"
                    value={confirmPassword}
                    onChange={(event) => setConfirmPassword(event.target.value)}
                    minLength={8}
                    required
                    disabled={!token || busy}
                  />
                  <button
                    type="button"
                    className="password-toggle"
                    onClick={() => setShowConfirm((value) => !value)}
                    aria-label={showConfirm ? "Hide confirm password" : "Show confirm password"}
                    aria-pressed={showConfirm}
                  >
                    <Icon name={showConfirm ? "eyeOff" : "eye"} size={18} />
                  </button>
                </span>
              </label>

              {error ? (
                <div className="form-error" role="alert">
                  {error}
                </div>
              ) : null}

              <button
                className="btn btn-primary login-submit"
                type="submit"
                disabled={busy || !token}
              >
                {busy ? "Resetting password..." : "Reset password"} <Icon name="arrow" />
              </button>
            </form>
          )}

          <p className="register">
            {success ? (
              <Link href="/login">Continue to login</Link>
            ) : (
              <>
                Need a new link? <Link href="/forgot-password">Request password reset</Link>
              </>
            )}
          </p>
          <div className="secure-line">
            <Icon name="shield" size={16} /> Your password is encrypted and never stored in plain
            text.
          </div>
        </div>
      </div>
    </div>
  );
}

export default function ResetPasswordPage() {
  return (
    <Suspense
      fallback={
        <div className="login-page">
          <div className="login-brand-panel">
            <Brand light />
          </div>
          <div className="login-form-panel">
            <div className="login-form">
              <p>Loading...</p>
            </div>
          </div>
        </div>
      }
    >
      <ResetPasswordForm />
    </Suspense>
  );
}
