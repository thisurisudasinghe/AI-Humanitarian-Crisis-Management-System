"use client";

import { LOCALES, useI18n, type Locale } from "@/lib/i18n";

type Props = {
  variant?: "nav" | "compact" | "light";
  className?: string;
};

export function LanguageSwitcher({ variant = "compact", className = "" }: Props) {
  const { locale, setLocale, t } = useI18n();

  return (
    <div
      className={`lang-switcher lang-switcher--${variant} ${className}`.trim()}
      role="group"
      aria-label={t("common.selectLanguage")}
    >
      <span className="lang-switcher__globe" aria-hidden="true">🌐</span>
      {LOCALES.map((item, index) => (
        <span key={item.code} className="lang-switcher__item">
          <button
            type="button"
            className={`lang-switcher__btn${locale === item.code ? " is-active" : ""}`}
            onClick={() => setLocale(item.code as Locale)}
            aria-pressed={locale === item.code}
            lang={item.code}
          >
            {item.label}
          </button>
          {index < LOCALES.length - 1 ? (
            <span className="lang-switcher__sep" aria-hidden="true">|</span>
          ) : null}
        </span>
      ))}
    </div>
  );
}
