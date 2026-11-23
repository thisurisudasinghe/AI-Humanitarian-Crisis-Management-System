"use client";

import Link from "next/link";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useState, type FormEvent } from "react";
import { Brand } from "@/components/safeway/Brand";
import { Icon } from "@/components/safeway/Icon";
import { LanguageSwitcher } from "@/components/i18n/LanguageSwitcher";
import { apiJson } from "@/lib/api";
import { useT } from "@/lib/i18n";

type ForgotPasswordResponse = {
  message: string;
  emailDeliveryConfigured: boolean;
};

export default function ForgotPasswordPage() {
  const t = useT();
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
      setError(t("auth.emailRequired"));
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
          <h1>{t("auth.forgotHeroTitle")}</h1>
          <p>{t("auth.forgotHeroCopy")}</p>
        </div>
        <blockquote>{t("auth.quote")}</blockquote>
      </div>

      <div className="login-form-panel">
        <div style={{ display: "flex", justifyContent: "space-between", gap: 12, alignItems: "center", flexWrap: "wrap" }}>
          <Link className="back-link" href="/login">{t("auth.backToLoginLink")}</Link>
          <LanguageSwitcher variant="compact" />
        </div>
        <div className="login-form">
          <span className="mobile-brand"><Brand /></span>
          <h2>{t("auth.forgotTitle2")}</h2>
          <p>{t("auth.forgotSubtitle2")}</p>

          <form onSubmit={onSubmit} noValidate>
            <label>
              <span className="field-label">{t("auth.email")}</span>
              <input type="email" placeholder={t("auth.emailPlaceholder")} autoComplete="email" value={email} onChange={(event) => setEmail(event.target.value)} required />
            </label>

            {error ? <div className="form-error" role="alert">{error}</div> : null}
            {result ? (
              <div className={result.emailDeliveryConfigured ? "form-success" : "form-warning"} role="status">
                {result.message}
              </div>
            ) : null}

            <button className="btn btn-primary login-submit" type="submit" disabled={busy}>
              {busy ? t("auth.sendingResetLink") : t("auth.sendResetLink")} <Icon name="arrow" />
            </button>
          </form>

          <p className="register">{t("auth.remembered")} <Link href="/login">{t("auth.backToLoginLink")}</Link></p>
          <div className="secure-line"><Icon name="shield" size={16} /> {t("auth.noConfirmEmail")}</div>
        </div>
      </div>
    </div>
  );
}
