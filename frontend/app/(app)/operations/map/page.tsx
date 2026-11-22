"use client";

import { AuthGuard } from "@/components/AuthGuard";
import { RoleNames } from "@/lib/roles";

// Member 4 / D4-11 operations map. Data comes through the integrated SAFEWAY operations API adapter.
// Each source reports its own loading/error state so a 403 on areas never
// silently hides shelters or service centres.

import dynamic from "next/dynamic";
import { useEffect, useState } from "react";
import { api, errorMessage } from "@/lib/operations-api";
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

function OperationsMapContent() {
  const [markers, setMarkers] = useState<MapMarker[]>([]);
  const [loading, setLoading] = useState(true);
  const [shelterError, setShelterError] = useState("");
  const [centreError, setCentreError] = useState("");
  const [areaError, setAreaError] = useState("");
  const [picked, setPicked] = useState<MapPoint | null>(null);

  useEffect(() => {
    let active = true;
    async function load() {
      setLoading(true);
      const next: MapMarker[] = [];

      try {
        const shelters = await api<PublicShelter[]>("/shelters/public");
        shelters.forEach((shelter) => next.push({
          id: shelter.id,
          lat: Number(shelter.lat),
          lng: Number(shelter.lng),
          label: shelter.name,
          type: "shelter",
          description: `${shelter.freePlaces} free places (${shelter.occupancy}/${shelter.capacity} occupied)`,
        }));
        if (active) setShelterError("");
      } catch (error) {
        if (active) setShelterError(errorMessage(error));
      }

      try {
        const centres = await api<ServiceCentre[]>("/public/service-centres");
        centres.forEach((centre) => next.push({
          id: centre.id,
          lat: Number(centre.lat),
          lng: Number(centre.lng),
          label: centre.name,
          type: "service-centre",
          description: centre.address || centre.services || "Service centre",
        }));
        if (active) setCentreError("");
      } catch (error) {
        if (active) setCentreError(errorMessage(error));
      }

      try {
        const areas = await api<Area[]>("/operations/areas");
        areas.forEach((area) => next.push({
          id: area.id,
          lat: Number(area.lat),
          lng: Number(area.lng),
          label: area.name,
          type: "area",
          description: area.description ?? (area.radiusKm ? `Radius ${area.radiusKm} km` : "Operational area"),
        }));
        if (active) setAreaError("");
      } catch (error) {
        // Areas are staff-only. Show the failure instead of pretending the map is complete.
        if (active) setAreaError(errorMessage(error));
      }

      if (active) {
        setMarkers(next);
        setLoading(false);
      }
    }
    void load();
    return () => { active = false; };
  }, []);

  return (
    <>
      <h1>Operations map</h1>
      <p className="muted">Approved shelters, service centres and operational areas. Coordinates can always be entered manually when the geocoder is offline.</p>

      {loading && <p className="muted" role="status">Loading map data…</p>}
      {shelterError && <p className="error" role="alert">Shelters: {shelterError}</p>}
      {centreError && <p className="error" role="alert">Service centres: {centreError}</p>}
      {areaError && <p className="error" role="alert">Operational areas: {areaError}</p>}

      <MapView markers={markers} />

      <section className="panel" aria-labelledby="map-picker-heading">
        <h2 id="map-picker-heading">Pick a location</h2>
        <p className="muted">This is the same MapPicker used by location forms. Search fails soft and manual coordinates stay available.</p>
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
