export type Locale = "en" | "si" | "ta";

export const LOCALES: { code: Locale; label: string; short: string }[] = [
  { code: "en", label: "English", short: "EN" },
  { code: "si", label: "සිංහල", short: "SI" },
  { code: "ta", label: "தமிழ்", short: "TA" }
];

export const STORAGE_KEY = "safeway.locale";
export const DEFAULT_LOCALE: Locale = "en";

export type Vars = Record<string, string | number>;

export function isLocale(value: unknown): value is Locale {
  return value === "en" || value === "si" || value === "ta";
}

export function resolveLocale(value: unknown): Locale {
  return isLocale(value) ? value : DEFAULT_LOCALE;
}

export function getByPath(obj: unknown, path: string): string | undefined {
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

export function interpolate(template: string, vars?: Vars): string {
  if (!vars) return template;
  return template.replace(/\{(\w+)\}/g, (_, key: string) => {
    const value = vars[key];
    return value === undefined || value === null ? `{${key}}` : String(value);
  });
}

export interface TranslatorOptions {
  fallbackLocale?: string;
  onMissing?: (key: string) => void;
}

export function createTranslator(
  dictionaries: Record<string, unknown>,
  locale: string,
  options: TranslatorOptions = {}
): (key: string, vars?: Vars) => string {
  const fallbackLocale = options.fallbackLocale ?? DEFAULT_LOCALE;

  return function translate(key: string, vars?: Vars): string {
    const primary = getByPath(dictionaries[locale], key);
    if (primary !== undefined) return interpolate(primary, vars);

    const fallback = getByPath(dictionaries[fallbackLocale], key);
    if (fallback !== undefined) return interpolate(fallback, vars);

    options.onMissing?.(key);
    return key;
  };
}

export function formatDate(
  value: string | number | Date | null | undefined,
  locale: Locale,
  options?: Intl.DateTimeFormatOptions
): string {
  if (!value) return "—";
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return String(value);
  const loc = locale === "si" ? "si-LK" : locale === "ta" ? "ta-LK" : "en-LK";
  return new Intl.DateTimeFormat(loc, options ?? { dateStyle: "medium", timeStyle: "short" }).format(date);
}

export function formatNumber(
  value: number,
  locale: Locale,
  options?: Intl.NumberFormatOptions
): string {
  const loc = locale === "si" ? "si-LK" : locale === "ta" ? "ta-LK" : "en-LK";
  return new Intl.NumberFormat(loc, options).format(value);
}
