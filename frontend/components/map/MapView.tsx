"use client";

// Member 4 / D4-03 stable public component.
// Props: markers={[{ id, lat, lng, label, type }]}, height?.
// Additional optional props (focus, currentLocation, controls) are additive and
// existing callers such as report/shelter/resource forms remain unchanged.
import dynamic from "next/dynamic";
import { useT } from "@/lib/i18n";
import type { MapViewProps } from "./MapViewInner";

function MapLoading() {
  const t = useT();
  return <div className="map map-loading">{t("shared.mapLoading")}</div>;
}

const MapViewInner = dynamic(() => import("./MapViewInner"), {
  ssr: false,
  loading: MapLoading,
});

export type {
  CurrentMapLocation,
  MapFocus,
  MapMarker
} from "./MapViewInner";

export default function MapView(props: MapViewProps) {
  return <MapViewInner {...props} />;
}
