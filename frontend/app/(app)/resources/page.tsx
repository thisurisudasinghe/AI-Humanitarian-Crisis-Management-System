"use client";

import { AuthGuard } from "@/components/AuthGuard";
import { useAuth } from "@/lib/auth";

// Member 4 / D4-11 logistics dashboard. The server owns every guard; this page
// only collects input, displays ProblemDetails and prevents double submits.

import { useEffect, useState } from "react";
import dynamic from "next/dynamic";
import { api, errorMessage, newIdempotencyKey, Paged } from "@/lib/operations-api";

const MapPicker = dynamic(() => import("@/components/map/MapPicker"), { ssr: false });

interface ResourceType {
  id: string;
  name: string;
  unit: string;
  description?: string | null;
}
interface Inventory {
  id: string;
  resourceTypeId: string;
  orgId: string;
  quantity: number;
  available: number;
  reserved: number;
  allocated: number;
  dispatched: number;
  utilised: number;
  reorderThreshold: number;
  expiryDate?: string | null;
  lat?: number | null;
  lng?: number | null;
  accessibilityCapable: boolean;
  lowStock: boolean;
}
interface Allocation {
  id: string;
  requestId: string;
  inventoryId: string;
  quantity: number;
  status: string;
  decisionId: string;
  allocatedAt: string;
}
interface Dispatch {
  id: string;
  allocationId: string;
  decisionId: string;
  quantity: number;
  status: string;
  confirmedAt?: string | null;
}
interface PendingTransition {
  id: string;
  operation: string;
  entityId: string;
  requestId: string;
  targetStatus: string;
  attempts: number;
  lastError?: string | null;
  completedAt?: string | null;
}
interface MatchCandidate {
  targetId: string;
  score: number;
  rationale: string;
  isApproximate: boolean;
}
interface ShortageItem {
  requestId: string;
  category: string;
  urgency: string;
  requestedQuantity: number;
  availableAtStart: number;
  priorityPosition: number;
  proposedPartialFulfilment: number;
  explanation: string;
}
interface ShortageProposal {
  resourceTypeId: string;
  availableQuantity: number;
  items: ShortageItem[];
  hasSideEffects: boolean;
  humanApprovalRequired: boolean;
}

const EMPTY_PAGE: Paged<Inventory> = { items: [], total: 0, page: 1, pageSize: 50 };

function query(params: Record<string, string | number | boolean | undefined | null>): string {
  const search = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") search.set(key, String(value));
  });
  const text = search.toString();
  return text ? `?${text}` : "";
}

