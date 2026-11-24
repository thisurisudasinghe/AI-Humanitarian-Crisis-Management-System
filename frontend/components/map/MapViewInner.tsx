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

function markerIcon(type: string | undefined, selected: boolean) {
  const colour = markerColor(type);
  const ring = selected
    ? "outline:3px solid #facc15;outline-offset:2px;"
    : "";
  return L.divIcon({
    className: "cms-map-pin",
    html: `<span aria-hidden="true" class="cms-map-pin-dot" style="background:${colour};${ring}"></span>`,
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
