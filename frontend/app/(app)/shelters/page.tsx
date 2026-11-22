"use client";

import { AuthGuard } from "@/components/AuthGuard";
import { useAuth } from "@/lib/auth";
import { RoleNames } from "@/lib/roles";

// Member 4 / D4-11 shelter admin. Capacity and occupancy guards are enforced by
// the SQL CHECK constraint and the guarded UPDATE; messages here only explain
// the server's ProblemDetails response.

import dynamic from "next/dynamic";
import { useEffect, useState } from "react";
import { api, errorMessage, Paged } from "@/lib/operations-api";

const MapPicker = dynamic(() => import("@/components/map/MapPicker"), { ssr: false });

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

interface ResourceType { id: string; name: string; unit: string; }
interface AssistanceRequest { id: string; fromOrgId: string; resourceTypeId: string; quantity: number; reason: string; status: string; shelterId?: string | null; createdAt: string; }
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

function SheltersContent() {
  const { user } = useAuth();
  const roles = user?.roles ?? [];
  const canManageShelters = roles.includes(RoleNames.ShelterAdmin) || roles.includes(RoleNames.SystemAdmin);
  const canApproveShelters = roles.includes(RoleNames.EmergencyCoordinator) || roles.includes(RoleNames.SystemAdmin);
  const canAllocateShelter = roles.includes(RoleNames.ShelterAdmin) || roles.includes(RoleNames.EmergencyCoordinator);
  const canMatchShelters = roles.includes(RoleNames.ShelterAdmin) || roles.includes(RoleNames.EmergencyCoordinator);
  const canRequestAssistance = roles.includes(RoleNames.ShelterAdmin) || roles.includes(RoleNames.SystemAdmin);
  const [shelters, setShelters] = useState<Paged<Shelter>>({ items: [], total: 0, page: 1, pageSize: 50 });
  const [publicShelters, setPublicShelters] = useState<Shelter[]>([]);
  const [resourceTypes, setResourceTypes] = useState<ResourceType[]>([]);
  const [assistanceRequests, setAssistanceRequests] = useState<Paged<AssistanceRequest>>({ items: [], total: 0, page: 1, pageSize: 50 });
  const [assistanceShelterId, setAssistanceShelterId] = useState("");
  const [assistanceTypeId, setAssistanceTypeId] = useState("");
  const [assistanceQuantity, setAssistanceQuantity] = useState("1");
  const [assistanceReason, setAssistanceReason] = useState("");
  const [assistanceError, setAssistanceError] = useState("");
  const [matchRequestId, setMatchRequestId] = useState("");
  const [shelterMatches, setShelterMatches] = useState<Array<{ targetId: string; score: number; rationale: string; isApproximate: boolean }> | null>(null);
  const [matchError, setMatchError] = useState("");
  const [previewPublic, setPreviewPublic] = useState(false);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  const [form, setForm] = useState<FormState>(EMPTY_FORM);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [formError, setFormError] = useState("");
  const [capacityHint, setCapacityHint] = useState("");

  const [allocationOpen, setAllocationOpen] = useState(false);
  const [allocationDecision, setAllocationDecision] = useState("");
  const [allocationError, setAllocationError] = useState("");
  const [allocationResult, setAllocationResult] = useState("");

  async function load() {
    setBusy(true);
    setError("");
    try {
      const [staff, publicRows, types, requests] = await Promise.all([
        api<Paged<Shelter>>("/shelters"),
        api<Shelter[]>("/shelters/public"),
        api<ResourceType[]>("/resources/types"),
        canRequestAssistance
          ? api<Paged<AssistanceRequest>>("/assistance-requests?direction=out")
          : Promise.resolve({ items: [], total: 0, page: 1, pageSize: 50 } as Paged<AssistanceRequest>),
      ]);
      setShelters(staff);
      setPublicShelters(publicRows);
      setResourceTypes(types);
      setAssistanceRequests(requests);
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
        body: JSON.stringify({ decisionId: allocationDecision.trim() }),
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

  async function findShelterMatches() {
    if (!matchRequestId.trim()) return;
    setBusy(true);
    setMatchError("");
    try {
      const result = await api<Array<{ targetId: string; score: number; rationale: string; isApproximate: boolean }>>(`/recommendations/match/shelters/${matchRequestId.trim()}`);
      setShelterMatches(result);
    } catch (matchRequestError) {
      setMatchError(errorMessage(matchRequestError));
    } finally {
      setBusy(false);
    }
  }

  async function createShelterAssistanceRequest() {
    const shelter = shelters.items.find((row) => row.id === assistanceShelterId);
    if (!shelter || !assistanceTypeId || !assistanceReason.trim() || Number(assistanceQuantity) <= 0) return;
    setBusy(true);
    setAssistanceError("");
    try {
      await api("/assistance-requests", {
        method: "POST",
        body: JSON.stringify({
          fromOrgId: shelter.orgId,
          toOrgId: null,
          resourceTypeId: assistanceTypeId,
          quantity: Number(assistanceQuantity),
          reason: assistanceReason.trim(),
          shelterId: shelter.id,
        }),
      });
      setAssistanceReason("");
      await load();
    } catch (requestError) {
      setAssistanceError(errorMessage(requestError));
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
          {canAllocateShelter && <button type="button" onClick={() => { setAllocationOpen(true); setAllocationError(""); setAllocationResult(""); }}>
            Allocate with decision…
          </button>}
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
                    {canManageShelters && <button type="button" onClick={() => startEdit(shelter)} disabled={busy}>Edit</button>}
                    {canApproveShelters && !shelter.approved && <button type="button" onClick={() => void approve(shelter.id)} disabled={busy}>Approve</button>}
                  </div>
                )}
              </article>
            );
          })}
        </div>
      </section>

      {!previewPublic && canManageShelters && (
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

      {canMatchShelters && <section className="panel" aria-labelledby="shelter-match-heading">
        <h2 id="shelter-match-heading">Shelter recommendations</h2>
        <p className="muted small">Read-only suitability ranking. Capacity is re-checked by the server when an approved shelter decision is allocated.</p>
        <div className="inline"><label htmlFor="shelter-match-request">Request ID</label><input id="shelter-match-request" value={matchRequestId} onChange={(event) => setMatchRequestId(event.target.value)} /><button type="button" onClick={() => void findShelterMatches()} disabled={busy || !matchRequestId.trim()}>Find shelters</button></div>
        {matchError && <p className="error" role="alert">{matchError}</p>}
        {shelterMatches && (shelterMatches.length === 0 ? <p className="muted">No suitable shelters found.</p> : <ol className="match-list">{shelterMatches.map((match) => <li key={match.targetId}><strong>{shelters.items.find((row) => row.id === match.targetId)?.name ?? match.targetId.slice(0, 8)}</strong> <span className="badge">{Math.round(match.score * 100)}%</span> {match.isApproximate && <span className="badge badge-warn">approximate</span>}<p className="small">{match.rationale}</p></li>)}</ol>)}
      </section>}

      {canRequestAssistance && <section className="panel" aria-labelledby="shelter-assistance-heading">
        <h2 id="shelter-assistance-heading">Request additional resources</h2>
        <p className="muted small">Creates a P1 assistance request linked to the selected shelter. The request is recorded for logistics coordination; no stock is allocated automatically.</p>
        {assistanceError && <p className="error" role="alert">{assistanceError}</p>}
        <div className="form-grid">
          <label htmlFor="assistance-shelter">Shelter</label>
          <select id="assistance-shelter" value={assistanceShelterId} onChange={(event) => setAssistanceShelterId(event.target.value)}><option value="">Select shelter</option>{shelters.items.map((shelter) => <option key={shelter.id} value={shelter.id}>{shelter.name}</option>)}</select>
          <label htmlFor="assistance-resource-type">Resource type</label>
          <select id="assistance-resource-type" value={assistanceTypeId} onChange={(event) => setAssistanceTypeId(event.target.value)}><option value="">Select resource type</option>{resourceTypes.map((type) => <option key={type.id} value={type.id}>{type.name} ({type.unit})</option>)}</select>
          <label htmlFor="assistance-quantity">Quantity</label>
          <input id="assistance-quantity" type="number" min="0.01" step="any" value={assistanceQuantity} onChange={(event) => setAssistanceQuantity(event.target.value)} />
          <label htmlFor="assistance-reason">Reason</label>
          <textarea id="assistance-reason" value={assistanceReason} onChange={(event) => setAssistanceReason(event.target.value)} rows={3} />
        </div>
        <button type="button" onClick={() => void createShelterAssistanceRequest()} disabled={busy || !assistanceShelterId || !assistanceTypeId || !assistanceReason.trim() || Number(assistanceQuantity) <= 0}>Send resource request</button>
        <h3>Outgoing assistance requests</h3>
        {assistanceRequests.items.length === 0 ? <p className="muted">No assistance requests from this organisation yet.</p> : <div className="table-wrap"><table><thead><tr><th>Shelter</th><th>Resource type</th><th>Quantity</th><th>Status</th><th>Reason</th></tr></thead><tbody>{assistanceRequests.items.map((request) => <tr key={request.id}><td>{shelters.items.find((shelter) => shelter.id === request.shelterId)?.name ?? request.shelterId?.slice(0, 8) ?? "—"}</td><td>{resourceTypes.find((type) => type.id === request.resourceTypeId)?.name ?? request.resourceTypeId.slice(0, 8)}</td><td>{request.quantity}</td><td>{request.status}</td><td>{request.reason}</td></tr>)}</tbody></table></div>}
      </section>}

      {canAllocateShelter && allocationOpen && (
        <div className="dialog-backdrop" role="presentation">
          <section className="dialog panel" role="dialog" aria-modal="true" aria-labelledby="shelter-allocate-title">
            <h2 id="shelter-allocate-title">Allocate shelter places</h2>
            <p className="muted">Household size and shelter identity are re-read from the approved decision payload on the server.</p>
            <label htmlFor="shelter-decision">Approved decision ID</label>
            <input id="shelter-decision" value={allocationDecision} onChange={(event) => setAllocationDecision(event.target.value)} />
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


export default function SheltersPage() {
  return (
    <AuthGuard allowedRoles={[RoleNames.ShelterAdmin, RoleNames.EmergencyCoordinator, RoleNames.SystemAdmin]}>
      <div className="dev4-operations">
        <SheltersContent />
      </div>
    </AuthGuard>
  );
}
