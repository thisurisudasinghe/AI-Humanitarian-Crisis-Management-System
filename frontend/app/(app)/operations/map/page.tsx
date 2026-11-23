"use client";

import { AuthGuard } from "@/components/AuthGuard";
import { useAuth } from "@/lib/auth";
import { RoleNames } from "@/lib/roles";

// Member 4 / D4-11 operations map. Data comes from existing SAFEWAY APIs only.
// Each source reports its own loading/error state so a 403 on one layer never
// silently hides every other layer.

import dynamic from "next/dynamic";
import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from "react";
import { api as coreApi } from "@/lib/api";
import { api, errorMessage, type Paged } from "@/lib/operations-api";
import type { MapFocus, MapMarker } from "@/components/map/MapView";
import type { MapPoint } from "@/components/map/MapPicker";
import { MAP_MARKER_TYPES, markerLabel } from "@/components/map/mapTypes";
import { useUserLocation, type LocationStatus } from "@/components/map/useUserLocation";
import styles from "./page.module.css";

const MapView = dynamic(() => import("@/components/map/MapView"), { ssr: false });
const MapPicker = dynamic(() => import("@/components/map/MapPicker"), { ssr: false });

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

const SOURCE_LABELS: Record<string, string> = {
  shelters: "Approved shelters",
  centres: "Service centres",
  areas: "Operational areas",
  resources: "Resource stock",
  volunteers: "Volunteer locations",
  tasks: "Volunteer tasks"
};

const LOCATION_LABELS: Record<LocationStatus, string> = {
  idle: "Not started",
  detecting: "Detecting location",
  detected: "Location detected",
  denied: "Permission denied",
  unavailable: "Location unavailable",
  timeout: "GPS timeout",
  unsupported: "Browser not supported",
  insecure: "HTTPS required",
  paused: "Tracking paused",
  disabled: "Tracking disabled"
};

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

function formatQuantity(value: unknown): string {
  const parsed = finiteNumber(value);
  return parsed === null
    ? String(value ?? "0")
    : parsed.toLocaleString(undefined, { maximumFractionDigits: 2 });
}

