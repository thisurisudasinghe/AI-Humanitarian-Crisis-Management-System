"use client";

import Link from "next/link";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useSearchParams } from "next/navigation";
import { Suspense, useState, type FormEvent } from "react";
import { Brand } from "@/components/safeway/Brand";
import { Icon } from "@/components/safeway/Icon";
import { apiJson } from "@/lib/api";
import { useT } from "@/lib/i18n";

function ResetPasswordForm() {
  const searchParams = useSearchParams();
  const token = (searchParams.get("token") ?? "").trim();
  const t = useT();
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
      setError(t("auth.resetMissingToken"));
      return;
    }
    if (newPassword.length < 8) {
      setError(t("validation.passwordMin"));
      return;
    }
    if (newPassword !== confirmPassword) {
      setError(t("validation.passwordsNoMatch"));
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
      setError(localizedErrorMessage(caught, t));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-page">
      <div className="login-brand-panel">
        <Brand light />
        <div>
          <div className="eyebrow"><span /> {t("auth.accountRecovery")}</div>
          <h1>{t("auth.resetHeroTitle")}</h1>
          <p>{t("auth.resetHeroCopy")}</p>
        </div>
        <blockquote>{t("auth.quote")}</blockquote>
      </div>

      <div className="login-form-panel">
        <div style={{ display: "flex", justifyContent: "space-between", gap: 12, alignItems: "center", flexWrap: "wrap" }}>
          <Link className="back-link" href="/login">{t("auth.backToLoginLink")}</Link>
        </div>
        <div className="login-form">
          <span className="mobile-brand"><Brand /></span>
          <h2>{t("auth.setNewPassword")}</h2>
          <p>{t("auth.resetSubtitle2")}</p>

          {!token ? <div className="form-error" role="alert">{t("auth.resetMissingToken")}</div> : null}

          {success ? (
            <div className="form-success" role="status">{t("auth.resetSuccess2")}</div>
          ) : (
            <form onSubmit={onSubmit} noValidate>
              <label>
                <span className="field-label">{t("auth.newPasswordLabel")}</span>
                <span className="password-field">
                  <input type={showNew ? "text" : "password"} autoComplete="new-password" value={newPassword} onChange={(event) => setNewPassword(event.target.value)} minLength={8} required disabled={!token || busy} />
                  <button type="button" className="password-toggle" onClick={() => setShowNew((value) => !value)} aria-label={showNew ? t("auth.hideNewPassword") : t("auth.showNewPassword")} aria-pressed={showNew}>
                    <Icon name={showNew ? "eyeOff" : "eye"} size={18} />
                  </button>
                </span>
              </label>

              <label>
                <span className="field-label">{t("auth.confirmNewPassword")}</span>
                <span className="password-field">
                  <input type={showConfirm ? "text" : "password"} autoComplete="new-password" value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} minLength={8} required disabled={!token || busy} />
                  <button type="button" className="password-toggle" onClick={() => setShowConfirm((value) => !value)} aria-label={showConfirm ? t("auth.hideConfirmPassword") : t("auth.showConfirmPassword")} aria-pressed={showConfirm}>
                    <Icon name={showConfirm ? "eyeOff" : "eye"} size={18} />
                  </button>
                </span>
              </label>

              {error ? <div className="form-error" role="alert">{error}</div> : null}

              <button className="btn btn-primary login-submit" type="submit" disabled={busy || !token}>
                {busy ? t("auth.resetting") : t("auth.resetPassword")} <Icon name="arrow" />
              </button>
            </form>
          )}

          <p className="register">
            {success ? <Link href="/login">{t("auth.continueToLogin")}</Link> : <>{t("auth.needNewLink")} <Link href="/forgot-password">{t("auth.requestReset")}</Link></>}
          </p>
          <div className="secure-line"><Icon name="shield" size={16} /> {t("auth.passwordEncrypted")}</div>
        </div>
      </div>
    </div>
  );
}

export default function ResetPasswordPage() {
  const t = useT();
  return (
    <Suspense fallback={<div className="login-page"><div className="login-brand-panel"><Brand light /></div><div className="login-form-panel"><div className="login-form"><p>{t("feedback.loading")}</p></div></div></div>}>
      <ResetPasswordForm />
    </Suspense>
  );
}
