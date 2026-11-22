"use client";

// Member 4 / D4-03 stable public component.
// Props: markers={[{ id, lat, lng, label, type }]}, height?.
import dynamic from "next/dynamic";
import type { MapViewProps } from "./MapViewInner";

const MapViewInner = dynamic(() => import("./MapViewInner"), {
  ssr: false,
  loading: () => <div className="map map-loading">Loading map…</div>,
});

export type { MapMarker } from "./MapViewInner";

export default function MapView(props: MapViewProps) {
  return <MapViewInner {...props} />;
}
