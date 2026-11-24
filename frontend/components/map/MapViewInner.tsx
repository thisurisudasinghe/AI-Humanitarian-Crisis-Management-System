"use client";

// Member 4 / D4-03. Client-only implementation loaded by MapView.tsx with
// next/dynamic({ ssr: false }). No business logic and no direct DB access.

import L from "leaflet";
import { useEffect, useRef } from "react";
import {
  Circle,
  CircleMarker,
  MapContainer,
  Marker,
  Popup,
  TileLayer,
  useMap
} from "react-leaflet";
import { useT } from "@/lib/i18n";
import { MAP_MARKER_TYPES, markerColor, markerLabelKey } from "./mapTypes";
import styles from "./MapView.module.css";
import "./mapMarkers.css";

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

export interface MapFocus {
  lat: number;
  lng: number;
  zoom?: number;
  /** Increment to re-focus on the same coordinates. */
  nonce?: number;
}

export interface CurrentMapLocation {
  lat: number;
  lng: number;
  accuracy?: number;
  timestamp?: number;
}

export interface MapViewProps {
  markers: MapMarker[];
  height?: number;
  focus?: MapFocus | null;
  fitNonce?: number;
  currentLocation?: CurrentMapLocation | null;
  onMyLocation?: () => void;
  onFitAll?: () => void;
  canLocate?: boolean;
  selectedId?: string | null;
  onMarkerSelect?: (marker: MapMarker) => void;
  showLegend?: boolean;
}

const DEFAULT_CENTRE: [number, number] = [7.8731, 80.7718];

/** Simple inline glyphs keep disaster markers distinguishable without colour alone. */
const MARKER_ICON_PATHS: Record<string, string> = {
  flood: "M2 8c2 0 2-2 4-2s2 2 4 2 2-2 4-2 2 2 4 2 2-2 4-2M2 14c2 0 2-2 4-2s2 2 4 2 2-2 4-2 2 2 4 2 2-2 4-2",
  landslide: "m3 19 6-10 4 6 2-3 6 7H3Z",
  fire: "M12 22c4 0 7-2.7 7-6.5 0-4-3.2-6.4-4.8-9.5-.5-1-2-1-2.4 0C11 8.5 9 10 9 13c-1-.5-2-1.6-2-3-2 1.6-2 3.8-2 5.5C5 19.3 8 22 12 22Z",
  "severe-weather": "M6 16a4 4 0 0 1 .5-8 6 6 0 0 1 11.3 1.6A3.5 3.5 0 0 1 18 16H6Zm6-2-2 4h3l-1 3 4-5h-3l1-2h-3Z",
  drought: "M12 7a5 5 0 1 0 0 10 5 5 0 0 0 0-10ZM12 2v2m0 16v2M4.9 4.9l1.4 1.4m11.4 11.4 1.4 1.4M2 12h2m16 0h2M4.9 19.1l1.4-1.4M17.7 6.3l-1.4 1.4",
  medical: "M12 5v14M5 12h14",
  "other-crisis": "M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Zm0-13h.01M11 12h1v5h1",
  "public-shelter": "m3 11 9-8 9 8v9a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1v-9Z",
  "aid-centre": "M12 3v18M3 12h18"
};

function markerIcon(type: string | undefined, selected: boolean) {
  const colour = markerColor(type);
  const ring = selected
    ? "outline:3px solid #facc15;outline-offset:2px;"
    : "";
  const path = type ? MARKER_ICON_PATHS[type] : undefined;
  const icon = path
    ? `<svg viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="${path}" /></svg>`
    : "";
  const classes = `cms-map-pin-dot${path ? " has-icon" : ""}`;
  return L.divIcon({
    className: "cms-map-pin",
    html: `<span aria-hidden="true" class="${classes}" style="background:${colour};${ring}">${icon}</span>`,
    iconSize: [24, 24],
    iconAnchor: [12, 12]
  });
}

function isPoint(marker: MapMarker): boolean {
  return Number.isFinite(marker.lat) && Number.isFinite(marker.lng);
}

function MapController({
  focus,
  fitNonce,
  markers
}: {
  focus?: MapFocus | null;
  fitNonce?: number;
  markers: MapMarker[];
}) {
  const map = useMap();
  const markersRef = useRef(markers);
  markersRef.current = markers;

  useEffect(() => {
    if (!focus || !Number.isFinite(focus.lat) || !Number.isFinite(focus.lng)) return;
    map.flyTo(
      [focus.lat, focus.lng],
      focus.zoom ?? Math.max(map.getZoom(), 13),
      { duration: 0.8 }
    );
  }, [map, focus?.lat, focus?.lng, focus?.zoom, focus?.nonce]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (!fitNonce) return;
    const points = markersRef.current
      .filter(isPoint)
      .map((marker) => [marker.lat, marker.lng] as [number, number]);
    if (points.length === 0) {
      map.setView(DEFAULT_CENTRE, 7);
    } else if (points.length === 1) {
      map.setView(points[0], 13);
    } else {
      map.fitBounds(L.latLngBounds(points), { padding: [32, 32], maxZoom: 14 });
    }
  }, [map, fitNonce]);

  return null;
}

