"use client";

import { AuthGuard } from "@/components/AuthGuard";
import { useAuth } from "@/lib/auth";
import { formatDate as formatLocaleDate, formatNumber, useI18n, useT, type Locale } from "@/lib/i18n";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { RoleNames } from "@/lib/roles";

// Member 4 / D4-11 operations map. Data comes from existing SAFEWAY APIs only.
// Each source reports its own loading/error state so a 403 on one layer never
// silently hides every other layer.

import dynamic from "next/dynamic";
import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from "react";
import { api as coreApi } from "@/lib/api";
import { api, type Paged } from "@/lib/operations-api";
import type { MapFocus, MapMarker } from "@/components/map/MapView";
import type { MapPoint } from "@/components/map/MapPicker";
import { MAP_MARKER_TYPES, markerLabelKey } from "@/components/map/mapTypes";
import { useUserLocation, type LocationStatus } from "@/components/map/useUserLocation";
import styles from "./page.module.css";

const MapView = dynamic(() => import("@/components/map/MapView"), { ssr: false });
const MapPicker = dynamic(() => import("@/components/map/MapPicker"), { ssr: false });

type Translator = (key: string, vars?: Record<string, string | number>) => string;

interface ShelterRow {
  id: string;
  name: string;
  capacity: number;
  occupancy: number;
  freePlaces: number;
  facilities: string;
  accessibilityFeatures: string;
  services: string;
  lat: number;
  lng: number;
}

interface ServiceCentreRow {
  id: string;
  name: string;
  address: string;
  lat: number | string;
  lng: number | string;
  services?: string;
  openingHours?: string | null;
}

interface AreaRow {
  id: string;
  crisisId: string;
  name: string;
  lat: number | string;
  lng: number | string;
  radiusKm?: number | string | null;
}

interface ResourceTypeRow {
  id: string;
  name: string;
  unit: string;
}

interface InventoryRow {
  id: string;
  resourceTypeId: string;
  quantity: number | string;
  available: number | string;
  expiryDate?: string | null;
  accessibilityCapable?: boolean;
  lat?: number | string | null;
  lng?: number | string | null;
}

interface VolunteerRow {
  id: string;
  skillsJson: string;
  languagesJson: string;
  verificationStatus: string;
  active: boolean;
  lat?: number | string | null;
  lng?: number | string | null;
}

interface VolunteerTaskRow {
  id: string;
  title: string;
  requiredSkillsJson: string;
  lat?: number | string | null;
  lng?: number | string | null;
  scheduledAt?: string | null;
  status: string;
}

interface CrisisRow {
  id: string;
  name: string;
}

const SOURCE_LABEL_KEYS: Record<string, string> = {
  shelters: "operations.map.sourceShelters",
  centres: "operations.map.sourceCentres",
  areas: "operations.map.sourceAreas",
  resources: "operations.map.sourceResources",
  volunteers: "operations.map.sourceVolunteers",
  tasks: "operations.map.sourceTasks"
};

const LOCATION_LABEL_KEYS: Record<LocationStatus, string> = {
  idle: "operations.map.statusIdle",
  detecting: "operations.map.statusDetecting",
  detected: "operations.map.statusDetected",
  denied: "operations.map.statusDenied",
  unavailable: "operations.map.statusUnavailable",
  timeout: "operations.map.statusTimeout",
  unsupported: "operations.map.statusUnsupported",
  insecure: "operations.map.statusInsecure",
  paused: "operations.map.statusPaused",
  disabled: "operations.map.statusDisabled"
};

function sourceLabel(key: string, t: Translator): string {
  return t(SOURCE_LABEL_KEYS[key] ?? key);
}

function locationStatusLabel(status: LocationStatus, t: Translator): string {
  return t(LOCATION_LABEL_KEYS[status]);
}

