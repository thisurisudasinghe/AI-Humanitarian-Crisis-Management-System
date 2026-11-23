import { formatDate as formatDateForLocale, resolveLocale } from "./i18n/core";

type Translator = (key: string, vars?: Record<string, string | number>) => string;

export function initials(name?: string | null): string {
  if (!name) {
    return "SW";
  }

  const parts = name
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2);

  if (parts.length === 0) {
    return "SW";
  }

  return parts.map((part) => part[0]?.toUpperCase() ?? "").join("");
}

export function humanizeRole(role?: string | null, t?: Translator): string {
  if (!role) {
    return t ? t("auth.publicAccount") : "Public account";
  }

  if (t) {
    const key = `roleLabels.${role}`;
    const label = t(key);
    if (label !== key) return label;
  }

  return role.replace(/([a-z])([A-Z])/g, "$1 $2");
}

export function formatShortDate(value?: string | null): string {
  if (!value) {
    return "—";
  }

  const locale = typeof document === "undefined" ? "en" : resolveLocale(document.documentElement.lang);
  return formatDateForLocale(value, locale);
}
