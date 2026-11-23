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
  location: { label: "Location", color: "#5b4b8a" }
} as const;

export type MapMarkerType = keyof typeof MAP_MARKER_TYPES;

export function markerLabel(type?: string): string {
  if (!type) return MAP_MARKER_TYPES.location.label;
  return MAP_MARKER_TYPES[type as MapMarkerType]?.label ?? type;
}

export function markerColor(type?: string): string {
  if (!type) return MAP_MARKER_TYPES.location.color;
  return MAP_MARKER_TYPES[type as MapMarkerType]?.color ?? MAP_MARKER_TYPES.location.color;
}
