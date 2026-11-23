"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode
} from "react";
import { en, type Dictionary } from "./locales/en";
import { si } from "./locales/si";
import { ta } from "./locales/ta";

export type Locale = "en" | "si" | "ta";

export const LOCALES: { code: Locale; label: string; short: string }[] = [
  { code: "en", label: "English", short: "EN" },
  { code: "si", label: "සිංහල", short: "SI" },
  { code: "ta", label: "தமிழ்", short: "TA" }
];

const STORAGE_KEY = "safeway.locale";
const DEFAULT_LOCALE: Locale = "en";

const dictionaries: Record<Locale, Dictionary> = { en, si, ta };

type Vars = Record<string, string | number>;

function getByPath(obj: unknown, path: string): string | undefined {
  const parts = path.split(".");
  let cur: unknown = obj;
  for (const part of parts) {
    if (!cur || typeof cur !== "object" || !(part in (cur as Record<string, unknown>))) {
      return undefined;
    }
    cur = (cur as Record<string, unknown>)[part];
  }
  return typeof cur === "string" ? cur : undefined;
}

function interpolate(template: string, vars?: Vars): string {
  if (!vars) return template;
  return template.replace(/\{(\w+)\}/g, (_, key: string) => {
    const val = vars[key];
    return val === undefined || val === null ? `{${key}}` : String(val);
  });
}

export function isLocale(value: unknown): value is Locale {
  return value === "en" || value === "si" || value === "ta";
}

export function resolveLocale(value: unknown): Locale {
  return isLocale(value) ? value : DEFAULT_LOCALE;
}

function readStoredLocale(): Locale {
  if (typeof window === "undefined") return DEFAULT_LOCALE;
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY);
    return resolveLocale(stored);
  } catch {
    return DEFAULT_LOCALE;
  }
}

interface I18nContextValue {
  locale: Locale;
  setLocale: (locale: Locale) => void;
  t: (key: string, vars?: Vars) => string;
  dict: Dictionary;
}

const I18nContext = createContext<I18nContextValue | null>(null);

export function I18nProvider({ children }: { children: ReactNode }) {
  const [locale, setLocaleState] = useState<Locale>(DEFAULT_LOCALE);

  useEffect(() => {
    const stored = readStoredLocale();
    setLocaleState(stored);
    document.documentElement.lang = stored;
  }, []);

  const setLocale = useCallback((next: Locale) => {
    setLocaleState(next);
    try {
      window.localStorage.setItem(STORAGE_KEY, next);
    } catch {
      // ignore storage failures
    }
    document.documentElement.lang = next;
  }, []);

  const dict = dictionaries[locale];

  const t = useCallback(
    (key: string, vars?: Vars): string => {
      const primary = getByPath(dict, key);
      if (primary !== undefined) return interpolate(primary, vars);
      const fallback = getByPath(en, key);
      if (fallback !== undefined) return interpolate(fallback, vars);
      if (process.env.NODE_ENV !== "production") {
        console.warn(`[i18n] Missing key: ${key}`);
      }
      return key;
    },
    [dict]
  );

  const value = useMemo(() => ({ locale, setLocale, t, dict }), [locale, setLocale, t, dict]);

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n(): I18nContextValue {
  const ctx = useContext(I18nContext);
  if (!ctx) throw new Error("useI18n must be used within I18nProvider");
  return ctx;
}

export function useT() {
  return useI18n().t;
}

export function formatDate(value: string | number | Date | null | undefined, locale: Locale, options?: Intl.DateTimeFormatOptions): string {
  if (!value) return "—";
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return String(value);
  const loc = locale === "si" ? "si-LK" : locale === "ta" ? "ta-LK" : "en-LK";
  return new Intl.DateTimeFormat(loc, options ?? { dateStyle: "medium", timeStyle: "short" }).format(date);
}

export function formatNumber(value: number, locale: Locale, options?: Intl.NumberFormatOptions): string {
  const loc = locale === "si" ? "si-LK" : locale === "ta" ? "ta-LK" : "en-LK";
  return new Intl.NumberFormat(loc, options).format(value);
}
