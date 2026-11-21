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

export function humanizeRole(role?: string | null): string {
  if (!role) {
    return "Public account";
  }

  return role.replace(/([a-z])([A-Z])/g, "$1 $2");
}

export function formatShortDate(value?: string | null): string {
  if (!value) {
    return "—";
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}
