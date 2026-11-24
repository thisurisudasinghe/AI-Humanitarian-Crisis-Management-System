import { api } from "@/lib/api";

/** Anonymous, privacy-safe projections returned by GET /api/v1/public/map. */
export interface PublicMapIncident {
  id: string;
  title: string;
  summary: string;
  type: string;
  severity: string;
  status: string;
  district?: string | null;
  latitude: number;
  longitude: number;
  publishedAt?: string | null;
  expiresAt?: string | null;
}

export interface PublicMapShelter {
  id: string;
  name: string;
  latitude: number;
  longitude: number;
  capacity: number;
  occupancy: number;
  services?: string | null;
}

export interface PublicMapAidLocation {
  id: string;
  name: string;
  address: string;
  latitude: number;
  longitude: number;
  services?: string | null;
  openingHours?: string | null;
}

export interface PublicMapData {
  updatedAt: string;
  incidents: PublicMapIncident[];
  shelters: PublicMapShelter[];
  aidLocations: PublicMapAidLocation[];
}

export function getPublicMap(signal?: AbortSignal): Promise<PublicMapData> {
  return api<PublicMapData>("/api/v1/public/map", { signal });
}

export type PublicCrisisFilter = "all" | "flood" | "landslide" | "fire" | "severe-weather" | "drought" | "other-crisis";

export function incidentMarkerType(type: string): PublicCrisisFilter {
  const normalized = type.trim().toLowerCase();
  if (normalized.includes("flood") || normalized.includes("water")) return "flood";
  if (normalized.includes("land")) return "landslide";
  if (normalized.includes("fire")) return "fire";
  if (normalized.includes("weather") || normalized.includes("storm") || normalized.includes("cyclone")) return "severe-weather";
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
export function publicMapTypeLabelKey(type: string): string {
  return TYPE_LABEL_KEYS[incidentMarkerType(type)];
}

const SEVERITY_LABEL_KEYS: Record<string, string> = {
  low: "publicMap.severityLow",
  medium: "publicMap.severityMedium",
  high: "publicMap.severityHigh",
  critical: "publicMap.severityCritical"
};

/** Returns an existing translation key for known severities, or null for custom values. */
export function publicMapSeverityLabelKey(severity: string): string | null {
  return SEVERITY_LABEL_KEYS[severity.trim().toLowerCase()] ?? null;
}

/** The public map only ever exposes explicitly approved, published incidents. */
export function publicMapStatusLabelKey(status: string): string | null {
  return status.trim().toLowerCase() === "published" ? "publicMap.published" : null;
}

export function finiteCoordinate(value: number | null | undefined): number | null {
  if (value === null || value === undefined) return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}
