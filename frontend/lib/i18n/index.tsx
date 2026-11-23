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
import {
  createTranslator,
  resolveLocale,
  DEFAULT_LOCALE,
  STORAGE_KEY,
  type Locale,
  type Vars
} from "./core";

export { LOCALES, isLocale, resolveLocale, formatDate, formatNumber } from "./core";
export type { Locale } from "./core";

const dictionaries: Record<Locale, Dictionary> = { en, si, ta };

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

  const t = useMemo(
    () =>
      createTranslator(dictionaries, locale, {
        onMissing: (key) => {
          if (process.env.NODE_ENV !== "production") {
            console.warn(`[i18n] Missing key: ${key}`);
          }
        }
      }),
    [locale]
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
