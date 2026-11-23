"use client";

// Mapping-only browser geolocation hook. Coordinates stay in React state: they
// are never persisted, transmitted, or shared with other SAFEWAY users.

import { useCallback, useEffect, useRef, useState } from "react";

type Translator = (key: string, vars?: Record<string, string | number>) => string;

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

function describeError(error: GeolocationPositionError, t: Translator): {
  status: LocationStatus;
  message: string;
} {
  switch (error.code) {
    case error.PERMISSION_DENIED:
      return {
        status: "denied",
        message: t("operations.map.geoPermissionDenied")
      };
    case error.POSITION_UNAVAILABLE:
      return {
        status: "unavailable",
        message: t("operations.map.geoUnavailable")
      };
    case error.TIMEOUT:
      return {
        status: "timeout",
        message: t("operations.map.geoTimeout")
      };
    default:
      return {
        status: "unavailable",
        message: t("operations.map.geoUnknown")
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

export function useUserLocation(active: boolean, t: Translator) {
  const [status, setStatus] = useState<LocationStatus>("idle");
  const [location, setLocation] = useState<TrackedLocation | null>(null);
  const [message, setMessage] = useState(t("operations.map.geoIntro"));
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

  useEffect(() => {
    if (status === "idle") setMessage(t("operations.map.geoIntro"));
  }, [status, t]);

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
        setMessage(t("operations.map.geoLiveActive"));
      },
      (error) => {
        if (!mountedRef.current) return;
        const mapped = describeError(error, t);
        setStatus(mapped.status);
        setMessage(mapped.message);
        stopWatching();
      },
      POSITION_OPTIONS
    );
  }, [stopWatching, t]);

  const begin = useCallback(() => {
    stopWatching();
    if (!mountedRef.current) return;

    if (!active) {
      setStatus("disabled");
      setMessage(t("operations.map.geoDisabled"));
      return;
    }

    if (!visible) {
      setStatus("paused");
      setMessage(t("operations.map.geoPaused"));
      return;
    }

    if (typeof window === "undefined") return;

    if (!window.isSecureContext) {
      setStatus("insecure");
      setMessage(t("operations.map.geoInsecure"));
      return;
    }

    if (!navigator.geolocation) {
      setStatus("unsupported");
      setMessage(t("operations.map.geoUnsupported"));
      return;
    }

    const seq = ++requestSeqRef.current;
    setStatus("detecting");
    setMessage(t("operations.map.geoRequesting"));

    navigator.geolocation.getCurrentPosition(
      (position) => {
        if (!mountedRef.current || seq !== requestSeqRef.current) return;
        setLocation(toTrackedLocation(position));
        setStatus("detected");
        setMessage(t("operations.map.geoDetected"));
        startWatching();
      },
      (error) => {
        if (!mountedRef.current || seq !== requestSeqRef.current) return;
        const mapped = describeError(error, t);
        setStatus(mapped.status);
        setMessage(mapped.message);
        stopWatching();
      },
      POSITION_OPTIONS
    );
  }, [active, visible, startWatching, stopWatching, t]);

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