function formatDate(value?: string | null): string {
  if (!value) return "not scheduled";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
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
  } = useUserLocation(Boolean(user) && trackingEnabled);

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
                    `Approved shelter • ${formatQuantity(row.freePlaces)} free of ${formatQuantity(row.capacity)} places`,
                    `${formatQuantity(row.occupancy)} occupied`,
                    `Facilities: ${parseStringList(row.facilities).join(", ") || "not listed"}`,
                    `Accessibility: ${parseStringList(row.accessibilityFeatures).join(", ") || "not listed"}`,
                    `Services: ${parseStringList(row.services).join(", ") || "not listed"}`
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
                    row.address || "Service centre",
                    row.services ? `Services: ${row.services}` : "",
                    `Hours: ${row.openingHours || "not listed"}`
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
                  `Operational area${row.radiusKm ? ` • radius ${formatQuantity(row.radiusKm)} km` : ""} • crisis ${row.crisisId}`
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
                const unit = type?.unit || "units";
                return buildMarker(
                  row.id,
                  finiteNumber(row.lat),
                  finiteNumber(row.lng),
                  type?.name || "Resource stock",
                  "resource",
                  [
                    `${formatQuantity(row.available)} ${unit} available of ${formatQuantity(row.quantity)} ${unit}`,
                    row.accessibilityCapable ? "Accessibility capable" : "",
                    row.expiryDate ? `Expires ${formatDate(row.expiryDate)}` : ""
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
                  "Verified volunteer",
                  "volunteer",
                  `Staff-only location • Skills: ${parseStringList(row.skillsJson).join(", ") || "not listed"} • Languages: ${parseStringList(row.languagesJson).join(", ") || "not listed"}`
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
                  `${row.status} task • Skills: ${parseStringList(row.requiredSkillsJson).join(", ") || "not listed"} • Scheduled ${formatDate(row.scheduledAt)}`
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
          errors[tasks[index].key] = errorMessage(result.reason);
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
  }, [canSeeVolunteers, reloadNonce]);

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
        label: "Your current location",
        description: location.accuracy != null
          ? `GPS accuracy about ${Math.round(location.accuracy)} m • Browser-only unless you explicitly save it in an area`
          : "Browser-only unless you explicitly save it in an area"
      });
    }
    if (searchResult) list.push(searchResult);
    return list;
  }, [markers, location, searchResult]);

  const filteredMarkers = useMemo(() => {
    const query = textFilter.trim().toLowerCase();
    return allMarkers.filter((marker) => {
      const matchesType = typeFilter === "all" || marker.type === typeFilter;
      const haystack = [
        marker.label ?? marker.name ?? "",
        marker.description ?? "",
        markerLabel(marker.type)
      ].join(" ").toLowerCase();
      return matchesType && (!query || haystack.includes(query));
    });
  }, [allMarkers, textFilter, typeFilter]);

  const selectMarker = useCallback((marker: MapMarker) => {
    setSelectedMarker(marker);
    setFocus({ lat: marker.lat, lng: marker.lng, zoom: 14, nonce: Date.now() });
  }, []);

  const centreOnMyLocation = () => {
    if (!location) {
      setActionNotice("No location has been detected yet. Use the manual picker below if needed.");
      return;
    }
    setActionNotice("Centred on your latest detected position.");
    setFocus({ lat: location.lat, lng: location.lng, zoom: 15, nonce: Date.now() });
  };

  const useDetectedLocationForPicking = () => {
    if (!location) {
      setAreaNotice("No location has been detected yet. Use the manual picker below.");
      return;
    }
    setPicked({
      lat: location.lat,
      lng: location.lng,
      address: location.accuracy != null
        ? `Detected location (±${Math.round(location.accuracy)} m)`
        : "Detected location"
    });
    setAreaNotice("Detected location copied into the form. Review it before saving.");
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
        if (active) setDistanceError(errorMessage(error));
      })
      .finally(() => {
        if (active) setDistanceLoading(false);
      });

    return () => {
      active = false;
    };
  }, [location?.lat, location?.lng, selectedMarker?.id, selectedMarker?.lat, selectedMarker?.lng, selectedMarker?.type]);

  async function handleSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const query = searchQuery.trim();
    if (!query) {
      setSearchError("Enter a place name or address to search.");
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
        setSearchError("No coordinates were found for that search. Use the manual location picker below.");
        return;
      }

      const result: MapMarker = {
        id: `search-${Date.now()}`,
        lat: lat!,
        lng: lng!,
        type: "search-result",
        label: query,
        description: "Geocoded search result • approximate location"
      };
      setSearchResult(result);
      selectMarker(result);
    } catch (error) {
      setSearchResult(null);
      setSearchError(`${errorMessage(error)} The manual location picker below remains available.`);
    } finally {
      setSearching(false);
    }
  }

  async function openCreateArea() {
    setAreaNotice("");
    setShowCreateArea(true);
    if (crises.length > 0) return;
    try {
      setCrises(await coreApi<CrisisRow[]>("/api/v1/crises"));
    } catch (error) {
      setAreaNotice(errorMessage(error));
    }
  }

  async function submitArea(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setAreaNotice("");

    const name = areaName.trim();
    if (!crisisId) {
      setAreaNotice("Select the crisis this area belongs to.");
      return;
    }
    if (!name) {
      setAreaNotice("Enter an area name.");
      return;
    }
    if (name.length > 200 || areaDescription.trim().length > 2000) {
      setAreaNotice("Name must be 200 characters or fewer and description 2000 characters or fewer.");
      return;
    }
    if (!picked || !validPoint(finiteNumber(picked.lat), finiteNumber(picked.lng))) {
      setAreaNotice("Select a valid location on the map before saving.");
      return;
    }
    const radius = areaRadius.trim() ? Number(areaRadius) : null;
    if (radius !== null && (!Number.isFinite(radius) || radius < 0 || radius > 1000)) {
      setAreaNotice("Radius must be between 0 and 1000 km, or leave it blank.");
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
      setAreaNotice(`Area "${name}" saved. Refreshing operational area markers…`);
      setAreaName("");
      setAreaDescription("");
      setAreaRadius("");
      setReloadNonce((value) => value + 1);
    } catch (error) {
      setAreaNotice(errorMessage(error));
    } finally {
      setSavingArea(false);
    }
  }

  const sourceErrorEntries = Object.entries(sourceErrors).filter(([, message]) => Boolean(message));

  return (
    <>
      <div className={styles.headerRow}>
        <h1>Operations map</h1>
        <span className="muted" role="status">
          {loading ? "Loading SAFEWAY map layers…" : `${filteredMarkers.length} of ${allMarkers.length} markers shown`}
        </span>
      </div>
      <p className="muted">
        Approved shelters, service centres, resource stock, role-permitted volunteer data and operational areas from the live SAFEWAY APIs.
        Markers without valid coordinates are omitted rather than placed misleadingly.
      </p>

      <section className="panel" aria-labelledby="map-location-heading">
        <h2 id="map-location-heading">Current location</h2>
        <p className="muted">
          SAFEWAY asks for browser location permission only to show your position on this map and to centre it for you.
          Coordinates stay in this browser tab: they are not saved, sent to the backend, or shown to other users.
          The only exception is when you explicitly choose “Use detected location” and save an operational area below.
          Tracking stops when you leave the page, hide the tab, log out, or turn it off.
        </p>
        <div className={styles.statusRow}>
          <span className={statusClass(locationStatus)}>{LOCATION_LABELS[locationStatus]}</span>
          {location && (
            <span className="muted small">
              {location.lat.toFixed(5)}, {location.lng.toFixed(5)}
              {location.accuracy != null ? ` • accuracy ±${Math.round(location.accuracy)} m` : " • accuracy unavailable"}
            </span>
          )}
        </div>
        <p className={["denied", "unavailable", "timeout", "unsupported", "insecure"].includes(locationStatus) ? "error" : "muted"} role={["denied", "unavailable", "timeout", "unsupported", "insecure"].includes(locationStatus) ? "alert" : "status"}>
          {locationMessage}
        </p>
        <div className={styles.actionRow}>
          <button type="button" onClick={() => setTrackingEnabled((value) => !value)}>
            {trackingEnabled ? "Stop live tracking" : "Start live tracking"}
          </button>
          <button type="button" onClick={retryLocation} disabled={!trackingEnabled}>
            Retry detection
          </button>
          <button type="button" onClick={centreOnMyLocation} disabled={!location}>
            My Location
          </button>
        </div>
        {actionNotice && <p className="muted small" role="status">{actionNotice}</p>}
      </section>

      <section className="panel" aria-labelledby="map-filters-heading">
        <h2 id="map-filters-heading">Search and filter</h2>
        <form className="filters" onSubmit={(event) => void handleSearch(event)}>
          <label htmlFor="map-location-search">Find a place</label>
          <input
            id="map-location-search"
            type="search"
            value={searchQuery}
            disabled={searching}
            placeholder="e.g. Negombo, Sri Lanka"
            onChange={(event) => setSearchQuery(event.target.value)}
          />
          <button type="submit" disabled={searching || !searchQuery.trim()}>
            {searching ? "Searching…" : "Search map"}
          </button>
          {searchResult && (
            <button type="button" onClick={() => setSearchResult(null)}>Clear result</button>
          )}
        </form>
        {searchError && <p className="error" role="alert">{searchError}</p>}
        <div className="filters">
          <label htmlFor="map-type-filter">Layer</label>
          <select
            id="map-type-filter"
            value={typeFilter}
            onChange={(event) => setTypeFilter(event.target.value)}
          >
            <option value="all">All layers</option>
            {Object.entries(MAP_MARKER_TYPES)
              .filter(([key]) => key !== "location")
              .map(([key, value]) => (
                <option key={key} value={key}>{value.label}</option>
              ))}
          </select>
          <label htmlFor="map-text-filter">Filter markers</label>
          <input
            id="map-text-filter"
            type="search"
            value={textFilter}
            placeholder="Name, type or detail"
            onChange={(event) => setTextFilter(event.target.value)}
          />
        </div>
        {!canSeeVolunteers && (
          <p className="muted small">Volunteer layers are hidden for your role.</p>
        )}
      </section>

      {sourceErrorEntries.length > 0 && (
        <div className="error" role="alert">
          <strong>Some map layers could not load:</strong>
          <ul className={styles.sourceList}>
            {sourceErrorEntries.map(([key, message]) => (
              <li key={key}>{SOURCE_LABELS[key] ?? key}: {message}</li>
            ))}
          </ul>
        </div>
      )}

      {loading && <p className="muted" role="status">Loading map data…</p>}

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
            {selectedMarker.label ?? selectedMarker.name ?? "Location"} — {markerLabel(selectedMarker.type)}
          </p>
          <p className="muted">
            {selectedMarker.description || "No further details are available for this marker."}
          </p>
          <p className="muted small">
            Coordinates: {selectedMarker.lat.toFixed(5)}, {selectedMarker.lng.toFixed(5)}
          </p>
          {selectedMarker.type !== "current-location" && location && (
            <p className="muted small" role="status">
              {distanceLoading
                ? "Calculating distance…"
                : routeDistance
                  ? `Distance from your location: ${routeDistance.km.toFixed(2)} km` +
                    (routeDistance.minutes != null ? ` (about ${routeDistance.minutes.toFixed(1)} min)` : "") +
                    (routeDistance.isApproximate ? " — approximate straight-line fallback; routing provider unavailable." : " — routed estimate.")
                  : distanceError
                    ? `Distance unavailable: ${distanceError}`
                    : "Distance unavailable."}
            </p>
          )}
        </section>
      )}

      <div className={styles.split}>
        <section className="panel" aria-labelledby="map-picker-heading">
          <h2 id="map-picker-heading">Manual location fallback</h2>
          <p className="muted small">
            Use this when geolocation is denied, unavailable, or the geocoding provider is blocked.
            Click the map or enter coordinates directly; nothing is saved until you submit an area below.
          </p>
          <MapPicker
            height={300}
            value={picked ?? undefined}
            onChange={(point) => setPicked(point)}
          />
          {picked && (
            <p className="success" role="status">
              Selected: {picked.lat.toFixed(5)}, {picked.lng.toFixed(5)}{picked.address ? ` — ${picked.address}` : ""}
            </p>
          )}
        </section>

        {canCreateArea && (
          <section className="panel" aria-labelledby="create-area-heading">
            <h2 id="create-area-heading">Create operational area</h2>
            <p className="muted small">
              Saves through the existing SAFEWAY crisis API. Emergency Coordinator permission is required.
            </p>
            {!showCreateArea ? (
              <button type="button" onClick={() => void openCreateArea()}>Create Area</button>
            ) : (
              <form className="form-grid" onSubmit={(event) => void submitArea(event)}>
                <label htmlFor="area-crisis">Crisis</label>
                <select
                  id="area-crisis"
                  value={crisisId}
                  onChange={(event) => setCrisisId(event.target.value)}
                  required
                >
                  <option value="">Select a crisis…</option>
                  {crises.map((crisis) => (
                    <option key={crisis.id} value={crisis.id}>{crisis.name}</option>
                  ))}
                </select>

                <label htmlFor="area-name">Area name</label>
                <input
                  id="area-name"
                  value={areaName}
                  maxLength={200}
                  onChange={(event) => setAreaName(event.target.value)}
                  required
                />

                <label htmlFor="area-description">Description</label>
                <textarea
                  id="area-description"
                  rows={3}
                  value={areaDescription}
                  maxLength={2000}
                  onChange={(event) => setAreaDescription(event.target.value)}
                />

                <label htmlFor="area-radius">Radius (km, optional)</label>
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
                    Use detected location
                  </button>
                </div>
                <p className="muted small">
                  Select the exact point in the manual location picker beside this form.
                </p>

                <div className="inline">
                  <button type="submit" disabled={savingArea}>
                    {savingArea ? "Saving…" : "Save area"}
                  </button>
                  <button type="button" onClick={() => setShowCreateArea(false)} disabled={savingArea}>
                    Close
                  </button>
                </div>

                {areaNotice && (
                  <p
                    className={areaNotice.startsWith("Area \"") ? styles.notice : "error"}
                    role={areaNotice.startsWith("Area \"") ? "status" : "alert"}
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
