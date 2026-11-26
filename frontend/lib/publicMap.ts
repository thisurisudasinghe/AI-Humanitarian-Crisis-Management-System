import { api } from "@/lib/api";

/** Anonymous, privacy-safe projections returned by GET /api/v1/public/map. */
export interface PublicMapIncident {
  id: string;
  title: string | null;
  summary: string | null;
  type: string | null;
  severity: string | null;
  status: string;
  district?: string | null;
  latitude: number | null;
  longitude: number | null;
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
