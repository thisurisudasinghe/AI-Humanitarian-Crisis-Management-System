"use client";

import { useEffect, useId, useRef, useState } from "react";
import { LOCALES, useI18n, type Locale } from "@/lib/i18n";

type Props = {
  variant?: "nav" | "compact" | "light";
  className?: string;
  dropdown?: boolean;
};

export function LanguageSwitcher({
  variant = "compact",
  className = "",
  dropdown = false
}: Props) {
  const { locale, setLocale, t } = useI18n();
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const menuId = useId();
  const selected = LOCALES.find((item) => item.code === locale) ?? LOCALES[0];

  useEffect(() => {
    if (!dropdown || !open) return;

    function handlePointerDown(event: MouseEvent | TouchEvent) {
      if (!containerRef.current?.contains(event.target as Node)) setOpen(false);
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key !== "Escape") return;
      setOpen(false);
      containerRef.current?.querySelector<HTMLButtonElement>(".lang-switcher__toggle")?.focus();
    }

    document.addEventListener("mousedown", handlePointerDown);
    document.addEventListener("touchstart", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("mousedown", handlePointerDown);
      document.removeEventListener("touchstart", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [dropdown, open]);

  const wrapperClass = `lang-switcher lang-switcher--${variant}${
    dropdown ? " lang-switcher--dropdown" : ""
  } ${className}`.trim();

  if (dropdown) {
    return (
      <div className={wrapperClass} ref={containerRef}>
        <button
          type="button"
          className="lang-switcher__toggle"
          onClick={() => setOpen((value) => !value)}
          aria-expanded={open}
          aria-controls={menuId}
          aria-label={t("common.selectLanguage")}
        >
          <span className="lang-switcher__globe" aria-hidden="true">🌐</span>
          <span className="lang-switcher__short" aria-hidden="true">{selected.short}</span>
        </button>
        <div id={menuId} className="lang-switcher__menu" hidden={!open}>
          {LOCALES.map((item) => (
            <button
              key={item.code}
              type="button"
              className={`lang-switcher__option${locale === item.code ? " is-active" : ""}`}
              onClick={() => {
                setLocale(item.code as Locale);
                setOpen(false);
              }}
              aria-pressed={locale === item.code}
              lang={item.code}
            >
              {item.label}
            </button>
          ))}
        </div>
      </div>
    );
  }

  return (
    <div
      className={wrapperClass}
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
