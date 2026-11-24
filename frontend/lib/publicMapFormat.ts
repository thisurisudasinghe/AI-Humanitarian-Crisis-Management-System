export type PublicCrisisFilter =
  | "all"
  | "flood"
  | "landslide"
  | "fire"
  | "severe-weather"
  | "drought"
  | "other-crisis";

export function incidentMarkerType(type: string | null | undefined): PublicCrisisFilter {
  const normalized = (type ?? "").trim().toLowerCase();
  if (normalized.includes("flood") || normalized.includes("water")) return "flood";
  if (normalized.includes("land")) return "landslide";
  if (normalized.includes("fire")) return "fire";
  if (normalized.includes("weather") || normalized.includes("storm") || normalized.includes("cyclone")) {
    return "severe-weather";
  }
  if (normalized.includes("drought")) return "drought";
  return "other-crisis";
}

const TYPE_LABEL_KEYS: Record<PublicCrisisFilter, string> = {
  all: "publicMap.typeAll",
  flood: "publicMap.typeFlood",
  landslide: "publicMap.typeLandslide",
  fire: "publicMap.typeFire",
  "severe-weather": "publicMap.typeSevereWeather",
  drought: "publicMap.typeDrought",
  "other-crisis": "publicMap.typeOtherCrisis"
};

/** Maps API crisis types to existing EN/SI/TA keys instead of constructing keys at runtime. */
export function publicMapTypeLabelKey(type: string | null | undefined): string {
  return TYPE_LABEL_KEYS[incidentMarkerType(type)];
}

const SEVERITY_LABEL_KEYS: Record<string, string> = {
  low: "publicMap.severityLow",
  medium: "publicMap.severityMedium",
  high: "publicMap.severityHigh",
  critical: "publicMap.severityCritical"
};

/** Returns an existing translation key for known severities, or null for missing/custom values. */
export function publicMapSeverityLabelKey(severity: string | null | undefined): string | null {
  if (!severity) return null;
  const key = severity.trim().toLowerCase();
  return key ? SEVERITY_LABEL_KEYS[key] ?? null : null;
}

/** The public map only ever exposes explicitly approved, published incidents. */
export function publicMapStatusLabelKey(status: string | null | undefined): string | null {
  if (!status) return null;
  return status.trim().toLowerCase() === "published" ? "publicMap.published" : null;
}

export function finiteCoordinate(value: number | null | undefined): number | null {
  if (value === null || value === undefined) return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}
