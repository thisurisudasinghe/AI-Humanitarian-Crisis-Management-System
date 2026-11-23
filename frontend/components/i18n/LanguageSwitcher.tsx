"use client";

import { useEffect, useId, useRef, useState } from "react";
import { LOCALES, useI18n, type Locale } from "@/lib/i18n";

type Props = {
  variant?: "nav" | "compact" | "light";
  className?: string;
  dropdown?: boolean;
};

function GlobeIcon() {
  return (
    <svg
      className="lang-switcher__globe"
      width="17"
      height="17"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      <circle cx="12" cy="12" r="9" />
      <path d="M3 12h18M12 3c2.4 2.3 3.8 5.4 3.8 9S14.4 18.7 12 21c-2.4-2.3-3.8-5.4-3.8-9S9.6 5.3 12 3Z" />
    </svg>
  );
}

function ChevronIcon() {
  return (
    <svg
      className="lang-switcher__chevron"
      width="13"
      height="13"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      <path d="m6 9 6 6 6-6" />
    </svg>
  );
}

export function LanguageSwitcher({
  variant = "compact",
  className = "",
  dropdown = false
}: Props) {
  const { locale, setLocale, t } = useI18n();
  const [open, setOpen] = useState(false);
  const [focusIndex, setFocusIndex] = useState<number | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const toggleRef = useRef<HTMLButtonElement>(null);
  const optionRefs = useRef<Array<HTMLButtonElement | null>>([]);
  const menuId = useId();
  const selectedIndex = Math.max(
    0,
    LOCALES.findIndex((item) => item.code === locale)
  );

  useEffect(() => {
    if (!dropdown || !open) return;

    function handlePointerDown(event: MouseEvent | TouchEvent) {
      if (!containerRef.current?.contains(event.target as Node)) setOpen(false);
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key !== "Escape") return;
      setOpen(false);
      toggleRef.current?.focus();
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

  useEffect(() => {
    if (!open || focusIndex === null) return;
    optionRefs.current[focusIndex]?.focus();
    setFocusIndex(null);
  }, [open, focusIndex]);

  function focusOption(index: number) {
    const next = (index + LOCALES.length) % LOCALES.length;
    optionRefs.current[next]?.focus();
  }

  function openWithFocus(index: number) {
    setFocusIndex(index);
    setOpen(true);
  }

  function selectLocale(nextLocale: Locale) {
    setLocale(nextLocale);
    setOpen(false);
    toggleRef.current?.focus();
  }

  const wrapperClass = `lang-switcher lang-switcher--${variant}${
    dropdown ? " lang-switcher--dropdown" : ""
  } ${className}`.trim();

  if (dropdown) {
    return (
      <div className={wrapperClass} ref={containerRef}>
        <button
          ref={toggleRef}
          type="button"
          className="lang-switcher__toggle"
          onClick={() => setOpen((value) => !value)}
          onKeyDown={(event) => {
            if (event.key !== "ArrowDown" && event.key !== "ArrowUp") return;
            event.preventDefault();
            openWithFocus(
              event.key === "ArrowDown"
                ? selectedIndex
                : LOCALES.length - 1
            );
          }}
          aria-expanded={open}
          aria-controls={menuId}
          aria-haspopup="menu"
          aria-label={t("common.selectLanguage")}
        >
          <GlobeIcon />
          <ChevronIcon />
        </button>
        <div
          id={menuId}
          className="lang-switcher__menu"
          role="menu"
          aria-label={t("common.selectLanguage")}
          hidden={!open}
        >
          {LOCALES.map((item, index) => {
            const active = locale === item.code;
            return (
              <button
                key={item.code}
                ref={(node) => {
                  optionRefs.current[index] = node;
                }}
                type="button"
                role="menuitemradio"
                className={`lang-switcher__option${active ? " is-active" : ""}`}
                onClick={() => selectLocale(item.code as Locale)}
                onKeyDown={(event) => {
                  if (event.key === "ArrowDown") {
                    event.preventDefault();
                    focusOption(index + 1);
                  } else if (event.key === "ArrowUp") {
                    event.preventDefault();
                    focusOption(index - 1);
                  } else if (event.key === "Home") {
                    event.preventDefault();
                    focusOption(0);
                  } else if (event.key === "End") {
                    event.preventDefault();
                    focusOption(LOCALES.length - 1);
                  }
                }}
                aria-checked={active}
                lang={item.code}
              >
                <span className="lang-switcher__check" aria-hidden="true">
                  {active ? "✓" : ""}
                </span>
                <span>{item.label}</span>
              </button>
            );
          })}
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
      <GlobeIcon />
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