function finiteNumber(value: unknown): number | null {
  if (value === null || value === undefined) return null;
  if (typeof value === "string" && !value.trim()) return null;
  const parsed = typeof value === "number" ? value : Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function validPoint(lat: number | null, lng: number | null): boolean {
  return (
    lat !== null && lng !== null &&
    lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180
  );
}

function buildMarker(
  id: string,
  lat: number | null,
  lng: number | null,
  label: string,
  type: string,
  description: string
): MapMarker | null {
  if (!validPoint(lat, lng)) return null;
  return { id, lat: lat!, lng: lng!, label, type, description };
}

function parseStringList(raw?: string | null): string[] {
  if (!raw) return [];
  try {
    const parsed: unknown = JSON.parse(raw);
    if (Array.isArray(parsed)) {
      return parsed.filter((item): item is string => typeof item === "string" && item.trim().length > 0);
    }
  } catch {
    return raw.split(",").map((item) => item.trim()).filter(Boolean);
  }
  return [];
}

function formatQuantity(value: unknown, locale: Locale): string {
  const parsed = finiteNumber(value);
  return parsed === null
    ? String(value ?? "0")
    : formatNumber(parsed, locale, { maximumFractionDigits: 2 });
}

function formatMapDate(value: string | null | undefined, locale: Locale, t: Translator): string {
  if (!value) return t("operations.map.notScheduled");
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : formatLocaleDate(date, locale);
}

function statusClass(status: LocationStatus): string {
  if (status === "detected") return `${styles.pill} ${styles.pillGood}`;
  if (["detecting", "paused"].includes(status)) return `${styles.pill} ${styles.pillWarn}`;
  if (["denied", "unavailable", "timeout", "unsupported", "insecure"].includes(status)) {
    return `${styles.pill} ${styles.pillBad}`;
  }
  return styles.pill;
}

async function fetchAllPaged<T>(pathForPage: (page: number) => string): Promise<T[]> {
  const first = await api<Paged<T>>(pathForPage(1));
  const items = [...first.items];
  const size = first.pageSize > 0 ? first.pageSize : 50;
  const totalPages = Math.min(5, Math.max(1, Math.ceil(first.total / size)));
  for (let page = 2; page <= totalPages; page += 1) {
    const next = await api<Paged<T>>(pathForPage(page));
    items.push(...next.items);
  }
  return items;
}

function OperationsMapContent() {
  const t = useT();
  const { locale } = useI18n();
  const { user, can } = useAuth();
  const canSeeVolunteers = can([
    RoleNames.VolunteerCoordinator,
    RoleNames.EmergencyCoordinator,
    RoleNames.SystemAdmin
  ]);
  const canCreateArea = can([RoleNames.EmergencyCoordinator]);

  const [trackingEnabled, setTrackingEnabled] = useState(true);
  const {
    status: locationStatus,
    location,
    message: locationMessage,
    retry: retryLocation
  } = useUserLocation(Boolean(user) && trackingEnabled, t);

  const [markers, setMarkers] = useState<MapMarker[]>([]);
  const [loading, setLoading] = useState(true);
  const [sourceErrors, setSourceErrors] = useState<Record<string, string>>({});
  const [reloadNonce, setReloadNonce] = useState(0);

  const [searchQuery, setSearchQuery] = useState("");
  const [searching, setSearching] = useState(false);
  const [searchError, setSearchError] = useState("");
  const [searchResult, setSearchResult] = useState<MapMarker | null>(null);
  const [typeFilter, setTypeFilter] = useState("all");
  const [textFilter, setTextFilter] = useState("");
  const [selectedMarker, setSelectedMarker] = useState<MapMarker | null>(null);
  const [focus, setFocus] = useState<MapFocus | null>(null);
  const [fitNonce, setFitNonce] = useState(0);
  const [actionNotice, setActionNotice] = useState("");
  const [routeDistance, setRouteDistance] = useState<{
    km: number;
    minutes?: number | null;
    isApproximate: boolean;
  } | null>(null);
  const [distanceLoading, setDistanceLoading] = useState(false);
  const [distanceError, setDistanceError] = useState("");

  const [picked, setPicked] = useState<MapPoint | null>(null);
  const [showCreateArea, setShowCreateArea] = useState(false);
  const [crises, setCrises] = useState<CrisisRow[]>([]);
  const [crisisId, setCrisisId] = useState("");
  const [areaName, setAreaName] = useState("");
  const [areaDescription, setAreaDescription] = useState("");
  const [areaRadius, setAreaRadius] = useState("");
  const [savingArea, setSavingArea] = useState(false);
  const [areaNotice, setAreaNotice] = useState("");
  const [areaNoticeTone, setAreaNoticeTone] = useState<"success" | "error">("error");

  function setAreaMessage(message: string, tone: "success" | "error" = "error") {
    setAreaNotice(message);
    setAreaNoticeTone(tone);
  }

  const centeredOnUserRef = useRef(false);

  useEffect(() => {
    let active = true;

    async function load() {
      setLoading(true);
      const tasks: Array<{ key: string; run: () => Promise<MapMarker[]> }> = [
        {
          key: "shelters",
          run: async () => {
            const rows = await api<ShelterRow[]>("/shelters/public");
            return rows
              .map((row) =>
                buildMarker(
                  row.id,
                  finiteNumber(row.lat),
                  finiteNumber(row.lng),
                  row.name,
                  "shelter",
                  [
                    t("operations.map.shelterApproved", {
                      free: formatQuantity(row.freePlaces, locale),
                      capacity: formatQuantity(row.capacity, locale)
                    }),
                    t("operations.map.shelterOccupied", {
                      count: formatQuantity(row.occupancy, locale)
                    }),
                    t("operations.map.fieldFacilities", {
                      value: parseStringList(row.facilities).join(", ") || t("operations.map.notListed")
                    }),
                    t("operations.map.fieldAccessibility", {
                      value: parseStringList(row.accessibilityFeatures).join(", ") || t("operations.map.notListed")
                    }),
                    t("operations.map.fieldServices", {
                      value: parseStringList(row.services).join(", ") || t("operations.map.notListed")
                    })
                  ].join(" • ")
                )
              )
              .filter((marker): marker is MapMarker => marker !== null);
          }
        },
        {
          key: "centres",
          run: async () => {
            const rows = await api<ServiceCentreRow[]>("/public/service-centres");
            return rows
              .map((row) =>
                buildMarker(
                  row.id,
                  finiteNumber(row.lat),
                  finiteNumber(row.lng),
                  row.name,
                  "service-centre",
                  [
                    row.address || t("operations.map.serviceCentre"),
                    row.services ? t("operations.map.fieldServices", { value: row.services }) : "",
                    t("operations.map.hours", {
                      value: row.openingHours || t("operations.map.notListed")
                    })
                  ].filter(Boolean).join(" • ")
                )
              )
              .filter((marker): marker is MapMarker => marker !== null);
          }
        },
        {
          key: "areas",
          run: async () => {
            const rows = await api<AreaRow[]>("/operations/areas");
            return rows
              .map((row) =>
                buildMarker(
                  row.id,
                  finiteNumber(row.lat),
                  finiteNumber(row.lng),
                  row.name,
                  "area",
                  t("operations.map.areaDetail", {
                    radius: row.radiusKm
                      ? t("operations.map.areaRadiusPart", { radius: formatQuantity(row.radiusKm, locale) })
                      : "",
                    crisis: row.crisisId
                  })
                )
              )
              .filter((marker): marker is MapMarker => marker !== null);
          }
        },
        {
          key: "resources",
          run: async () => {
            let types: ResourceTypeRow[] = [];
            try {
              types = await api<ResourceTypeRow[]>("/resources/types");
            } catch {
              // Resource markers remain useful with a generic label if the type list is unavailable.
            }
            const names = new Map(types.map((type) => [type.id, type]));
            const rows = await fetchAllPaged<InventoryRow>((page) => `/inventories?page=${page}&pageSize=200`);
            return rows
              .map((row) => {
                const type = names.get(row.resourceTypeId);
                const unit = type?.unit || t("operations.map.units");
                return buildMarker(
                  row.id,
                  finiteNumber(row.lat),
                  finiteNumber(row.lng),
                  type?.name || t("operations.map.sourceResources"),
                  "resource",
                  [
                    t("operations.map.stockAvailable", {
                      available: formatQuantity(row.available, locale),
                      quantity: formatQuantity(row.quantity, locale),
                      unit
                    }),
                    row.accessibilityCapable ? t("operations.map.accessibilityCapable") : "",
                    row.expiryDate
                      ? t("operations.map.expires", { date: formatMapDate(row.expiryDate, locale, t) })
                      : ""
                  ].filter(Boolean).join(" • ")
                );
              })
              .filter((marker): marker is MapMarker => marker !== null);
          }
        }
      ];

      if (canSeeVolunteers) {
        tasks.push({
          key: "volunteers",
          run: async () => {
            const rows = await api<VolunteerRow[]>("/volunteers");
            return rows
              .filter((row) => row.active && row.verificationStatus?.toLowerCase() === "verified")
              .map((row) =>
                buildMarker(
                  row.id,
                  finiteNumber(row.lat),
                  finiteNumber(row.lng),
                  t("operations.map.volunteer"),
                  "volunteer",
                  [
                    t("operations.map.staffOnly"),
                    t("operations.map.fieldSkills", {
                      value: parseStringList(row.skillsJson).join(", ") || t("operations.map.notListed")
                    }),
                    t("operations.map.fieldLanguages", {
                      value: parseStringList(row.languagesJson).join(", ") || t("operations.map.notListed")
                    })
                  ].join(" • ")
                )
              )
              .filter((marker): marker is MapMarker => marker !== null);
          }
        });
        tasks.push({
          key: "tasks",
          run: async () => {
            const rows = await fetchAllPaged<VolunteerTaskRow>(
              (page) => `/volunteer-tasks?page=${page}&pageSize=200`
            );
            return rows
              .map((row) =>
                buildMarker(
                  row.id,
                  finiteNumber(row.lat),
                  finiteNumber(row.lng),
                  row.title,
                  "volunteer-task",
                  [
                    t("operations.map.taskStatus", { status: row.status }),
                    t("operations.map.fieldSkills", {
                      value: parseStringList(row.requiredSkillsJson).join(", ") || t("operations.map.notListed")
                    }),
                    t("operations.map.scheduled", { date: formatMapDate(row.scheduledAt, locale, t) })
                  ].join(" • ")
                )
              )
              .filter((marker): marker is MapMarker => marker !== null);
          }
        });
      }

      const results = await Promise.allSettled(tasks.map((task) => task.run()));
      if (!active) return;

      const nextMarkers: MapMarker[] = [];
      const errors: Record<string, string> = {};
      results.forEach((result, index) => {
        if (result.status === "fulfilled") {
          nextMarkers.push(...result.value);
        } else {
          errors[tasks[index].key] = localizedErrorMessage(result.reason, t);
        }
      });

      setMarkers(nextMarkers);
      setSourceErrors(errors);
      setLoading(false);
    }

    void load();
    return () => {
      active = false;
    };
  }, [canSeeVolunteers, reloadNonce, locale, t]);

  useEffect(() => {
    if (!centeredOnUserRef.current && location) {
      setFocus({ lat: location.lat, lng: location.lng, zoom: 14, nonce: Date.now() });
      centeredOnUserRef.current = true;
    }
  }, [location]);

  useEffect(() => {
    if (!trackingEnabled || !user) centeredOnUserRef.current = false;
  }, [trackingEnabled, user]);

  const allMarkers = useMemo(() => {
    const list = [...markers];
    if (location && validPoint(location.lat, location.lng)) {
      list.push({
        id: "current-location",
        lat: location.lat,
        lng: location.lng,
        type: "current-location",
        label: t("map.markerTypes.currentLocation"),
        description: location.accuracy != null
          ? `${t("operations.map.gpsAccuracyAbout", { accuracy: Math.round(location.accuracy) })} • ${t("operations.map.browserOnlyArea")}`
          : t("operations.map.browserOnlyArea")
      });
    }
    if (searchResult) list.push(searchResult);
    return list;
  }, [markers, location, searchResult, t]);

  const filteredMarkers = useMemo(() => {
    const query = textFilter.trim().toLowerCase();
    return allMarkers.filter((marker) => {
      const matchesType = typeFilter === "all" || marker.type === typeFilter;
      const haystack = [
        marker.label ?? marker.name ?? "",
        marker.description ?? "",
        t(markerLabelKey(marker.type))
      ].join(" ").toLowerCase();
      return matchesType && (!query || haystack.includes(query));
    });
  }, [allMarkers, textFilter, typeFilter, t]);

  const selectMarker = useCallback((marker: MapMarker) => {
    setSelectedMarker(marker);
    setFocus({ lat: marker.lat, lng: marker.lng, zoom: 14, nonce: Date.now() });
  }, []);

  const centreOnMyLocation = () => {
    if (!location) {
      setActionNotice(t("operations.map.noLocationDetectedPicker"));
      return;
    }
    setActionNotice(t("operations.map.centredOnLocation"));
    setFocus({ lat: location.lat, lng: location.lng, zoom: 15, nonce: Date.now() });
  };

  const useDetectedLocationForPicking = () => {
    if (!location) {
      setAreaMessage(t("operations.map.noLocationDetectedManual"));
      return;
    }
    setPicked({
      lat: location.lat,
      lng: location.lng,
      address: location.accuracy != null
        ? t("operations.map.detectedLocationAccuracy", { accuracy: Math.round(location.accuracy) })
        : t("operations.map.detectedLocation")
    });
    setAreaMessage(t("operations.map.detectedCopied"), "success");
  };

  useEffect(() => {
    setRouteDistance(null);
    setDistanceError("");
    if (!location || !selectedMarker || selectedMarker.type === "current-location") return;

    let active = true;
    setDistanceLoading(true);
    api<{ km: number; minutes?: number | null; isApproximate: boolean }>(
      `/geo/distance?fromLat=${encodeURIComponent(String(location.lat))}` +
      `&fromLng=${encodeURIComponent(String(location.lng))}` +
      `&toLat=${encodeURIComponent(String(selectedMarker.lat))}` +
      `&toLng=${encodeURIComponent(String(selectedMarker.lng))}`
    )
      .then((result) => {
        if (active) setRouteDistance(result);
      })
      .catch((error) => {
        if (active) setDistanceError(localizedErrorMessage(error, t));
      })
      .finally(() => {
        if (active) setDistanceLoading(false);
      });

    return () => {
      active = false;
    };
  }, [location?.lat, location?.lng, selectedMarker?.id, selectedMarker?.lat, selectedMarker?.lng, selectedMarker?.type, t]);

  async function handleSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const query = searchQuery.trim();
    if (!query) {
      setSearchError(t("operations.map.enterSearch"));
      return;
    }

    setSearching(true);
    setSearchError("");
    setActionNotice("");
    try {
      const found = await api<{ lat: number | string; lng: number | string } | null>(
        `/geo/geocode?address=${encodeURIComponent(query)}`
      );
      const lat = finiteNumber(found?.lat);
      const lng = finiteNumber(found?.lng);
      if (!found || !validPoint(lat, lng)) {
        setSearchResult(null);
        setSearchError(t("operations.map.geocodeNotFoundMap"));
        return;
      }

      const result: MapMarker = {
        id: `search-${Date.now()}`,
        lat: lat!,
        lng: lng!,
        type: "search-result",
        label: query,
        description: t("operations.map.geocodedResult")
      };
      setSearchResult(result);
      selectMarker(result);
    } catch (error) {
      setSearchResult(null);
      setSearchError(t("operations.map.manualStillAvailable", {
        error: localizedErrorMessage(error, t)
      }));
    } finally {
      setSearching(false);
    }
  }

  async function openCreateArea() {
    setAreaMessage("");
    setShowCreateArea(true);
    if (crises.length > 0) return;
    try {
      setCrises(await coreApi<CrisisRow[]>("/api/v1/crises"));
    } catch (error) {
      setAreaMessage(localizedErrorMessage(error, t));
    }
  }

  async function submitArea(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setAreaMessage("");

    const name = areaName.trim();
    if (!crisisId) {
      setAreaMessage(t("operations.map.selectCrisis"));
      return;
    }
    if (!name) {
      setAreaMessage(t("operations.map.enterAreaName"));
      return;
    }
    if (name.length > 200 || areaDescription.trim().length > 2000) {
      setAreaMessage(t("operations.map.areaLengths"));
      return;
    }
    if (!picked || !validPoint(finiteNumber(picked.lat), finiteNumber(picked.lng))) {
      setAreaMessage(t("operations.map.selectValidLocation"));
      return;
    }
    const radius = areaRadius.trim() ? Number(areaRadius) : null;
    if (radius !== null && (!Number.isFinite(radius) || radius < 0 || radius > 1000)) {
      setAreaMessage(t("operations.map.radiusRange"));
      return;
    }

    setSavingArea(true);
    try {
      await coreApi<{ id: string }>(`/api/v1/crises/${crisisId}/areas`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name,
          description: areaDescription.trim() || null,
          lat: picked.lat,
          lng: picked.lng,
          radiusKm: radius
        })
      });
      setAreaMessage(t("operations.map.areaSaved", { name }), "success");
      setAreaName("");
      setAreaDescription("");
      setAreaRadius("");
      setReloadNonce((value) => value + 1);
    } catch (error) {
      setAreaMessage(localizedErrorMessage(error, t));
    } finally {
      setSavingArea(false);
    }
  }

  const sourceErrorEntries = Object.entries(sourceErrors).filter(([, message]) => Boolean(message));

  return (
    <>
      <div className={styles.headerRow}>
        <h1>{t("operations.map.title")}</h1>
        <span className="muted" role="status">
          {loading
            ? t("operations.map.loadingLayers")
            : t("operations.map.markersShown", {
                shown: filteredMarkers.length,
                total: allMarkers.length
              })}
        </span>
      </div>
      <p className="muted">{t("operations.map.detailSubtitle")}</p>

      <section className="panel" aria-labelledby="map-location-heading">
        <h2 id="map-location-heading">{t("operations.map.currentLocation")}</h2>
        <p className="muted">{t("operations.map.locationPrivacy")}</p>
        <div className={styles.statusRow}>
          <span className={statusClass(locationStatus)}>{locationStatusLabel(locationStatus, t)}</span>
          {location && (
            <span className="muted small">
              {location.lat.toFixed(5)}, {location.lng.toFixed(5)}
              {location.accuracy != null
                ? t("operations.map.accuracyApprox", { accuracy: Math.round(location.accuracy) })
                : t("operations.map.accuracyUnavailable")}
            </span>
          )}
        </div>
        <p className={["denied", "unavailable", "timeout", "unsupported", "insecure"].includes(locationStatus) ? "error" : "muted"} role={["denied", "unavailable", "timeout", "unsupported", "insecure"].includes(locationStatus) ? "alert" : "status"}>
          {locationMessage}
        </p>
        <div className={styles.actionRow}>
          <button type="button" onClick={() => setTrackingEnabled((value) => !value)}>
            {trackingEnabled ? t("operations.map.stopTracking") : t("operations.map.startTracking")}
          </button>
          <button type="button" onClick={retryLocation} disabled={!trackingEnabled}>
            {t("operations.map.retryDetection")}
          </button>
          <button type="button" onClick={centreOnMyLocation} disabled={!location}>
            {t("map.myLocation")}
          </button>
        </div>
        {actionNotice && <p className="muted small" role="status">{actionNotice}</p>}
      </section>

      <section className="panel" aria-labelledby="map-filters-heading">
        <h2 id="map-filters-heading">{t("operations.map.searchAndFilter")}</h2>
        <form className="filters" onSubmit={(event) => void handleSearch(event)}>
          <label htmlFor="map-location-search">{t("operations.map.findPlace")}</label>
          <input
            id="map-location-search"
            type="search"
            value={searchQuery}
            disabled={searching}
            placeholder={t("map.addressPlaceholder")}
            onChange={(event) => setSearchQuery(event.target.value)}
          />
          <button type="submit" disabled={searching || !searchQuery.trim()}>
            {searching ? t("map.searching") : t("operations.map.searchMap")}
          </button>
          {searchResult && (
            <button type="button" onClick={() => setSearchResult(null)}>{t("operations.map.clearResult")}</button>
          )}
        </form>
        {searchError && <p className="error" role="alert">{searchError}</p>}
        <div className="filters">
          <label htmlFor="map-type-filter">{t("operations.map.layer")}</label>
          <select
            id="map-type-filter"
            value={typeFilter}
            onChange={(event) => setTypeFilter(event.target.value)}
          >
            <option value="all">{t("operations.map.allLayers")}</option>
            {Object.entries(MAP_MARKER_TYPES)
              .filter(([key]) => key !== "location")
              .map(([key]) => (
                <option key={key} value={key}>{t(markerLabelKey(key))}</option>
              ))}
          </select>
          <label htmlFor="map-text-filter">{t("operations.map.filterMarkers")}</label>
          <input
            id="map-text-filter"
            type="search"
            value={textFilter}
            placeholder={t("operations.map.nameTypeDetail")}
            onChange={(event) => setTextFilter(event.target.value)}
          />
        </div>
        {!canSeeVolunteers && (
          <p className="muted small">{t("operations.map.volunteerHidden")}</p>
        )}
      </section>

      {sourceErrorEntries.length > 0 && (
        <div className="error" role="alert">
          <strong>{t("operations.map.sourceErrorsTitle")}</strong>
          <ul className={styles.sourceList}>
            {sourceErrorEntries.map(([key, message]) => (
              <li key={key}>{sourceLabel(key, t)}: {message}</li>
            ))}
          </ul>
        </div>
      )}

      {loading && <p className="muted" role="status">{t("operations.map.loading")}</p>}

      <MapView
        markers={filteredMarkers}
        height={480}
        focus={focus}
        fitNonce={fitNonce}
        currentLocation={location}
        canLocate={Boolean(location)}
        selectedId={selectedMarker?.id ?? null}
        onMarkerSelect={selectMarker}
        onMyLocation={centreOnMyLocation}
        onFitAll={() => setFitNonce((value) => value + 1)}
        showLegend
      />

      {selectedMarker && (
        <section className={styles.detail} aria-live="polite">
          <p className={styles.detailTitle}>
            {selectedMarker.label ?? selectedMarker.name ?? t("map.location")} — {t(markerLabelKey(selectedMarker.type))}
          </p>
          <p className="muted">
            {selectedMarker.description || t("operations.map.noMarkerDetails")}
          </p>
          <p className="muted small">
            {t("operations.map.coordinates", {
              lat: selectedMarker.lat.toFixed(5),
              lng: selectedMarker.lng.toFixed(5)
            })}
          </p>
          {selectedMarker.type !== "current-location" && location && (
            <p className="muted small" role="status">
              {distanceLoading
                ? t("operations.map.calculatingDistance")
                : routeDistance
                  ? t("operations.map.distanceFrom", { km: routeDistance.km.toFixed(2) }) +
                    (routeDistance.minutes != null
                      ? ` (${t("operations.map.distanceMinutes", { minutes: routeDistance.minutes.toFixed(1) })})`
                      : "") +
                    (routeDistance.isApproximate
                      ? ` ${t("operations.map.distanceApproximate")}`
                      : ` ${t("operations.map.distanceRouted")}`)
                  : distanceError
                    ? t("operations.map.distanceUnavailableError", { error: distanceError })
                    : t("operations.map.distanceUnavailable")}
            </p>
          )}
        </section>
      )}

      <div className={styles.split}>
        <section className="panel" aria-labelledby="map-picker-heading">
          <h2 id="map-picker-heading">{t("operations.map.manualFallback")}</h2>
          <p className="muted small">{t("operations.map.manualHelp")}</p>
          <MapPicker
            height={300}
            value={picked ?? undefined}
            onChange={(point) => setPicked(point)}
          />
          {picked && (
            <p className="success" role="status">
              {t("operations.map.selected", {
                lat: picked.lat.toFixed(5),
                lng: picked.lng.toFixed(5)
              })}
              {picked.address ? t("operations.map.selectedAddress", { address: picked.address }) : ""}
            </p>
          )}
        </section>

        {canCreateArea && (
          <section className="panel" aria-labelledby="create-area-heading">
            <h2 id="create-area-heading">{t("operations.map.createArea")}</h2>
            <p className="muted small">{t("operations.map.createAreaHelp")}</p>
            {!showCreateArea ? (
              <button type="button" onClick={() => void openCreateArea()}>{t("operations.map.createAreaButton")}</button>
            ) : (
              <form className="form-grid" onSubmit={(event) => void submitArea(event)}>
                <label htmlFor="area-crisis">{t("operations.map.crisis")}</label>
                <select
                  id="area-crisis"
                  value={crisisId}
                  onChange={(event) => setCrisisId(event.target.value)}
                  required
                >
                  <option value="">{t("operations.map.selectCrisisPlaceholder")}</option>
                  {crises.map((crisis) => (
                    <option key={crisis.id} value={crisis.id}>{crisis.name}</option>
                  ))}
                </select>

                <label htmlFor="area-name">{t("operations.map.areaName")}</label>
                <input
                  id="area-name"
                  value={areaName}
                  maxLength={200}
                  onChange={(event) => setAreaName(event.target.value)}
                  required
                />

                <label htmlFor="area-description">{t("operations.map.description")}</label>
                <textarea
                  id="area-description"
                  rows={3}
                  value={areaDescription}
                  maxLength={2000}
                  onChange={(event) => setAreaDescription(event.target.value)}
                />

                <label htmlFor="area-radius">{t("operations.map.radiusOptional")}</label>
                <input
                  id="area-radius"
                  type="number"
                  min={0}
                  max={1000}
                  step="any"
                  value={areaRadius}
                  onChange={(event) => setAreaRadius(event.target.value)}
                />

                <div className="inline">
                  <button type="button" onClick={useDetectedLocationForPicking} disabled={!location}>
                    {t("operations.map.useDetectedLocation")}
                  </button>
                </div>
                <p className="muted small">{t("operations.map.exactPointHelp")}</p>

                <div className="inline">
                  <button type="submit" disabled={savingArea}>
                    {savingArea ? t("operations.map.saving") : t("operations.map.saveArea")}
                  </button>
                  <button type="button" onClick={() => setShowCreateArea(false)} disabled={savingArea}>
                    {t("common.close")}
                  </button>
                </div>

                {areaNotice && (
                  <p
                    className={areaNoticeTone === "success" ? styles.notice : "error"}
                    role={areaNoticeTone === "success" ? "status" : "alert"}
                  >
                    {areaNotice}
                  </p>
                )}
              </form>
            )}
          </section>
        )}
      </div>
    </>
  );
}

export default function OperationsMapPage() {
  return (
    <AuthGuard allowedRoles={[RoleNames.LogisticsManager, RoleNames.ShelterAdmin, RoleNames.VolunteerCoordinator, RoleNames.EmergencyCoordinator, RoleNames.SystemAdmin]}>
      <div className="dev4-operations">
        <OperationsMapContent />
      </div>
    </AuthGuard>
  );
}
