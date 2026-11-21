"use client";

import Link from "next/link";
import dynamic from "next/dynamic";
import { useEffect, useState } from "react";
import { casesApi, type ServiceCentre, type ShelterSummary } from "../../../components/cases/casesApi";
import { EmptyState, ErrorState, LoadingState } from "../../../components/cases/PageState";
import type { MapMarker } from "../../../components/map/MapView";

const MapView = dynamic(() => import("../../../components/map/MapView"), {
  ssr: false,
  loading: () => <p className="muted">Loading map…</p>
});

export default function ServiceCentresPage() {
  const [centres, setCentres] = useState<ServiceCentre[]>([]);
  const [shelters, setShelters] = useState<ShelterSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    Promise.allSettled([casesApi.publicServiceCentres(), casesApi.publicShelters()])
      .then(([centreResult, shelterResult]) => {
        if (centreResult.status === "fulfilled") {
          setCentres(centreResult.value);
        } else {
          setError(
            centreResult.reason instanceof Error
              ? centreResult.reason.message
              : "Service centres could not be loaded."
          );
        }
        if (shelterResult.status === "fulfilled") {
          setShelters(shelterResult.value);
        }
      })
      .finally(() => setLoading(false));
  }, []);

  const markers: MapMarker[] = [
    ...centres.map((item) => ({
      id: `centre-${item.id}`,
      name: item.name,
      lat: Number(item.lat),
      lng: Number(item.lng),
      description: item.address
    })),
    ...shelters.map((item) => ({
      id: `shelter-${item.id}`,
      name: item.name,
      lat: Number(item.lat),
      lng: Number(item.lng),
      description: `${item.freePlaces} places free`
    }))
  ];

  return (
    <div>
      <h1>Service centres and approved shelters</h1>
      <p>
        <Link href="/alerts">Read public alerts</Link> ·{" "}
        <Link href="/requests/new">Request assistance</Link>
      </p>
      {loading ? <LoadingState /> : null}
      {error ? <ErrorState message={error} /> : null}
      {!loading && markers.length === 0 ? (
        <EmptyState message="No public locations are available yet." />
      ) : null}

      <div className="grid">
        <section className="panel" aria-labelledby="centres-heading">
          <h2 id="centres-heading">Service centres</h2>
          <ul>
            {centres.map((item) => (
              <li key={item.id}>
                <strong>{item.name}</strong>
                <div>{item.address}</div>
                <div className="muted">{item.services}</div>
              </li>
            ))}
          </ul>
        </section>
        <section className="panel" aria-labelledby="shelters-heading">
          <h2 id="shelters-heading">Approved shelters</h2>
          <ul>
            {shelters.map((item) => (
              <li key={item.id}>
                <strong>{item.name}</strong>
                <div>{item.freePlaces} places free</div>
              </li>
            ))}
          </ul>
          <p className="muted">Shelter data is Member 4 owned and is read-only here.</p>
        </section>
      </div>
      {markers.length ? <MapView markers={markers} /> : null}
    </div>
  );
}
