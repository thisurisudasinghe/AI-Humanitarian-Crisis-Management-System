// Member 4 mapping marker vocabulary. Shared by the map renderer and the
// operations map page so colours, labels and filters cannot drift apart.

export const MAP_MARKER_TYPES = {
  shelter: { label: "Shelter", color: "#174b78" },
  "service-centre": { label: "Service centre", color: "#2f7d4f" },
  resource: { label: "Resource stock", color: "#a35c00" },
  volunteer: { label: "Verified volunteer", color: "#7c3aed" },
  "volunteer-task": { label: "Volunteer task", color: "#c2410c" },
  area: { label: "Operational area", color: "#b45309" },
  "search-result": { label: "Search result", color: "#0f766e" },
  "current-location": { label: "Your current location", color: "#1d4ed8" },
  location: { label: "Location", color: "#5b4b8a" },
  flood: { label: "Flood", color: "#2f6fb0" },
  landslide: { label: "Landslide", color: "#8a6a4f" },
  fire: { label: "Fire", color: "#b6433e" },
  "severe-weather": { label: "Severe weather", color: "#7356a8" },
  drought: { label: "Drought", color: "#a78b32" },
  medical: { label: "Medical emergency", color: "#a63a3a" },
  "other-crisis": { label: "Other crisis", color: "#667085" },
  "public-shelter": { label: "Public shelter", color: "#2f7d4f" },
  "aid-centre": { label: "Aid centre", color: "#0f766e" }
} as const;

export type MapMarkerType = keyof typeof MAP_MARKER_TYPES;

export const MARKER_LABEL_KEYS: Record<MapMarkerType, string> = {
  shelter: "map.markerTypes.shelter",
  "service-centre": "map.markerTypes.serviceCentre",
  resource: "map.markerTypes.resource",
  volunteer: "map.markerTypes.volunteer",
  "volunteer-task": "map.markerTypes.volunteerTask",
  area: "map.markerTypes.area",
  "search-result": "map.markerTypes.searchResult",
  "current-location": "map.markerTypes.currentLocation",
  location: "map.markerTypes.location",
  flood: "map.markerTypes.flood",
  landslide: "map.markerTypes.landslide",
  fire: "map.markerTypes.fire",
  "severe-weather": "map.markerTypes.severeWeather",
  drought: "map.markerTypes.drought",
  medical: "map.markerTypes.medical",
  "other-crisis": "map.markerTypes.otherCrisis",
  "public-shelter": "map.markerTypes.publicShelter",
  "aid-centre": "map.markerTypes.aidCentre"
};

export function markerLabelKey(type?: string): string {
  if (!type) return MARKER_LABEL_KEYS.location;
  return MARKER_LABEL_KEYS[type as MapMarkerType] ?? "map.markerTypes.location";
}

export function markerLabel(type?: string): string {
  if (!type) return MAP_MARKER_TYPES.location.label;
  return MAP_MARKER_TYPES[type as MapMarkerType]?.label ?? type;
}

export function markerColor(type?: string): string {
  if (!type) return MAP_MARKER_TYPES.location.color;
  return MAP_MARKER_TYPES[type as MapMarkerType]?.color ?? MAP_MARKER_TYPES.location.color;
}
