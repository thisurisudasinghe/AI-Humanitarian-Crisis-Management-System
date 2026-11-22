"use client";

// Member 4 / D4-11 shelter admin. Capacity and occupancy guards are enforced by
// the SQL CHECK constraint and the guarded UPDATE; messages here only explain
// the server's ProblemDetails response.

import dynamic from "next/dynamic";
import { useEffect, useState } from "react";
import { api, errorMessage, Paged } from "../../../lib/api";

const MapPicker = dynamic(() => import("../../../components/map/MapPicker"), { ssr: false });

interface Shelter {
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
  orgId: string;
  approved: boolean;
}

interface FormState {
  name: string;
  capacity: string;
  facilities: string;
  accessibilityFeatures: string;
  services: string;
  lat: string;
  lng: string;
  orgId: string;
}

const EMPTY_FORM: FormState = {
  name: "",
  capacity: "",
  facilities: "[]",
  accessibilityFeatures: "[]",
  services: "[]",
  lat: "7.8731",
  lng: "80.7718",
  orgId: "",
};

export default function Shelters() {
  const [shelters, setShelters] = useState<Paged<Shelter>>({ items: [], total: 0, page: 1, pageSize: 50 });
  const [publicShelters, setPublicShelters] = useState<Shelter[]>([]);
  const [previewPublic, setPreviewPublic] = useState(false);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  const [form, setForm] = useState<FormState>(EMPTY_FORM);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [formError, setFormError] = useState("");
  const [capacityHint, setCapacityHint] = useState("");

  const [allocationOpen, setAllocationOpen] = useState(false);
  const [allocationDecision, setAllocationDecision] = useState("");
  const [allocationRequest, setAllocationRequest] = useState("");
  const [allocationHousehold, setAllocationHousehold] = useState("");
  const [allocationError, setAllocationError] = useState("");
  const [allocationResult, setAllocationResult] = useState("");

  async function load() {
    setBusy(true);
    setError("");
    try {
      const [staff, publicRows] = await Promise.all([
        api<Paged<Shelter>>("/shelters"),
        api<Shelter[]>("/shelters/public"),
      ]);
      setShelters(staff);
      setPublicShelters(publicRows);
    } catch (loadError) {
      setError(errorMessage(loadError));
    } finally {
      setBusy(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  function startEdit(shelter: Shelter) {
    setEditingId(shelter.id);
    setForm({
      name: shelter.name,
      capacity: String(shelter.capacity),
      facilities: shelter.facilities,
      accessibilityFeatures: shelter.accessibilityFeatures,
      services: shelter.services,
      lat: String(shelter.lat),
      lng: String(shelter.lng),
      orgId: shelter.orgId,
    });
    setCapacityHint(`Current occupancy: ${shelter.occupancy}. Capacity cannot be set below this value.`);
    setFormError("");
  }

  function resetForm() {
    setEditingId(null);
    setForm(EMPTY_FORM);
    setCapacityHint("");
    setFormError("");
  }

  async function saveShelter() {
    if (busy) return;
    setBusy(true);
    setFormError("");
    try {
      const payload = {
        name: form.name,
        capacity: Number(form.capacity),
        facilities: form.facilities || "[]",
        accessibilityFeatures: form.accessibilityFeatures || "[]",
        services: form.services || "[]",
        lat: Number(form.lat),
        lng: Number(form.lng),
        ...(editingId ? {} : { occupancy: 0, orgId: form.orgId }),
      };
      if (editingId) await api(`/shelters/${editingId}`, { method: "PATCH", body: JSON.stringify(payload) });
      else await api("/shelters", { method: "POST", body: JSON.stringify(payload) });
      resetForm();
      await load();
    } catch (saveError) {
      setFormError(errorMessage(saveError));
    } finally {
      setBusy(false);
    }
  }

  async function approve(id: string) {
    setBusy(true);
    setFormError("");
    try {
      await api(`/shelters/${id}/approve`, { method: "POST" });
      await load();
    } catch (approveError) {
      setFormError(errorMessage(approveError));
    } finally {
      setBusy(false);
    }
  }

  async function allocateShelter() {
    if (!allocationDecision.trim() || busy) return;
    setBusy(true);
    setAllocationError("");
    setAllocationResult("");
    try {
      const created = await api<{ id: string; householdSize: number }>("/shelters/allocations", {
        method: "POST",
        body: JSON.stringify({
          decisionId: allocationDecision.trim(),
          ...(allocationRequest.trim() ? { requestId: allocationRequest.trim() } : {}),
          ...(allocationHousehold ? { householdSize: Number(allocationHousehold) } : {}),
        }),
      });
      setAllocationResult(`Shelter allocation ${created.id} created for ${created.householdSize} people.`);
      setAllocationOpen(false);
      await load();
    } catch (allocationErrorValue) {
      setAllocationError(errorMessage(allocationErrorValue));
    } finally {
      setBusy(false);
    }
  }

  const rows = previewPublic ? publicShelters : shelters.items;

  return (
    <>
      <h1>Shelter management</h1>
      <p className="muted">Capacity, occupancy and approved public listing. Occupancy is changed only by the guarded allocation endpoint.</p>
      {error && <p className="error" role="alert">{error}</p>}
      {formError && <p className="error" role="alert">{formError}</p>}

      <section className="panel" aria-labelledby="shelter-list-heading">
        <div className="inline">
          <h2 id="shelter-list-heading">{previewPublic ? "Public preview" : "Shelters"}</h2>
          <button type="button" onClick={() => setPreviewPublic((current) => !current)}>
            {previewPublic ? "Show internal list" : "Show public preview"}
          </button>
          <button type="button" onClick={() => void load()} disabled={busy}>Refresh</button>
          <button type="button" onClick={() => { setAllocationOpen(true); setAllocationError(""); setAllocationResult(""); }}>
            Allocate with decision…
          </button>
        </div>
        {previewPublic && <p className="muted small">Anonymous view: approved shelters only, without contact details.</p>}
        <div className="grid">
          {rows.length === 0 ? <p className="muted">No shelters returned.</p> : rows.map((shelter) => {
            const percentage = shelter.capacity > 0 ? Math.min(100, Math.round((shelter.occupancy / shelter.capacity) * 100)) : 0;
            return (
              <article className="panel" key={shelter.id}>
                <h3>{shelter.name}</h3>
                <p>{shelter.occupancy} / {shelter.capacity} occupied · {shelter.freePlaces} free places</p>
                <progress value={shelter.occupancy} max={shelter.capacity} aria-label={`${shelter.name} occupancy`} />
                <p className="small muted">Occupancy {percentage}% · {shelter.approved ? "Approved" : "Pending approval"}</p>
                {!previewPublic && (
                  <div className="inline">
                    <button type="button" onClick={() => startEdit(shelter)} disabled={busy}>Edit</button>
                    {!shelter.approved && <button type="button" onClick={() => void approve(shelter.id)} disabled={busy}>Approve</button>}
                  </div>
                )}
              </article>
            );
          })}
        </div>
      </section>

      {!previewPublic && (
        <section className="panel" aria-labelledby="shelter-form-heading">
          <h2 id="shelter-form-heading">{editingId ? "Edit shelter" : "Create shelter"}</h2>
          <div className="form-grid">
            <label htmlFor="shelter-name">Name</label>
            <input id="shelter-name" value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} />
            <label htmlFor="shelter-capacity">Capacity</label>
            <input id="shelter-capacity" type="number" min={1} value={form.capacity} onChange={(event) => setForm({ ...form, capacity: event.target.value })} />
            <label htmlFor="shelter-facilities">Facilities (JSON array)</label>
            <input id="shelter-facilities" value={form.facilities} onChange={(event) => setForm({ ...form, facilities: event.target.value })} />
            <label htmlFor="shelter-access">Accessibility features (JSON array)</label>
            <input id="shelter-access" value={form.accessibilityFeatures} onChange={(event) => setForm({ ...form, accessibilityFeatures: event.target.value })} />
            <label htmlFor="shelter-services">Services (JSON array)</label>
            <input id="shelter-services" value={form.services} onChange={(event) => setForm({ ...form, services: event.target.value })} />
            {!editingId && (
              <>
                <label htmlFor="shelter-org">Organisation ID</label>
                <input id="shelter-org" value={form.orgId} onChange={(event) => setForm({ ...form, orgId: event.target.value })} />
              </>
            )}
          </div>
          <MapPicker
            height={260}
            value={{ lat: Number(form.lat), lng: Number(form.lng) }}
            onChange={(point) => setForm({ ...form, lat: String(point.lat), lng: String(point.lng) })}
          />
          {capacityHint && <p className="muted small">{capacityHint}</p>}
          <div className="inline">
            <button type="button" onClick={() => void saveShelter()} disabled={busy || !form.name.trim()}>
              {busy ? "Saving…" : editingId ? "Save changes" : "Create shelter"}
            </button>
            {editingId && <button type="button" className="secondary" onClick={resetForm}>Cancel edit</button>}
          </div>
          <p className="small muted">If capacity is below current occupancy the server returns SHELTER_CAPACITY_BELOW_OCCUPANCY (409); no local-only guard replaces that check.</p>
        </section>
      )}

      {allocationOpen && (
        <div className="dialog-backdrop" role="presentation">
          <section className="dialog panel" role="dialog" aria-modal="true" aria-labelledby="shelter-allocate-title">
            <h2 id="shelter-allocate-title">Allocate shelter places</h2>
            <p className="muted">Household size and shelter identity are re-read from the approved decision payload on the server.</p>
            <label htmlFor="shelter-decision">Approved decision ID</label>
            <input id="shelter-decision" value={allocationDecision} onChange={(event) => setAllocationDecision(event.target.value)} />
            <label htmlFor="shelter-request">Request ID (optional)</label>
            <input id="shelter-request" value={allocationRequest} onChange={(event) => setAllocationRequest(event.target.value)} />
            <label htmlFor="shelter-household">Household size (optional)</label>
            <input id="shelter-household" type="number" min={1} value={allocationHousehold} onChange={(event) => setAllocationHousehold(event.target.value)} />
            {allocationError && <p className="error" role="alert">{allocationError}</p>}
            {allocationResult && <p className="success" role="status">{allocationResult}</p>}
            <div className="inline">
              <button type="button" onClick={() => void allocateShelter()} disabled={busy || !allocationDecision.trim()}>
                {busy ? "Allocating…" : "Allocate places"}
              </button>
              <button type="button" className="secondary" onClick={() => setAllocationOpen(false)} disabled={busy}>Close</button>
            </div>
            <p className="small muted">A full shelter returns SHELTER_FULL (409). Occupancy is never edited directly from this form.</p>
          </section>
        </div>
      )}
    </>
  );
}