function LogisticsContent() {
  const { user } = useAuth();
  const roles = user?.roles ?? [];
  const canManageInventory = roles.includes("LogisticsManager") || roles.includes("SystemAdmin");
  const canDispatch = roles.includes("LogisticsManager");
  const canAllocate = roles.includes("LogisticsManager") || roles.includes("EmergencyCoordinator");
  const canMatchResources = canAllocate;
  const [types, setTypes] = useState<ResourceType[]>([]);
  const [inventories, setInventories] = useState<Paged<Inventory>>(EMPTY_PAGE);
  const [orgId, setOrgId] = useState("");
  const [typeId, setTypeId] = useState("");
  const [lowStock, setLowStock] = useState("");
  const [loadError, setLoadError] = useState("");
  const [loading, setLoading] = useState(false);

  const [matchRequest, setMatchRequest] = useState("");
  const [matches, setMatches] = useState<MatchCandidate[] | null>(null);
  const [matchError, setMatchError] = useState("");

  const [shortageType, setShortageType] = useState("");
  const [proposal, setProposal] = useState<ShortageProposal | null>(null);
  const [shortageError, setShortageError] = useState("");

  const [dialogOpen, setDialogOpen] = useState(false);
  const [decisionId, setDecisionId] = useState("");
  const [idempotencyKey, setIdempotencyKey] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [allocationResult, setAllocationResult] = useState("");
  const [allocationError, setAllocationError] = useState("");
  const [allocations, setAllocations] = useState<Paged<Allocation>>({ items: [], total: 0, page: 1, pageSize: 50 });
  const [dispatches, setDispatches] = useState<Paged<Dispatch>>({ items: [], total: 0, page: 1, pageSize: 50 });
  const [pendingTransitions, setPendingTransitions] = useState<PendingTransition[]>([]);
  const [lifecycleError, setLifecycleError] = useState("");
  const [lifecycleBusy, setLifecycleBusy] = useState(false);
  const [workflowSubmitting, setWorkflowSubmitting] = useState(false);
  const [inventoryForm, setInventoryForm] = useState({ resourceTypeId: "", orgId: "", quantity: "", reorderThreshold: "0", expiryDate: "", lat: "7.8731", lng: "80.7718", accessibilityCapable: false });
  const [inventoryFormError, setInventoryFormError] = useState("");
  const [inventoryFormBusy, setInventoryFormBusy] = useState(false);
  const [editingInventory, setEditingInventory] = useState<Inventory | null>(null);
  const [correction, setCorrection] = useState({ quantity: "", reorderThreshold: "", expiryDate: "", lat: "", lng: "", accessibilityCapable: false });

  async function loadLifecycle() {
    setLifecycleBusy(true);
    setLifecycleError("");
    try {
      const [allocationRows, dispatchRows, transitions] = await Promise.all([
        api<Paged<Allocation>>("/allocations"),
        api<Paged<Dispatch>>("/dispatches"),
        api<PendingTransition[]>("/operations/status-transitions"),
      ]);
      setAllocations(allocationRows);
      setDispatches(dispatchRows);
      setPendingTransitions(transitions);
    } catch (error) {
      setLifecycleError(errorMessage(error));
    } finally {
      setLifecycleBusy(false);
    }
  }

  useEffect(() => {
    api<ResourceType[]>("/resources/types")
      .then(setTypes)
      .catch((error) => setLoadError(errorMessage(error)));
    void loadLifecycle();
  }, []);

  async function loadInventories() {
    setLoading(true);
    setLoadError("");
    try {
      setInventories(
        await api<Paged<Inventory>>(
          `/inventories${query({ orgId, resourceTypeId: typeId, lowStock: lowStock || undefined })}`,
        ),
      );
    } catch (error) {
      setLoadError(errorMessage(error));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadInventories();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [orgId, typeId, lowStock]);

  async function findMatches() {
    if (!matchRequest.trim()) return;
    setMatchError("");
    setMatches(null);
    try {
      setMatches(await api<MatchCandidate[]>(`/recommendations/match/resources/${matchRequest.trim()}`));
    } catch (error) {
      setMatchError(errorMessage(error));
    }
  }

  async function loadShortages() {
    if (!shortageType) return;
    setShortageError("");
    setProposal(null);
    try {
      setProposal(await api<ShortageProposal>(`/shortages/proposal${query({ resourceTypeId: shortageType })}`));
    } catch (error) {
      setShortageError(errorMessage(error));
    }
  }

  function openAllocation() {
    setDecisionId("");
    setIdempotencyKey(newIdempotencyKey());
    setAllocationResult("");
    setAllocationError("");
    setDialogOpen(true);
  }

  async function allocate() {
    if (!decisionId.trim() || submitting) return;
    setSubmitting(true);
    setAllocationError("");
    setAllocationResult("");
    // The key is created once per attempt and deliberately reused on retry so
    // a retry cannot double-decrement stock.
    const key = idempotencyKey || newIdempotencyKey();
    setIdempotencyKey(key);
    try {
      const result = await api<{ id: string; quantity: number; status: string }>("/allocations", {
        method: "POST",
        headers: { "Idempotency-Key": key },
        body: JSON.stringify({ decisionId: decisionId.trim() }),
      });
      setAllocationResult(`Allocation ${result.id} created (${result.quantity}, ${result.status}).`);
      await Promise.all([loadInventories(), loadLifecycle()]);
    } catch (error) {
      // Keep the same key on failure: retry must be idempotent.
      setAllocationError(errorMessage(error));
    } finally {
      setSubmitting(false);
    }
  }

  async function createInventory() {
    setInventoryFormError("");
    setInventoryFormBusy(true);
    try {
      await api("/inventories", {
        method: "POST",
        body: JSON.stringify({
          resourceTypeId: inventoryForm.resourceTypeId,
          orgId: inventoryForm.orgId,
          quantity: Number(inventoryForm.quantity),
          reorderThreshold: Number(inventoryForm.reorderThreshold),
          expiryDate: inventoryForm.expiryDate || null,
          lat: Number(inventoryForm.lat),
          lng: Number(inventoryForm.lng),
          accessibilityCapable: inventoryForm.accessibilityCapable,
        }),
      });
      setInventoryForm({ resourceTypeId: "", orgId: "", quantity: "", reorderThreshold: "0", expiryDate: "", lat: "7.8731", lng: "80.7718", accessibilityCapable: false });
      await loadInventories();
    } catch (error) {
      setInventoryFormError(errorMessage(error));
    } finally {
      setInventoryFormBusy(false);
    }
  }

  function startCorrection(row: Inventory) {
    setEditingInventory(row);
    setCorrection({ quantity: String(row.quantity), reorderThreshold: String(row.reorderThreshold), expiryDate: row.expiryDate?.slice(0, 10) ?? "", lat: String(row.lat ?? ""), lng: String(row.lng ?? ""), accessibilityCapable: row.accessibilityCapable });
    setInventoryFormError("");
  }

  async function saveCorrection() {
    if (!editingInventory || inventoryFormBusy) return;
    setInventoryFormBusy(true);
    setInventoryFormError("");
    try {
      await api(`/inventories/${editingInventory.id}`, {
        method: "PATCH",
        body: JSON.stringify({
          quantity: Number(correction.quantity),
          reorderThreshold: Number(correction.reorderThreshold),
          expiryDate: correction.expiryDate || null,
          lat: correction.lat ? Number(correction.lat) : null,
          lng: correction.lng ? Number(correction.lng) : null,
          accessibilityCapable: correction.accessibilityCapable,
        }),
      });
      setEditingInventory(null);
      await loadInventories();
    } catch (error) {
      setInventoryFormError(errorMessage(error));
    } finally {
      setInventoryFormBusy(false);
    }
  }

  async function dispatchAllocation(allocation: Allocation) {
    if (workflowSubmitting) return;
    setWorkflowSubmitting(true);
    setLifecycleError("");
    try {
      await api(`/dispatches/${allocation.id}`, {
        method: "POST",
        body: JSON.stringify({ decisionId: allocation.decisionId }),
      });
      await Promise.all([loadLifecycle(), loadInventories()]);
    } catch (error) {
      setLifecycleError(errorMessage(error));
    } finally {
      setWorkflowSubmitting(false);
    }
  }

  async function confirmDelivery(dispatch: Dispatch) {
    if (workflowSubmitting) return;
    setWorkflowSubmitting(true);
    setLifecycleError("");
    try {
      await api(`/dispatches/${dispatch.id}/confirm`, {
        method: "POST",
        body: JSON.stringify({ decisionId: dispatch.decisionId }),
      });
      await Promise.all([loadLifecycle(), loadInventories()]);
    } catch (error) {
      setLifecycleError(errorMessage(error));
    } finally {
      setWorkflowSubmitting(false);
    }
  }

  async function retryTransition(transitionId: string) {
    if (workflowSubmitting) return;
    setWorkflowSubmitting(true);
    setLifecycleError("");
    try {
      await api(`/operations/status-transitions/${transitionId}/retry`, { method: "POST" });
      await loadLifecycle();
    } catch (error) {
      setLifecycleError(errorMessage(error));
    } finally {
      setWorkflowSubmitting(false);
    }
  }

  const typeName = (id: string) => types.find((type) => type.id === id)?.name ?? id.slice(0, 8);

  return (
    <>
      <h1>Logistics dashboard</h1>
      <p className="muted">Inventory availability, match rationale and read-only shortage proposals.</p>

      {loadError && <p className="error" role="alert">{loadError}</p>}

      <section className="panel" aria-labelledby="inventory-heading">
        <h2 id="inventory-heading">Inventory</h2>
        <div className="filters">
          <label htmlFor="filter-org">Organisation ID</label>
          <input id="filter-org" value={orgId} onChange={(event) => setOrgId(event.target.value)} placeholder="Optional GUID" />
          <label htmlFor="filter-type">Resource type</label>
          <select id="filter-type" value={typeId} onChange={(event) => setTypeId(event.target.value)}>
            <option value="">All resource types</option>
            {types.map((type) => <option key={type.id} value={type.id}>{type.name}</option>)}
          </select>
          <label htmlFor="filter-low">Stock level</label>
          <select id="filter-low" value={lowStock} onChange={(event) => setLowStock(event.target.value)}>
            <option value="">All</option>
            <option value="true">Low stock only</option>
            <option value="false">Above reorder threshold</option>
          </select>
          <button type="button" onClick={() => void loadInventories()} disabled={loading}>
            {loading ? "Loading…" : "Refresh"}
          </button>
        </div>

        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Resource type</th>
                <th>Quantity</th>
                <th>Available</th>
                <th>Reserved</th>
                <th>Allocated</th>
                <th>Dispatched</th>
                <th>Utilised</th>
                <th>Stock status</th><th>Manage</th>
              </tr>
            </thead>
            <tbody>
              {inventories.items.length === 0 ? (
                <tr><td colSpan={9} className="muted">No inventories match these filters.</td></tr>
              ) : (
                inventories.items.map((row) => (
                  <tr key={row.id}>
                    <td>{typeName(row.resourceTypeId)}</td>
                    <td>{row.quantity}</td>
                    <td>{row.available}</td>
                    <td>{row.reserved}</td>
                    <td>{row.allocated}</td>
                    <td>{row.dispatched}</td>
                    <td>{row.utilised}</td>
                    <td>{row.lowStock ? <span className="badge badge-warn">Reorder required</span> : "Available"}</td>
                    <td>{canManageInventory ? <button type="button" className="secondary" onClick={() => startCorrection(row)}>Correct stock</button> : "—"}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
        <p className="muted small">{inventories.total} inventory rows · page {inventories.page} of {Math.max(1, Math.ceil(inventories.total / inventories.pageSize))}</p>
      </section>

      {canManageInventory && <section className="panel" aria-labelledby="inventory-create-heading">
        <h2 id="inventory-create-heading">Add inventory</h2>
        <p className="muted small">Stock creation and corrections are validated by the API and database. Available stock cannot be reduced below committed buckets.</p>
        {inventoryFormError && <p className="error" role="alert">{inventoryFormError}</p>}
        <div className="form-grid">
          <label htmlFor="new-inventory-type">Resource type</label>
          <select id="new-inventory-type" value={inventoryForm.resourceTypeId} onChange={(event) => setInventoryForm({ ...inventoryForm, resourceTypeId: event.target.value })}>
            <option value="">Select type</option>{types.map((type) => <option key={type.id} value={type.id}>{type.name}</option>)}
          </select>
          <label htmlFor="new-inventory-org">Organisation ID</label>
          <input id="new-inventory-org" value={inventoryForm.orgId} onChange={(event) => setInventoryForm({ ...inventoryForm, orgId: event.target.value })} />
          <label htmlFor="new-inventory-qty">Quantity</label>
          <input id="new-inventory-qty" type="number" min="0" step="any" value={inventoryForm.quantity} onChange={(event) => setInventoryForm({ ...inventoryForm, quantity: event.target.value })} />
          <label htmlFor="new-inventory-threshold">Reorder threshold</label>
          <input id="new-inventory-threshold" type="number" min="0" step="any" value={inventoryForm.reorderThreshold} onChange={(event) => setInventoryForm({ ...inventoryForm, reorderThreshold: event.target.value })} />
          <label htmlFor="new-inventory-expiry">Expiry date (optional)</label>
          <input id="new-inventory-expiry" type="date" value={inventoryForm.expiryDate} onChange={(event) => setInventoryForm({ ...inventoryForm, expiryDate: event.target.value })} />
          <label htmlFor="new-inventory-lat">Latitude</label>
          <input id="new-inventory-lat" type="number" step="any" value={inventoryForm.lat} onChange={(event) => setInventoryForm({ ...inventoryForm, lat: event.target.value })} />
          <label htmlFor="new-inventory-lng">Longitude</label>
          <input id="new-inventory-lng" type="number" step="any" value={inventoryForm.lng} onChange={(event) => setInventoryForm({ ...inventoryForm, lng: event.target.value })} />
          <label htmlFor="new-inventory-access">Accessibility capable</label>
          <input id="new-inventory-access" type="checkbox" checked={inventoryForm.accessibilityCapable} onChange={(event) => setInventoryForm({ ...inventoryForm, accessibilityCapable: event.target.checked })} />
        </div>
        <MapPicker height={240} value={{ lat: Number(inventoryForm.lat), lng: Number(inventoryForm.lng) }} onChange={(point) => setInventoryForm({ ...inventoryForm, lat: String(point.lat), lng: String(point.lng) })} />
        <button type="button" onClick={() => void createInventory()} disabled={inventoryFormBusy || !inventoryForm.resourceTypeId || !inventoryForm.orgId.trim() || !inventoryForm.quantity.trim()}>{inventoryFormBusy ? "Saving…" : "Create inventory"}</button>
      </section>}

      {canManageInventory && editingInventory && (
        <section className="panel" aria-labelledby="inventory-correction-heading">
          <h2 id="inventory-correction-heading">Correct inventory {editingInventory.id.slice(0, 8)}</h2>
          <p className="muted small">The API rejects corrections that would make stock lower than reserved, allocated or dispatched quantities.</p>
          <div className="form-grid">
            <label htmlFor="correction-quantity">Total quantity</label>
            <input id="correction-quantity" type="number" min="0" step="any" value={correction.quantity} onChange={(event) => setCorrection({ ...correction, quantity: event.target.value })} />
            <label htmlFor="correction-threshold">Reorder threshold</label>
            <input id="correction-threshold" type="number" min="0" step="any" value={correction.reorderThreshold} onChange={(event) => setCorrection({ ...correction, reorderThreshold: event.target.value })} />
            <label htmlFor="correction-expiry">Expiry date</label>
            <input id="correction-expiry" type="date" value={correction.expiryDate} onChange={(event) => setCorrection({ ...correction, expiryDate: event.target.value })} />
            <label htmlFor="correction-lat">Latitude</label>
            <input id="correction-lat" type="number" step="any" value={correction.lat} onChange={(event) => setCorrection({ ...correction, lat: event.target.value })} />
            <label htmlFor="correction-lng">Longitude</label>
            <input id="correction-lng" type="number" step="any" value={correction.lng} onChange={(event) => setCorrection({ ...correction, lng: event.target.value })} />
            <label htmlFor="correction-access">Accessibility capable</label>
            <input id="correction-access" type="checkbox" checked={correction.accessibilityCapable} onChange={(event) => setCorrection({ ...correction, accessibilityCapable: event.target.checked })} />
          </div>
          <div className="inline"><button type="button" onClick={() => void saveCorrection()} disabled={inventoryFormBusy}>{inventoryFormBusy ? "Saving…" : "Save correction"}</button><button type="button" className="secondary" onClick={() => setEditingInventory(null)} disabled={inventoryFormBusy}>Cancel</button></div>
        </section>
      )}

      <section className="panel" aria-labelledby="lifecycle-heading">
        <div className="inline" style={{ justifyContent: "space-between" }}>
          <h2 id="lifecycle-heading">Allocation, dispatch &amp; delivery</h2>
          <button type="button" className="secondary" onClick={() => void loadLifecycle()} disabled={lifecycleBusy}>
            {lifecycleBusy ? "Refreshing…" : "Refresh workflow"}
          </button>
        </div>
        {lifecycleError && <p className="error" role="alert">{lifecycleError}</p>}
        <h3>Allocations</h3>
        <div className="table-wrap">
          <table>
            <thead><tr><th>Allocation</th><th>Request</th><th>Quantity</th><th>Status</th><th>Action</th></tr></thead>
            <tbody>
              {allocations.items.length === 0 ? <tr><td colSpan={5} className="muted">No allocations recorded yet.</td></tr> : allocations.items.map((allocation) => (
                <tr key={allocation.id}>
                  <td><code>{allocation.id.slice(0, 8)}</code></td>
                  <td><code>{allocation.requestId.slice(0, 8)}</code></td>
                  <td>{allocation.quantity}</td>
                  <td>{allocation.status}</td>
                  <td>{canDispatch && allocation.status === "Allocated" ? <button type="button" onClick={() => void dispatchAllocation(allocation)} disabled={workflowSubmitting}>Dispatch</button> : "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <h3>Dispatches</h3>
        <div className="table-wrap">
          <table>
            <thead><tr><th>Dispatch</th><th>Allocation</th><th>Quantity</th><th>Status</th><th>Confirmed</th><th>Action</th></tr></thead>
            <tbody>
              {dispatches.items.length === 0 ? <tr><td colSpan={6} className="muted">No dispatches recorded yet.</td></tr> : dispatches.items.map((dispatch) => (
                <tr key={dispatch.id}>
                  <td><code>{dispatch.id.slice(0, 8)}</code></td>
                  <td><code>{dispatch.allocationId.slice(0, 8)}</code></td>
                  <td>{dispatch.quantity}</td>
                  <td>{dispatch.status}</td>
                  <td>{dispatch.confirmedAt ? new Date(dispatch.confirmedAt).toLocaleString() : "—"}</td>
                  <td>{canDispatch && dispatch.status === "Dispatched" ? <button type="button" onClick={() => void confirmDelivery(dispatch)} disabled={workflowSubmitting}>Confirm delivery</button> : "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <p className="muted small">Dispatch and delivery confirmation re-use the approved decision linked to each allocation. Inventory buckets are updated by the API, not by this page.</p>
        <h3>Recover failed request-status transitions</h3>
        {pendingTransitions.length === 0 ? <p className="muted">No pending status transitions.</p> : <div className="table-wrap"><table><thead><tr><th>Operation</th><th>Request</th><th>Target status</th><th>Attempts</th><th>Last error</th><th>Action</th></tr></thead><tbody>{pendingTransitions.map((transition) => <tr key={transition.id}><td>{transition.operation}</td><td><code>{transition.requestId.slice(0, 8)}</code></td><td>{transition.targetStatus}</td><td>{transition.attempts}</td><td className="small">{transition.lastError ?? "—"}</td><td><button type="button" onClick={() => void retryTransition(transition.id)} disabled={workflowSubmitting}>Retry</button></td></tr>)}</tbody></table></div>}
      </section>

      {canMatchResources && <section className="panel" aria-labelledby="match-heading">
        <h2 id="match-heading">Resource matches</h2>
        <p className="muted">Read-only ranking from the approved matching strategy. Matching never reserves stock.</p>
        <div className="inline">
          <label htmlFor="match-request">Request ID</label>
          <input id="match-request" value={matchRequest} onChange={(event) => setMatchRequest(event.target.value)} placeholder="Request GUID" />
          <button type="button" onClick={() => void findMatches()} disabled={!matchRequest.trim()}>Find matches</button>
        </div>
        {matchError && <p className="error" role="alert">{matchError}</p>}
        {matches && (matches.length === 0 ? <p className="muted">No eligible inventories.</p> : (
          <ol className="match-list">
            {matches.map((candidate) => (
              <li key={candidate.targetId}>
                <strong>{candidate.targetId.slice(0, 8)}</strong>
                <span className="badge">{Math.round(candidate.score * 100)}%</span>
                {candidate.isApproximate && <span className="badge badge-warn">approximate distance</span>}
                <p className="small">{candidate.rationale}</p>
              </li>
            ))}
          </ol>
        ))}
        <button type="button" onClick={openAllocation}>Allocate with approved decision…</button>
      </section>}

      <section className="panel" aria-labelledby="shortage-heading">
        <h2 id="shortage-heading">Shortage proposal</h2>
        <p className="muted">Ranked, read-only proposal. Nothing is allocated or reserved by this view.</p>
        <div className="inline">
          <label htmlFor="shortage-type">Resource type</label>
          <select id="shortage-type" value={shortageType} onChange={(event) => setShortageType(event.target.value)}>
            <option value="">Select a resource type</option>
            {types.map((type) => <option key={type.id} value={type.id}>{type.name}</option>)}
          </select>
          <button type="button" onClick={() => void loadShortages()} disabled={!shortageType}>Show proposal</button>
        </div>
        {shortageError && <p className="error" role="alert">{shortageError}</p>}
        {proposal && (
          <>
            <p><strong>Available at start:</strong> {proposal.availableQuantity}</p>
            {proposal.humanApprovalRequired && <p className="badge badge-warn">Human approval required — every allocation needs its own approved decision.</p>}
            {proposal.items.length === 0 ? <p className="muted">No competing open requests.</p> : (
              <table>
                <thead><tr><th>#</th><th>Request</th><th>Urgency</th><th>Requested</th><th>Available</th><th>Proposed</th><th>Explanation</th></tr></thead>
                <tbody>
                  {proposal.items.map((item) => (
                    <tr key={item.requestId}>
                      <td>{item.priorityPosition}</td>
                      <td>{item.requestId.slice(0, 8)}</td>
                      <td>{item.urgency}</td>
                      <td>{item.requestedQuantity}</td>
                      <td>{item.availableAtStart}</td>
                      <td>{item.proposedPartialFulfilment}</td>
                      <td className="small">{item.explanation}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        )}
      </section>

      {canAllocate && dialogOpen && (
        <div className="dialog-backdrop" role="presentation">
          <section className="dialog panel" role="dialog" aria-modal="true" aria-labelledby="allocate-title">
            <h2 id="allocate-title">Allocate stock</h2>
            <p className="muted">The inventory and quantity come from the decision payload, never from this form.</p>
            <label htmlFor="allocate-decision">Approved decision ID</label>
            <input id="allocate-decision" value={decisionId} onChange={(event) => setDecisionId(event.target.value)} placeholder="Decision GUID" />
            <p className="muted small">Idempotency-Key: <code>{idempotencyKey}</code></p>
            <p className="muted small">This key is reused if the request fails, so retrying cannot allocate twice. Generate a new key only when starting a different allocation attempt.</p>
            <button type="button" disabled={!decisionId.trim()} onClick={() => setIdempotencyKey(newIdempotencyKey())}>
              Generate a new key
            </button>
            {allocationError && <p className="error" role="alert">{allocationError}</p>}
            {allocationResult && <p className="success" role="status">{allocationResult}</p>}
            <div className="inline">
              <button type="button" onClick={() => void allocate()} disabled={submitting || !decisionId.trim()}>
                {submitting ? "Allocating…" : "Allocate"}
              </button>
              <button type="button" className="secondary" onClick={() => setDialogOpen(false)} disabled={submitting}>Close</button>
            </div>
          </section>
        </div>
      )}
    </>
  );
}


export default function LogisticsPage() {
  return (
    <AuthGuard allowedRoles={["LogisticsManager", "EmergencyCoordinator", "SystemAdmin"]}>
      <div className="dev4-operations">
        <LogisticsContent />
      </div>
    </AuthGuard>
  );
}
