"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState, type FormEvent } from "react";
import { Brand } from "@/components/safeway/Brand";
import { Icon } from "@/components/safeway/Icon";
import { errorMessage } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { homePathForRoles } from "@/lib/roles";

export default function LoginPage() {
  const router = useRouter();
  const { user, ready, login } = useAuth();
  const [role, setRole] = useState<"public" | "officer">("public");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [show, setShow] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (ready && user) {
      router.replace(
        user.isEmailVerified ? homePathForRoles(user.roles) : "/verify-email"
      );
    }
  }, [ready, user, router]);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setBusy(true);

    try {
      const current = await login(email.trim(), password);
      router.replace(
        current.isEmailVerified ? homePathForRoles(current.roles) : "/verify-email"
      );
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
            <span /> Secure access
          </div>
          <h1>Welcome back to a clearer path forward.</h1>
          <p>
            Access reports, verified assistance and coordinated response tools in one secure place.
          </p>
        </div>
        <blockquote>“Connecting people to help. Coordinating action. Restoring hope.”</blockquote>
      </div>

      <div className="login-form-panel">
        <Link className="back-link" href="/">
          ← Back to home
        </Link>
        <div className="login-form">
          <span className="mobile-brand">
            <Brand />
          </span>
          <h2>Sign in to SAFEWAY</h2>
          <p>Choose your access type to continue.</p>

          <div className="role-toggle" role="group" aria-label="Access type">
            <button
              type="button"
              className={role === "public" ? "active" : ""}
              onClick={() => setRole("public")}
            >
              Public access
            </button>
            <button
              type="button"
              className={role === "officer" ? "active" : ""}
              onClick={() => setRole("officer")}
            >
              Authorized officer
            </button>
          </div>
          <p className="metric__label" style={{ marginTop: 10 }}>
            Your account role decides the workspace. This selection does not change your
            permissions.
          </p>

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

            <label>
              <span className="field-label">
                Password <Link href="/contact">Forgot password?</Link>
              </span>
              <span className="password-field">
                <input
                  type={show ? "text" : "password"}
                  autoComplete="current-password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  required
                />
                <button
                  type="button"
                  onClick={() => setShow((value) => !value)}
                  aria-label={show ? "Hide password" : "Show password"}
                >
                  <Icon name="eye" />
                </button>
              </span>
            </label>

            <label className="consent">
              <input type="checkbox" checked readOnly disabled />
              <span>
                Session-only sign-in. The refresh token stays in this browser tab and is never
                written to local storage.
              </span>
            </label>

            {error ? (
              <div className="form-error" role="alert">
                {error}
              </div>
            ) : null}

            <button className="btn btn-primary login-submit" type="submit" disabled={busy}>
              {busy ? "Signing in…" : "Sign in securely"} <Icon name="arrow" />
            </button>
          </form>

          <p className="register">
            New to SAFEWAY? <Link href="/register">Create an account</Link>
          </p>
          <div className="secure-line">
            <Icon name="shield" size={16} /> Your connection is encrypted and protected.
          </div>
        </div>
      </div>
    </div>
  );
}
