"use client";

import { AuthGuard } from "@/components/AuthGuard";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useT } from "@/lib/i18n";
import { RoleNames } from "@/lib/roles";

// Member 4 / D4-11 operations map. Data comes through the integrated SAFEWAY operations API adapter.
// Each source reports its own loading/error state so a 403 on areas never
// silently hides shelters or service centres.

import dynamic from "next/dynamic";
import { useEffect, useMemo, useState } from "react";
import { api } from "@/lib/operations-api";
import type { MapMarker } from "@/components/map/MapView";
import type { MapPoint } from "@/components/map/MapPicker";

const MapView = dynamic(() => import("@/components/map/MapView"), { ssr: false });
const MapPicker = dynamic(() => import("@/components/map/MapPicker"), { ssr: false });

interface PublicShelter {
  id: string;
  name: string;
  lat: number;
  lng: number;
  freePlaces: number;
  capacity: number;
  occupancy: number;
}
interface ServiceCentre {
  id: string;
  name: string;
  address: string;
  lat: number | string;
  lng: number | string;
  services?: string;
  openingHours?: string | null;
}
interface Area {
  id: string;
  name: string;
  lat: number | string;
  lng: number | string;
  radiusKm?: number | string | null;
  description?: string | null;
}

type MarkerSource =
  | {
      kind: "shelter";
      id: string;
      lat: number;
      lng: number;
      label: string;
      freePlaces: number;
      occupancy: number;
      capacity: number;
    }
  | {
      kind: "service";
      id: string;
      lat: number;
      lng: number;
      label: string;
      description: string;
    }
  | {
      kind: "area";
      id: string;
      lat: number;
      lng: number;
      label: string;
      radiusKm?: number | string | null;
      description?: string | null;
    };

function OperationsMapContent() {
  const t = useT();
  const [sources, setSources] = useState<MarkerSource[]>([]);
  const [loading, setLoading] = useState(true);
  const [shelterError, setShelterError] = useState<unknown>(null);
  const [centreError, setCentreError] = useState<unknown>(null);
  const [areaError, setAreaError] = useState<unknown>(null);
  const [picked, setPicked] = useState<MapPoint | null>(null);

  useEffect(() => {
    let active = true;
    async function load() {
      setLoading(true);
      const next: MarkerSource[] = [];

      try {
        const shelters = await api<PublicShelter[]>("/shelters/public");
        shelters.forEach((shelter) => next.push({
          kind: "shelter",
          id: shelter.id,
          lat: Number(shelter.lat),
          lng: Number(shelter.lng),
          label: shelter.name,
          freePlaces: shelter.freePlaces,
          occupancy: shelter.occupancy,
          capacity: shelter.capacity
        }));
        if (active) setShelterError(null);
      } catch (error) {
        if (active) setShelterError(error);
      }

      try {
        const centres = await api<ServiceCentre[]>("/public/service-centres");
        centres.forEach((centre) => next.push({
          kind: "service",
          id: centre.id,
          lat: Number(centre.lat),
          lng: Number(centre.lng),
          label: centre.name,
          description: centre.address || centre.services || ""
        }));
        if (active) setCentreError(null);
      } catch (error) {
        if (active) setCentreError(error);
      }

      try {
        const areas = await api<Area[]>("/operations/areas");
        areas.forEach((area) => next.push({
          kind: "area",
          id: area.id,
          lat: Number(area.lat),
          lng: Number(area.lng),
          label: area.name,
          radiusKm: area.radiusKm,
          description: area.description
        }));
        if (active) setAreaError(null);
      } catch (error) {
        // Areas are staff-only. Show the failure instead of pretending the map is complete.
        if (active) setAreaError(error);
      }

      if (active) {
        setSources(next);
        setLoading(false);
      }
    }
    void load();
    return () => { active = false; };
  }, []);

  const markers = useMemo<MapMarker[]>(() => sources.map((source) => {
    if (source.kind === "shelter") {
      return {
        id: source.id,
        lat: source.lat,
        lng: source.lng,
        label: source.label,
        type: "shelter",
        description: t("operations.map.freePlaces", {
          freePlaces: source.freePlaces,
          occupancy: source.occupancy,
          capacity: source.capacity
        })
      };
    }

    if (source.kind === "service") {
      return {
        id: source.id,
        lat: source.lat,
        lng: source.lng,
        label: source.label,
        type: "service-centre",
        description: source.description || t("operations.map.serviceCentre")
      };
    }

    return {
      id: source.id,
      lat: source.lat,
      lng: source.lng,
      label: source.label,
      type: "area",
      description: source.description ?? (source.radiusKm
        ? t("operations.map.radius", { radiusKm: source.radiusKm })
        : t("operations.map.operationalArea"))
    };
  }), [sources, t]);

  return (
    <>
      <h1>{t("operations.map.title")}</h1>
      <p className="muted">{t("operations.map.subtitle")}</p>

      {loading && <p className="muted" role="status">{t("operations.map.loading")}</p>}
      {shelterError ? <p className="error" role="alert">{t("operations.map.sheltersError", { error: localizedErrorMessage(shelterError, t) })}</p> : null}
      {centreError ? <p className="error" role="alert">{t("operations.map.centresError", { error: localizedErrorMessage(centreError, t) })}</p> : null}
      {areaError ? <p className="error" role="alert">{t("operations.map.areasError", { error: localizedErrorMessage(areaError, t) })}</p> : null}

      <MapView markers={markers} />

      <section className="panel" aria-labelledby="map-picker-heading">
        <h2 id="map-picker-heading">{t("operations.map.pickLocation")}</h2>
        <p className="muted">{t("operations.map.pickerHelp")}</p>
        <MapPicker
          height={300}
          value={picked ?? undefined}
          onChange={(point) => setPicked(point)}
        />
        {picked && (
          <p className="success" role="status">
            {t("operations.map.selected", { lat: picked.lat.toFixed(5), lng: picked.lng.toFixed(5) })}{picked.address ? t("operations.map.selectedAddress", { address: picked.address }) : ""}
          </p>
        )}
      </section>
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
