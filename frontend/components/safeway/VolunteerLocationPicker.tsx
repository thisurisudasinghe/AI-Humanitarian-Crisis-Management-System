"use client";

import dynamic from "next/dynamic";
import { useState } from "react";
import type { MapPoint } from "@/components/map/MapPicker";
import { useT } from "@/lib/i18n";
import { hasValidPoint, mapLink } from "@/lib/volunteer";
import { Icon } from "./Icon";
import { PortalNotice } from "./PortalPrimitives";

const MapPicker = dynamic(() => import("@/components/map/MapPicker"), { ssr: false });

export function VolunteerLocationPicker({
  lat,
  lng,
  onChange
}: {
  lat: number | null;
  lng: number | null;
  onChange: (point: MapPoint | null) => void;
}) {
  const t = useT();
  const [address, setAddress] = useState("");
  const [locating, setLocating] = useState(false);
  const [message, setMessage] = useState<{ text: string; tone: "success" | "error" } | null>(null);
  const valid = hasValidPoint(lat, lng);
  const value = valid ? { lat: lat as number, lng: lng as number, address } : undefined;

  function commit(point: MapPoint | null) {
    onChange(point);
    setAddress(point?.address?.trim() ?? "");
    setMessage(
      point
        ? { text: point.address?.trim() ? point.address.trim() : t("volunteerX.profile.locationSelected"), tone: "success" }
        : { text: t("volunteerX.profile.locationCleared"), tone: "success" }
    );
  }

  function useCurrentLocation() {
    setMessage(null);
    if (typeof window === "undefined" || !("geolocation" in navigator)) {
      setMessage({ text: t("volunteerX.profile.locationUnsupported"), tone: "error" });
      return;
    }
    if (window.isSecureContext === false) {
      setMessage({ text: t("volunteerX.profile.locationUnsupported"), tone: "error" });
      return;
    }

    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      (position) => {
        const nextLat = Number(position.coords.latitude.toFixed(6));
        const nextLng = Number(position.coords.longitude.toFixed(6));
        setLocating(false);
        if (!hasValidPoint(nextLat, nextLng)) {
          setMessage({ text: t("volunteerX.profile.locationInvalid"), tone: "error" });
          return;
        }
        commit({ lat: nextLat, lng: nextLng });
        setMessage({ text: t("volunteerX.profile.locationSuccess"), tone: "success" });
      },
      (error) => {
        setLocating(false);
        const denied = error.code === error.PERMISSION_DENIED;
        setMessage({
          text: t(
            denied
              ? "volunteerX.profile.locationDenied"
              : "volunteerX.profile.locationUnavailable"
          ),
          tone: "error"
        });
      },
      { enableHighAccuracy: true, timeout: 10_000, maximumAge: 60_000 }
    );
  }

  return (
    <div className="volunteer-map">
      <div className="volunteer-map__toolbar">
        <button
          className="btn btn-outline"
          type="button"
          onClick={useCurrentLocation}
          disabled={locating}
          aria-busy={locating}
        >
          <Icon name="pin" size={16} />
          {locating ? t("volunteerX.profile.locating") : t("volunteerX.profile.currentLocation")}
        </button>
        {valid ? (
          <button className="btn btn-outline" type="button" onClick={() => commit(null)}>
            <Icon name="close" size={15} /> {t("common.clear")}
          </button>
        ) : null}
      </div>

      {message ? <PortalNotice tone={message.tone}>{message.text}</PortalNotice> : null}

      <div className="dev4-operations">
        <MapPicker
          height={280}
          value={value}
          onChange={(point) => commit(point)}
        />
      </div>

      <div className="volunteer-map__summary" aria-live="polite">
        <Icon name="pin" size={16} />
        <span>
          {valid
            ? address || t("volunteerX.shared.locationOnMap")
            : t("volunteerX.profile.locationCleared")}
        </span>
        {valid ? (
          <a href={mapLink(lat as number, lng as number)} target="_blank" rel="noreferrer">
            {t("volunteerX.shared.openMap")} <Icon name="arrow" size={14} />
          </a>
        ) : null}
      </div>
      <p className="volunteer-map__hint">{t("volunteerX.profile.mapHint")}</p>
    </div>
  );
}
