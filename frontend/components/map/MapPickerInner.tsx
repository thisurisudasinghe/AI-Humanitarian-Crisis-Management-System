"use client";

// Member 4 / D4-03. Client-only implementation loaded through MapPicker.tsx
// with next/dynamic({ ssr: false }). Leaflet is never imported at module top
// level of a server-rendered component.

import { useEffect, useState } from "react";
import L from "leaflet";
import { MapContainer, Marker, TileLayer, useMap, useMapEvents } from "react-leaflet";
import { api, errorMessage } from "@/lib/operations-api";

export interface MapPoint {
  lat: number;
  lng: number;
  address?: string;
}

export interface MapPickerProps {
  value?: MapPoint;
  onChange: (point: MapPoint) => void;
  height?: number;
  disabled?: boolean;
}

const DEFAULT_CENTRE: [number, number] = [7.8731, 80.7718];

// A local divIcon avoids depending on marker images hosted by a CDN.
const pinIcon = L.divIcon({
  className: "cms-map-pin",
  html: '<span aria-hidden="true" class="cms-map-pin-dot"></span>',
  iconSize: [24, 24],
  iconAnchor: [12, 12],
});

function KeyboardPan({ enabled }: { enabled: boolean }) {
  const map = useMap();
  useEffect(() => {
    const container = map.getContainer();
    container.tabIndex = 0;
    container.setAttribute("role", "application");
    container.setAttribute("aria-label", "Location map. Use arrow keys to pan, or the coordinate fields below.");
    const onKeyDown = (event: KeyboardEvent) => {
      if (!enabled) return;
      const step = 64;
      const moves: Record<string, [number, number]> = {
        ArrowLeft: [step, 0],
        ArrowRight: [-step, 0],
        ArrowUp: [0, step],
        ArrowDown: [0, -step],
      };
      const delta = moves[event.key];
      if (delta) {
        event.preventDefault();
        map.panBy(delta, { animate: true });
      }
    };
    container.addEventListener("keydown", onKeyDown);
    return () => container.removeEventListener("keydown", onKeyDown);
  }, [map, enabled]);
  return null;
}

function ClickToPin({
  disabled,
  onPick,
}: {
  disabled: boolean;
  onPick: (lat: number, lng: number) => void;
}) {
  useMapEvents({
    click(event) {
      if (!disabled) onPick(event.latlng.lat, event.latlng.lng);
    },
  });
  return null;
}

export default function MapPickerInner({
  value,
  onChange,
  height = 420,
  disabled = false,
}: MapPickerProps) {
  const [point, setPoint] = useState<MapPoint>(value ?? { lat: DEFAULT_CENTRE[0], lng: DEFAULT_CENTRE[1] });
  const [address, setAddress] = useState(value?.address ?? "");
  const [manual, setManual] = useState(false);
  const [approximate, setApproximate] = useState(false);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState("");

  useEffect(() => {
    if (value) {
      setPoint(value);
      setAddress(value.address ?? "");
    }
  }, [value?.lat, value?.lng, value?.address]); // eslint-disable-line react-hooks/exhaustive-deps

  const commit = (next: MapPoint) => {
    setPoint(next);
    onChange(next);
  };

  const search = async () => {
    if (!address.trim() || disabled) return;
    setBusy(true);
    setNotice("");
    try {
      const found = await api<{ lat: number; lng: number } | null>(
        `/geo/geocode?address=${encodeURIComponent(address.trim())}`,
      );
      if (!found) throw new Error("No coordinates were found for that address.");
      setApproximate(false);
      setManual(false);
      commit({ lat: Number(found.lat), lng: Number(found.lng), address: address.trim() });
    } catch (error) {
      // Geocoder offline / 404: soft fallback, never throw to the page.
      setApproximate(true);
      setManual(true);
      setNotice(`${errorMessage(error)} Enter latitude and longitude manually.`);
    } finally {
      setBusy(false);
    }
  };

  const updateManual = (field: "lat" | "lng", raw: string) => {
    const parsed = Number(raw);
    if (!Number.isFinite(parsed)) return;
    commit({ ...point, [field]: parsed });
  };

  return (
    <div className="map-picker">
      <div className="map-picker-toolbar">
        <label htmlFor="map-picker-address">Address search</label>
        <div className="inline">
          <input
            id="map-picker-address"
            type="search"
            value={address}
            disabled={disabled || busy}
            placeholder="e.g. Negombo, Sri Lanka"
            onChange={(event) => setAddress(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === "Enter") {
                event.preventDefault();
                void search();
              }
            }}
          />
          <button type="button" onClick={() => void search()} disabled={disabled || busy || !address.trim()}>
            {busy ? "Searching…" : "Find on map"}
          </button>
          <button type="button" disabled={disabled} onClick={() => setManual((current) => !current)}>
            {manual ? "Hide coordinates" : "Enter coordinates manually"}
          </button>
        </div>
        <p className="muted small">
          Click the map to drop a pin. Focus the map and use arrow keys to pan.
        </p>
      </div>

      <div className="map" style={{ height }}>
        <MapContainer center={[point.lat, point.lng]} zoom={8} style={{ height: "100%", width: "100%" }}>
          <TileLayer
            attribution="&copy; OpenStreetMap contributors"
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          <KeyboardPan enabled={!disabled} />
          <ClickToPin
            disabled={disabled}
            onPick={(lat, lng) => {
              setApproximate(true);
              commit({ ...point, lat, lng });
            }}
          />
          <Marker position={[point.lat, point.lng]} icon={pinIcon} />
        </MapContainer>
      </div>

      {(manual || approximate) && (
        <div className="map-picker-manual panel" aria-live="polite">
          {approximate && <p className="badge badge-warn">Approximate / manually placed point</p>}
          {notice && <p className="error small">{notice}</p>}
          <div className="inline">
            <label htmlFor="map-picker-lat">Latitude</label>
            <input
              id="map-picker-lat"
              type="number"
              step="any"
              min={-90}
              max={90}
              disabled={disabled}
              value={point.lat}
              onChange={(event) => updateManual("lat", event.target.value)}
            />
            <label htmlFor="map-picker-lng">Longitude</label>
            <input
              id="map-picker-lng"
              type="number"
              step="any"
              min={-180}
              max={180}
              disabled={disabled}
              value={point.lng}
              onChange={(event) => updateManual("lng", event.target.value)}
            />
          </div>
        </div>
      )}
    </div>
  );
}
