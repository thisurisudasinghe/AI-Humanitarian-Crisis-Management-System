"use client";

// Member 4 / D4-03. Client-only implementation loaded by MapView.tsx with
// next/dynamic({ ssr: false }). No business logic and no direct DB access.

import L from "leaflet";
import { MapContainer, Marker, Popup, TileLayer } from "react-leaflet";

export interface MapMarker {
  id: string;
  lat: number;
  lng: number;
  /** Preferred D4-03 fields. `name` is accepted for older callers. */
  label?: string;
  name?: string;
  type?: string;
  description?: string;
}

export interface MapViewProps {
  markers: MapMarker[];
  height?: number;
}

const DEFAULT_CENTRE: [number, number] = [7.8731, 80.7718];

function markerIcon(type: string) {
  const colour =
    type === "shelter" ? "#174b78" : type === "service-centre" ? "#2f7d4f" : type === "area" ? "#a35c00" : "#5b4b8a";
  return L.divIcon({
    className: "cms-map-pin",
    html: `<span aria-hidden="true" class="cms-map-pin-dot" style="background:${colour}"></span>`,
    iconSize: [24, 24],
    iconAnchor: [12, 12],
  });
}

export default function MapViewInner({ markers, height = 420 }: MapViewProps) {
  const valid = markers.filter(
    (marker) => Number.isFinite(marker.lat) && Number.isFinite(marker.lng),
  );
  const centre: [number, number] = valid.length
    ? [valid[0].lat, valid[0].lng]
    : DEFAULT_CENTRE;

  return (
    <div className="map-view">
      <div className="map" style={{ height }}>
        <MapContainer center={centre} zoom={7} style={{ height: "100%", width: "100%" }}>
          <TileLayer
            attribution="&copy; OpenStreetMap contributors"
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          {valid.map((marker) => (
            <Marker
              key={`${marker.type ?? "location"}-${marker.id}`}
              position={[marker.lat, marker.lng]}
              icon={markerIcon(marker.type ?? "location")}
            >
              <Popup>
                <strong>{marker.label ?? marker.name ?? "Location"}</strong>
                <br />
                <span>{marker.description ?? marker.type ?? "Location"}</span>
              </Popup>
            </Marker>
          ))}
        </MapContainer>
      </div>

      {/* Accessible fallback list: always rendered, not hidden behind the tiles. */}
      <section className="panel map-fallback" aria-label="Map locations list">
        <h2>Locations ({valid.length})</h2>
        {valid.length === 0 ? (
          <p className="muted">No locations are available yet.</p>
        ) : (
          <ul>
            {valid.map((marker) => (
              <li key={`${marker.type ?? "location"}-${marker.id}`}>
                <strong>{marker.label ?? marker.name ?? "Location"}</strong> — {marker.type ?? "location"}
                {marker.description ? ` — ${marker.description}` : ""}
                <span className="muted"> ({marker.lat.toFixed(4)}, {marker.lng.toFixed(4)})</span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
