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
import { CRISIS_STATUS_LABELS } from "../../../components/cases/statusLabels";

export default function CoordinatorCrisesPage() {
  const [crises, setCrises] = useState<Crisis[]>([]);
  const [areas, setAreas] = useState<Area[]>([]);
  const [summary, setSummary] = useState<CrisisSummary | null>(null);
  const [selectedId, setSelectedId] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [referenceError, setReferenceError] = useState("");

  const [crisisName, setCrisisName] = useState("");
  const [crisisType, setCrisisType] = useState("Flood");
  const [editName, setEditName] = useState("");
  const [editType, setEditType] = useState("Flood");
  const [editDescription, setEditDescription] = useState("");
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
    setBusy(true);
    setError("");
    try {
      await casesApi.updateCrisis(selectedId, {
        status,
        endedAt: status === "Closed" ? new Date().toISOString() : undefined
      });
      loadCrises();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Crisis could not be updated.");
    } finally {
      setBusy(false);
    }
  }

  async function saveSelectedCrisis(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedId) return;
    setBusy(true);
    setError("");
    try {
      await casesApi.updateCrisis(selectedId, {
        name: editName,
        type: editType,
        description: editDescription || undefined
      });
      loadCrises();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Crisis details could not be saved.");
    } finally {
      setBusy(false);
    }
  }

  useEffect(() => {
    if (!selectedId) return;
    const selected = crises.find((crisis) => crisis.id === selectedId);
    setEditName(selected?.name ?? "");
    setEditType(selected?.type ?? "Flood");
    setEditDescription(selected?.description ?? "");
    setReferenceError("");
    casesApi.areas(selectedId).then(setAreas).catch(() => {
      setAreas([]);
      setReferenceError("Area data could not be loaded for this crisis.");
    });
    casesApi.crisisSummary(selectedId).then(setSummary).catch(() => {
      setSummary(null);
      setReferenceError("Crisis summary could not be loaded for this crisis.");
    });
  }, [selectedId, crises]);

  async function createCrisis(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
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
    } finally {
      setBusy(false);
    }
  }

  async function createArea(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedId) return;
    setBusy(true);
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
    } finally {
      setBusy(false);
    }
  }

  async function createAlert(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedId) return;
    setBusy(true);
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
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <h1>Crises and areas</h1>
      {loading ? <LoadingState /> : null}
      {error ? <ErrorState message={error} /> : null}
      {referenceError ? <ErrorState message={referenceError} /> : null}

      <section className="panel" aria-labelledby="crisis-list-heading">
        <h2 id="crisis-list-heading">Crises</h2>
        {crises.length === 0 ? <EmptyState message="No crises have been created." /> : null}
        <ul>
          {crises.map((crisis) => (
            <li key={crisis.id}>
              <button type="button" onClick={() => setSelectedId(crisis.id)}>
                {crisis.name} · {CRISIS_STATUS_LABELS[crisis.status]}
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
          <button type="submit" disabled={busy}>{busy ? "Saving…" : "Create"}</button>
        </form>
      </section>

      {selectedId ? (
        <>
          <section className="panel" aria-labelledby="crisis-edit-heading">
            <h2 id="crisis-edit-heading">Update selected crisis</h2>
            <form onSubmit={(event) => void saveSelectedCrisis(event)}>
              <label htmlFor="edit-crisis-name">Name</label>
              <input id="edit-crisis-name" required maxLength={200} value={editName} onChange={(event) => setEditName(event.target.value)} />
              <label htmlFor="edit-crisis-type">Type</label>
              <input id="edit-crisis-type" required maxLength={30} value={editType} onChange={(event) => setEditType(event.target.value)} />
              <label htmlFor="edit-crisis-description">Description (optional)</label>
              <textarea id="edit-crisis-description" maxLength={2000} value={editDescription} onChange={(event) => setEditDescription(event.target.value)} />
              <button type="submit" disabled={busy}>{busy ? "Saving…" : "Save details"}</button>
            </form>
            <label htmlFor="crisis-status">Status</label>
            <select
              id="crisis-status"
              disabled={busy}
              value={crises.find((crisis) => crisis.id === selectedId)?.status ?? ""}
              onChange={(event) => void updateSelectedStatus(event.target.value as CrisisStatus)}
            >
              {CRISIS_STATUSES.map((status) => <option key={status} value={status}>{CRISIS_STATUS_LABELS[status]}</option>)}
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
              <button type="submit" disabled={busy}>{busy ? "Saving…" : "Add area"}</button>
            </form>
          </section>

          <section className="panel" aria-labelledby="alert-heading">
            <h2 id="alert-heading">Publish alert</h2>
            <form onSubmit={(event) => void createAlert(event)}>
              <label htmlFor="alert-title">Title</label>
              <input id="alert-title" required maxLength={200} value={alertTitle} onChange={(event) => setAlertTitle(event.target.value)} />
              <label htmlFor="alert-body">Body</label>
              <textarea id="alert-body" required maxLength={2000} value={alertBody} onChange={(event) => setAlertBody(event.target.value)} />
              <button type="submit" disabled={busy}>{busy ? "Publishing…" : "Publish"}</button>
            </form>
            <p className="muted">Alert editing is not exposed by the current server contract.</p>
          </section>
        </>
      ) : null}
    </div>
  );
}
