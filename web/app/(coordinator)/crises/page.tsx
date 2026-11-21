"use client";

import { useEffect, useState, type FormEvent } from "react";
import {
  CRISIS_STATUSES,
  casesApi,
  type Area,
  type Crisis,
  type CrisisStatus,
  type CrisisSummary
} from "../../../components/cases/casesApi";
import { EmptyState, ErrorState, LoadingState } from "../../../components/cases/PageState";

export default function CoordinatorCrisesPage() {
  const [crises, setCrises] = useState<Crisis[]>([]);
  const [areas, setAreas] = useState<Area[]>([]);
  const [summary, setSummary] = useState<CrisisSummary | null>(null);
  const [selectedId, setSelectedId] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [crisisName, setCrisisName] = useState("");
  const [crisisType, setCrisisType] = useState("Flood");
  const [areaName, setAreaName] = useState("");
  const [areaLat, setAreaLat] = useState("7.2906");
  const [areaLng, setAreaLng] = useState("80.6337");
  const [alertTitle, setAlertTitle] = useState("");
  const [alertBody, setAlertBody] = useState("");

  const loadCrises = () => {
    casesApi
      .crises()
      .then((items) => {
        setCrises(items);
        setSelectedId((current) => current || items[0]?.id || "");
      })
      .catch((cause: unknown) =>
        setError(cause instanceof Error ? cause.message : "Crises could not be loaded.")
      )
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadCrises();
  }, []);

  async function updateSelectedStatus(status: CrisisStatus) {
    if (!selectedId) return;
    setError("");
    try {
      await casesApi.updateCrisis(selectedId, {
        status,
        endedAt: status === "Closed" ? new Date().toISOString() : undefined
      });
      loadCrises();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Crisis could not be updated.");
    }
  }

  useEffect(() => {
    if (!selectedId) return;
    casesApi.areas(selectedId).then(setAreas).catch(() => setAreas([]));
    casesApi.crisisSummary(selectedId).then(setSummary).catch(() => setSummary(null));
  }, [selectedId]);

  async function createCrisis(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");
    try {
      await casesApi.createCrisis({
        name: crisisName,
        type: crisisType,
        startedAt: new Date().toISOString()
      });
      setCrisisName("");
      loadCrises();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Crisis could not be created.");
    }
  }

  async function createArea(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedId) return;
    setError("");
    try {
      await casesApi.createArea(selectedId, {
        name: areaName,
        lat: Number(areaLat),
        lng: Number(areaLng)
      });
      setAreaName("");
      setAreas(await casesApi.areas(selectedId));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Area could not be created.");
    }
  }

  async function createAlert(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedId) return;
    setError("");
    try {
      await casesApi.createAlert(selectedId, {
        title: alertTitle,
        body: alertBody,
        publish: true
      });
      setAlertTitle("");
      setAlertBody("");
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Alert could not be created.");
    }
  }

  return (
    <div>
      <h1>Crises and areas</h1>
      {loading ? <LoadingState /> : null}
      {error ? <ErrorState message={error} /> : null}

      <section className="panel" aria-labelledby="crisis-list-heading">
        <h2 id="crisis-list-heading">Crises</h2>
        {crises.length === 0 ? <EmptyState message="No crises have been created." /> : null}
        <ul>
          {crises.map((crisis) => (
            <li key={crisis.id}>
              <button type="button" onClick={() => setSelectedId(crisis.id)}>
                {crisis.name} · {crisis.status}
              </button>
            </li>
          ))}
        </ul>
      </section>

      <section className="panel" aria-labelledby="new-crisis-heading">
        <h2 id="new-crisis-heading">Create crisis</h2>
        <form onSubmit={(event) => void createCrisis(event)}>
          <label htmlFor="crisis-name">Name</label>
          <input id="crisis-name" required maxLength={200} value={crisisName} onChange={(event) => setCrisisName(event.target.value)} />
          <label htmlFor="crisis-type">Type</label>
          <input id="crisis-type" required maxLength={30} value={crisisType} onChange={(event) => setCrisisType(event.target.value)} />
          <button type="submit">Create</button>
        </form>
      </section>

      {selectedId ? (
        <>
          <section className="panel" aria-labelledby="crisis-edit-heading">
            <h2 id="crisis-edit-heading">Update selected crisis</h2>
            <label htmlFor="crisis-status">Status</label>
            <select
              id="crisis-status"
              value={crises.find((crisis) => crisis.id === selectedId)?.status ?? ""}
              onChange={(event) => void updateSelectedStatus(event.target.value as CrisisStatus)}
            >
              {CRISIS_STATUSES.map((status) => <option key={status} value={status}>{status}</option>)}
            </select>
            <p className="muted">Open total: {summary?.openTotal ?? "—"} · Overdue: {summary?.overdueTotal ?? "—"}</p>
          </section>

          <section className="panel" aria-labelledby="area-heading">
            <h2 id="area-heading">Areas</h2>
            {areas.length === 0 ? <EmptyState message="No areas for this crisis." /> : null}
            <ul>{areas.map((area) => <li key={area.id}>{area.name} ({area.lat}, {area.lng})</li>)}</ul>
            <form onSubmit={(event) => void createArea(event)}>
              <label htmlFor="area-name">Area name</label>
              <input id="area-name" required maxLength={200} value={areaName} onChange={(event) => setAreaName(event.target.value)} />
              <label htmlFor="area-lat">Latitude</label>
              <input id="area-lat" type="number" step="any" min={-90} max={90} required value={areaLat} onChange={(event) => setAreaLat(event.target.value)} />
              <label htmlFor="area-lng">Longitude</label>
              <input id="area-lng" type="number" step="any" min={-180} max={180} required value={areaLng} onChange={(event) => setAreaLng(event.target.value)} />
              <button type="submit">Add area</button>
            </form>
          </section>

          <section className="panel" aria-labelledby="alert-heading">
            <h2 id="alert-heading">Publish alert</h2>
            <form onSubmit={(event) => void createAlert(event)}>
              <label htmlFor="alert-title">Title</label>
              <input id="alert-title" required maxLength={200} value={alertTitle} onChange={(event) => setAlertTitle(event.target.value)} />
              <label htmlFor="alert-body">Body</label>
              <textarea id="alert-body" required maxLength={2000} value={alertBody} onChange={(event) => setAlertBody(event.target.value)} />
              <button type="submit">Publish</button>
            </form>
            <p className="muted">Alert editing is not exposed by the current server contract.</p>
          </section>
        </>
      ) : null}
    </div>
  );
}
