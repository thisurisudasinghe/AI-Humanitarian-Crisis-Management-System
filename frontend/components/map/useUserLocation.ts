"use client";

// Mapping-only browser geolocation hook. Coordinates stay in React state: they
// are never persisted, transmitted, or shared with other SAFEWAY users.

import { useCallback, useEffect, useRef, useState } from "react";

export type LocationStatus =
  | "idle"
  | "detecting"
  | "detected"
  | "denied"
  | "unavailable"
  | "timeout"
  | "unsupported"
  | "insecure"
  | "paused"
  | "disabled";

export interface TrackedLocation {
  lat: number;
  lng: number;
  accuracy?: number;
  timestamp: number;
}

const POSITION_OPTIONS: PositionOptions = {
  enableHighAccuracy: true,
  timeout: 15000,
  maximumAge: 30000
};

function describeError(error: GeolocationPositionError): {
  status: LocationStatus;
  message: string;
} {
  switch (error.code) {
    case error.PERMISSION_DENIED:
      return {
        status: "denied",
        message:
          "Location permission was denied. Enter a location manually below, or change this site's permission and retry."
      };
    case error.POSITION_UNAVAILABLE:
      return {
        status: "unavailable",
        message:
          "Your location is currently unavailable. Check device location services, then retry or enter coordinates manually."
      };
    case error.TIMEOUT:
      return {
        status: "timeout",
        message:
          "Location detection timed out. Retry, move to a clearer signal area, or enter coordinates manually."
      };
    default:
      return {
        status: "unavailable",
        message: "Your browser could not determine a location. Manual coordinates remain available."
      };
  }
}

function toTrackedLocation(position: GeolocationPosition): TrackedLocation {
  return {
    lat: position.coords.latitude,
    lng: position.coords.longitude,
    accuracy: Number.isFinite(position.coords.accuracy)
      ? position.coords.accuracy
      : undefined,
    timestamp: position.timestamp
  };
}

export function useUserLocation(active: boolean) {
  const [status, setStatus] = useState<LocationStatus>("idle");
  const [location, setLocation] = useState<TrackedLocation | null>(null);
  const [message, setMessage] = useState(
    "Location permission is used only to show your position on this map."
  );
  const [visible, setVisible] = useState(true);

  const mountedRef = useRef(true);
  const watchIdRef = useRef<number | null>(null);
  const requestSeqRef = useRef(0);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  useEffect(() => {
    const update = () => setVisible(document.visibilityState === "visible");
    update();
    document.addEventListener("visibilitychange", update);
    return () => document.removeEventListener("visibilitychange", update);
  }, []);

  const stopWatching = useCallback(() => {
    if (watchIdRef.current !== null && typeof navigator !== "undefined" && navigator.geolocation) {
      navigator.geolocation.clearWatch(watchIdRef.current);
    }
    watchIdRef.current = null;
  }, []);

  const startWatching = useCallback(() => {
    if (watchIdRef.current !== null || typeof navigator === "undefined" || !navigator.geolocation) {
      return;
    }
    watchIdRef.current = navigator.geolocation.watchPosition(
      (position) => {
        if (!mountedRef.current) return;
        setLocation(toTrackedLocation(position));
        setStatus("detected");
        setMessage("Live location updates are active while this page is open.");
      },
      (error) => {
        if (!mountedRef.current) return;
        const mapped = describeError(error);
        setStatus(mapped.status);
        setMessage(mapped.message);
        stopWatching();
      },
      POSITION_OPTIONS
    );
  }, [stopWatching]);

  const begin = useCallback(() => {
    stopWatching();
    if (!mountedRef.current) return;

    if (!active) {
      setStatus("disabled");
      setMessage("Location tracking is off. Manual coordinates remain available.");
      return;
    }

    if (!visible) {
      setStatus("paused");
      setMessage("Live tracking is paused while this tab is hidden.");
      return;
    }

    if (typeof window === "undefined") return;

    if (!window.isSecureContext) {
      setStatus("insecure");
      setMessage(
        "Browser geolocation requires HTTPS (or localhost). Use manual coordinates, or open SAFEWAY over HTTPS."
      );
      return;
    }

    if (!navigator.geolocation) {
      setStatus("unsupported");
      setMessage("This browser does not support geolocation. Use manual coordinates instead.");
      return;
    }

    const seq = ++requestSeqRef.current;
    setStatus("detecting");
    setMessage("Requesting browser permission to show your current location on this map…");

    navigator.geolocation.getCurrentPosition(
      (position) => {
        if (!mountedRef.current || seq !== requestSeqRef.current) return;
        setLocation(toTrackedLocation(position));
        setStatus("detected");
        setMessage("Location detected. The map will follow live updates while this page is open.");
        startWatching();
      },
      (error) => {
        if (!mountedRef.current || seq !== requestSeqRef.current) return;
        const mapped = describeError(error);
        setStatus(mapped.status);
        setMessage(mapped.message);
        stopWatching();
      },
      POSITION_OPTIONS
    );
  }, [active, visible, startWatching, stopWatching]);

  useEffect(() => {
    begin();
    return () => {
      requestSeqRef.current += 1;
      stopWatching();
    };
  }, [begin, stopWatching]);

  return {
    status,
    location,
    message,
    retry: begin,
    stopWatching
  };
}
