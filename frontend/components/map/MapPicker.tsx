"use client";

// Member 4 / D4-03 stable public component.
// Dynamic import with ssr:false is required: Leaflet touches `window` and must
// never be imported by a server-rendered module. Member 2 can drop this into a
// request form without changing its props.
import dynamic from "next/dynamic";
import { useT } from "@/lib/i18n";
import type { MapPickerProps } from "./MapPickerInner";

function MapLoading() {
  const t = useT();
  return <div className="map map-loading">{t("shared.mapLoading")}</div>;
}

const MapPickerInner = dynamic(() => import("./MapPickerInner"), {
  ssr: false,
  loading: MapLoading,
});

export type { MapPoint } from "./MapPickerInner";

export default function MapPicker(props: MapPickerProps) {
  return <MapPickerInner {...props} />;
}
