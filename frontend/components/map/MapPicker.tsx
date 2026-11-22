"use client";

// Member 4 / D4-03 stable public component.
// Dynamic import with ssr:false is required: Leaflet touches `window` and must
// never be imported by a server-rendered module. Member 2 can drop this into a
// request form without changing its props.
import dynamic from "next/dynamic";
import type { MapPickerProps } from "./MapPickerInner";

const MapPickerInner = dynamic(() => import("./MapPickerInner"), {
  ssr: false,
  loading: () => <div className="map map-loading">Loading map…</div>,
});

export type { MapPoint } from "./MapPickerInner";

export default function MapPicker(props: MapPickerProps) {
  return <MapPickerInner {...props} />;
}