export default function MapViewInner({
  markers,
  height = 420,
  focus = null,
  fitNonce = 0,
  currentLocation = null,
  onMyLocation,
  onFitAll,
  canLocate = false,
  selectedId = null,
  onMarkerSelect,
  showLegend = true
}: MapViewProps) {
  const t = useT();
  const valid = markers.filter(isPoint);
  const regular = valid.filter((marker) => marker.type !== "current-location");
  const centre: [number, number] = valid.length
    ? [valid[0].lat, valid[0].lng]
    : DEFAULT_CENTRE;
  const locationValid =
    currentLocation &&
    Number.isFinite(currentLocation.lat) &&
    Number.isFinite(currentLocation.lng);

  return (
    <div className="map-view">
      <div className={styles.mapShell}>
        <div className={`map ${styles.mapCanvas}`} style={{ height }}>
          <MapContainer center={centre} zoom={7} style={{ height: "100%", width: "100%" }}>
            <TileLayer
              attribution="&copy; OpenStreetMap contributors"
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            <MapController focus={focus} fitNonce={fitNonce} markers={valid} />

            {locationValid && (
              <>
                <Circle
                  center={[currentLocation!.lat, currentLocation!.lng]}
                  radius={Math.max(currentLocation!.accuracy ?? 25, 10)}
                  pathOptions={{
                    color: "#1d4ed8",
                    weight: 1,
                    fillColor: "#60a5fa",
                    fillOpacity: 0.14
                  }}
                />
                <CircleMarker
                  center={[currentLocation!.lat, currentLocation!.lng]}
                  radius={8}
                  pathOptions={{
                    color: "#ffffff",
                    weight: 3,
                    fillColor: MAP_MARKER_TYPES["current-location"].color,
                    fillOpacity: 1
                  }}
                >
                  <Popup>
                    <strong>{t(markerLabelKey("current-location"))}</strong>
                    <br />
                    {currentLocation!.accuracy != null
                      ? t("map.gpsAccuracy", { accuracy: Math.round(currentLocation!.accuracy) })
                      : t("map.gpsAccuracyUnavailable")}
                    <br />
                    <small>{t("map.browserOnly")}</small>
                  </Popup>
                </CircleMarker>
              </>
            )}

            {regular.map((marker) => (
              <Marker
                key={`${marker.type ?? "location"}-${marker.id}`}
                position={[marker.lat, marker.lng]}
                icon={markerIcon(marker.type, selectedId === marker.id)}
                eventHandlers={{ click: () => onMarkerSelect?.(marker) }}
              >
                <Popup>
                  <strong>{marker.label ?? marker.name ?? t("map.location")}</strong>
                  <br />
                  <span>{marker.description ?? t(markerLabelKey(marker.type))}</span>
                </Popup>
              </Marker>
            ))}
          </MapContainer>

          <div className={styles.controls} role="group" aria-label={t("map.controls")}>
            <button
              type="button"
              onClick={onFitAll}
              disabled={valid.length === 0}
              title={t("map.fitAllTitle")}
            >
              {t("map.fitAll")}
            </button>
            <button
              type="button"
              onClick={onMyLocation}
              disabled={!canLocate}
              title={
                canLocate
                  ? t("map.myLocationTitle")
                  : t("map.myLocationDisabledTitle")
              }
            >
              {t("map.myLocation")}
            </button>
          </div>

          {showLegend && (
            <div className={styles.legend} aria-label={t("map.legend")}>
              <p className={styles.legendTitle}>{t("map.legend")}</p>
              <ul className={styles.legendList}>
                {Object.entries(MAP_MARKER_TYPES)
                  .filter(([key]) => key !== "location")
                  .map(([key, value]) => (
                    <li key={key} className={styles.legendItem}>
                      <span
                        className={styles.legendSwatch}
                        style={{ background: value.color }}
                        aria-hidden="true"
                      />
                      <span>{t(markerLabelKey(key))}</span>
                    </li>
                  ))}
              </ul>
            </div>
          )}
        </div>
      </div>

      {/* Accessible fallback list: always rendered, not hidden behind the tiles. */}
      <section className={`panel map-fallback ${styles.fallback}`} aria-label={t("map.mapList")}>
        <h2>{t("map.locations", { count: String(valid.length) })}</h2>
        {valid.length === 0 ? (
          <p className="muted">{t("map.noFilterLocations")}</p>
        ) : (
          <ul>
            {valid.map((marker) => (
              <li key={`${marker.type ?? "location"}-${marker.id}`}>
                <button
                  type="button"
                  className={`${styles.listButton} ${selectedId === marker.id ? styles.selected : ""}`}
                  onClick={() => onMarkerSelect?.(marker)}
                >
                  <strong>{marker.label ?? marker.name ?? t("map.location")}</strong> — {t(markerLabelKey(marker.type))}
                  {marker.description ? ` — ${marker.description}` : ""}
                  <span className="muted"> ({marker.lat.toFixed(4)}, {marker.lng.toFixed(4)})</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
