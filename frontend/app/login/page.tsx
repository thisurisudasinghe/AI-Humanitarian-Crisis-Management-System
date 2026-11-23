"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState, type FormEvent } from "react";
import { Brand } from "@/components/safeway/Brand";
import { Icon } from "@/components/safeway/Icon";
import { LanguageSwitcher } from "@/components/i18n/LanguageSwitcher";
import { errorMessage } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useT } from "@/lib/i18n";
import { homePathForRoles } from "@/lib/roles";

export default function LoginPage() {
  const router = useRouter();
  const { user, ready, login } = useAuth();
  const t = useT();
  const [role, setRole] = useState<"public" | "officer">("public");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [show, setShow] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (ready && user) {
      router.replace(homePathForRoles(user.roles));
    }
  }, [ready, user, router]);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setBusy(true);

    try {
      const current = await login(email.trim(), password);
      router.replace(homePathForRoles(current.roles));
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
            <span /> {t("auth.secureAccess")}
          </div>
          <h1>{t("auth.loginHeroTitle")}</h1>
          <p>{t("auth.loginHeroCopy")}</p>
        </div>
        <blockquote>{t("auth.quote")}</blockquote>
      </div>

      <div className="login-form-panel">
        <div style={{ display: "flex", justifyContent: "space-between", gap: 12, alignItems: "center", flexWrap: "wrap" }}>
          <Link className="back-link" href="/">{t("common.backToHome")}</Link>
          <LanguageSwitcher variant="compact" />
        </div>
        <div className="login-form">
          <span className="mobile-brand"><Brand /></span>
          <h2>{t("auth.loginTitle")}</h2>
          <p>{t("auth.loginSubtitle")}</p>

          <div className="role-toggle" role="group" aria-label={t("auth.loginSubtitle")}>
            <button type="button" className={role === "public" ? "active" : ""} aria-pressed={role === "public"} onClick={() => setRole("public")}>
              {t("auth.publicAccess")}
            </button>
            <button type="button" className={role === "officer" ? "active" : ""} aria-pressed={role === "officer"} onClick={() => setRole("officer")}>
              {t("auth.authorizedOfficer")}
            </button>
          </div>
          <p className="metric__label" style={{ marginTop: 10 }}>{t("auth.roleHelp")}</p>

          <form onSubmit={onSubmit} noValidate>
            <label>
              <span className="field-label">{t("auth.email")}</span>
              <input type="email" placeholder={t("auth.emailPlaceholder")} autoComplete="email" value={email} onChange={(event) => setEmail(event.target.value)} required />
            </label>

            <label>
              <span className="field-label">
                {t("auth.password")} <Link href="/forgot-password">{t("auth.forgotPassword")}</Link>
              </span>
              <span className="password-field">
                <input type={show ? "text" : "password"} autoComplete="current-password" value={password} onChange={(event) => setPassword(event.target.value)} required />
                <button type="button" className="password-toggle" onClick={() => setShow((value) => !value)} aria-label={show ? t("auth.hidePassword") : t("auth.showPassword")} aria-pressed={show}>
                  <Icon name={show ? "eyeOff" : "eye"} size={18} />
                </button>
              </span>
            </label>

            <label className="consent">
              <input type="checkbox" checked readOnly disabled />
              <span>{t("auth.sessionOnly")}</span>
            </label>

            {error ? <div className="form-error" role="alert">{error}</div> : null}

            <button className="btn btn-primary login-submit" type="submit" disabled={busy}>
              {busy ? t("auth.signingIn") : t("auth.signInSecurely")} <Icon name="arrow" />
            </button>
          </form>

          <p className="register">{t("auth.newToSafeway")} <Link href="/register">{t("auth.createAccount")}</Link></p>
          <div className="secure-line"><Icon name="shield" size={16} /> {t("auth.encrypted")}</div>
        </div>
      </div>
    </div>
  );
}
