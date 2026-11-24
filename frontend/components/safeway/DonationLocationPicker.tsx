"use client";

import dynamic from "next/dynamic";
import { useState } from "react";
import type { MapPoint } from "@/components/map/MapPicker";
import { hasValidPoint, mapLink } from "@/lib/donor";
import { useT } from "@/lib/i18n";
import { Icon } from "./Icon";
import { PortalNotice } from "./PortalPrimitives";

const MapPicker = dynamic(() => import("@/components/map/MapPicker"), { ssr: false });

export interface DonorLocationValue {
  lat: number;
  lng: number;
  label: string;
  address: string;
}

/**
 * Location is chosen through the shared address search, map pin or device GPS.
 * The donor never has to type raw coordinates; the API still validates them.
 */
export function DonationLocationPicker({
  lat,
  lng,
  label,
  address = "",
  onChange
}: {
  lat: number | null;
  lng: number | null;
  label: string;
  address?: string;
  onChange: (value: DonorLocationValue | null) => void;
}) {
  const t = useT();
  const [locating, setLocating] = useState(false);
  const [message, setMessage] = useState<{ text: string; tone: "success" | "error" } | null>(null);
  const valid = hasValidPoint(lat, lng);

  function describe(address?: string): string {
    return address?.trim() || t("donorX.location.saved");
  }

  function commit(point: MapPoint | null) {
    if (!point) {
      onChange(null);
      setMessage({ text: t("donorX.location.cleared"), tone: "success" });
      return;
    }
    if (!hasValidPoint(point.lat, point.lng)) {
      setMessage({ text: t("donorX.location.invalid"), tone: "error" });
      return;
    }
    const nextLat = Number(point.lat.toFixed(6));
    const nextLng = Number(point.lng.toFixed(6));
    onChange({
      lat: nextLat,
      lng: nextLng,
      label: describe(point.address),
      address: point.address?.trim() ?? ""
    });
    setMessage({ text: t("donorX.location.selected"), tone: "success" });
  }

  function useCurrentLocation() {
    setMessage(null);
    if (typeof window === "undefined" || !("geolocation" in navigator) || window.isSecureContext === false) {
      setMessage({ text: t("donorX.location.unsupported"), tone: "error" });
      return;
    }

    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      (position) => {
        setLocating(false);
        const nextLat = Number(position.coords.latitude.toFixed(6));
        const nextLng = Number(position.coords.longitude.toFixed(6));
        if (!hasValidPoint(nextLat, nextLng)) {
          setMessage({ text: t("donorX.location.invalid"), tone: "error" });
          return;
        }
        onChange({ lat: nextLat, lng: nextLng, label: describe(), address: "" });
        setMessage({ text: `${t("donorX.location.selected")} · ${describe()}`, tone: "success" });
      },
      (error) => {
        setLocating(false);
        setMessage({
          text: t(
            error.code === error.PERMISSION_DENIED
              ? "donorX.location.denied"
              : error.code === error.POSITION_UNAVAILABLE || error.code === error.TIMEOUT
                ? "donorX.location.unavailable"
                : "donorX.location.unavailable"
          ),
          tone: "error"
        });
      },
      { enableHighAccuracy: true, timeout: 10_000, maximumAge: 60_000 }
    );
  }

  const value: MapPoint | undefined = valid
    ? { lat: lat as number, lng: lng as number, address }
    : undefined;

  return (
    <div className="donor-map">
      <div className="donor-map__toolbar">
        <button
          className="btn btn-outline"
          type="button"
          onClick={useCurrentLocation}
          disabled={locating}
          aria-busy={locating}
        >
          <Icon name="pin" size={16} />
          {locating ? t("donorX.location.locating") : t("donorX.location.current")}
        </button>
        {valid ? (
          <button className="btn btn-outline" type="button" onClick={() => commit(null)}>
            <Icon name="close" size={15} /> {t("donorX.location.clear")}
          </button>
        ) : null}
      </div>

      {message ? <PortalNotice tone={message.tone}>{message.text}</PortalNotice> : null}

      <div className="donor-map__canvas">
        <MapPicker height={280} value={value} onChange={commit} allowCoordinateEntry={false} />
      </div>

      <div className="donor-map__summary" aria-live="polite">
        <Icon name="pin" size={16} />
        <span>{valid ? label || t("donorX.location.saved") : t("donorX.shared.locationNotSpecified")}</span>
        {valid ? (
          <a href={mapLink(lat as number, lng as number)} target="_blank" rel="noreferrer">
            {t("donorX.shared.openMap")} <Icon name="arrow" size={14} />
          </a>
        ) : null}
      </div>
      <p className="donor-map__hint">
        <Icon name="info" size={14} /> {t("donorX.location.hint")}
      </p>
    </div>
  );
}
