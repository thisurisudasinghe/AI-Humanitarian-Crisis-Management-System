"use client";

import { AuthGuard } from "@/components/AuthGuard";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useAuth } from "@/lib/auth";
import { formatDate, useI18n } from "@/lib/i18n";
import { RoleNames } from "@/lib/roles";

// Member 4 / D4-11 logistics dashboard. The server owns every guard; this page
// only collects input, displays ProblemDetails and prevents double submits.

import { useEffect, useState } from "react";
import dynamic from "next/dynamic";
import { api, newIdempotencyKey, Paged } from "@/lib/operations-api";

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
  const { t, locale } = useI18n();
  const roles = user?.roles ?? [];
  const canManageInventory = roles.includes(RoleNames.LogisticsManager) || roles.includes(RoleNames.SystemAdmin);
  const canDispatch = roles.includes(RoleNames.LogisticsManager);
  const canAllocate = roles.includes(RoleNames.LogisticsManager) || roles.includes(RoleNames.EmergencyCoordinator);
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
      setLifecycleError(localizedErrorMessage(error, t));
    } finally {
      setLifecycleBusy(false);
    }
  }

  useEffect(() => {
    api<ResourceType[]>("/resources/types")
      .then(setTypes)
      .catch((error) => setLoadError(localizedErrorMessage(error, t)));
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
      setLoadError(localizedErrorMessage(error, t));
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
      setMatchError(localizedErrorMessage(error, t));
    }
  }

  async function loadShortages() {
    if (!shortageType) return;
    setShortageError("");
    setProposal(null);
    try {
      setProposal(await api<ShortageProposal>(`/shortages/proposal${query({ resourceTypeId: shortageType })}`));
    } catch (error) {
      setShortageError(localizedErrorMessage(error, t));
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
      setAllocationResult(t("resources.allocationResult", { id: result.id, quantity: result.quantity, status: result.status }));
      await Promise.all([loadInventories(), loadLifecycle()]);
    } catch (error) {
      // Keep the same key on failure: retry must be idempotent.
      setAllocationError(localizedErrorMessage(error, t));
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
      setInventoryFormError(localizedErrorMessage(error, t));
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
      setInventoryFormError(localizedErrorMessage(error, t));
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
      setLifecycleError(localizedErrorMessage(error, t));
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
      setLifecycleError(localizedErrorMessage(error, t));
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
      setLifecycleError(localizedErrorMessage(error, t));
    } finally {
      setWorkflowSubmitting(false);
    }
  }

  const typeName = (id: string) => types.find((type) => type.id === id)?.name ?? id.slice(0, 8);

  return (
    <>
      <h1>{t("resources.title")}</h1>
      <p className="muted">{t("resources.subtitle")}</p>

      {loadError && <p className="error" role="alert">{loadError}</p>}

      <section className="panel" aria-labelledby="inventory-heading">
        <h2 id="inventory-heading">{t("resources.inventory")}</h2>
        <div className="filters">
          <label htmlFor="filter-org">{t("resources.organisationId")}</label>
          <input id="filter-org" value={orgId} onChange={(event) => setOrgId(event.target.value)} placeholder={t("resources.optionalGuid")} />
          <label htmlFor="filter-type">{t("resources.resourceType")}</label>
          <select id="filter-type" value={typeId} onChange={(event) => setTypeId(event.target.value)}>
            <option value="">{t("resources.allResourceTypes")}</option>
            {types.map((type) => <option key={type.id} value={type.id}>{type.name}</option>)}
          </select>
          <label htmlFor="filter-low">{t("resources.stockLevel")}</label>
          <select id="filter-low" value={lowStock} onChange={(event) => setLowStock(event.target.value)}>
            <option value="">{t("resources.all")}</option>
            <option value="true">{t("resources.lowStockOnly")}</option>
            <option value="false">{t("resources.aboveReorderThreshold")}</option>
          </select>
          <button type="button" onClick={() => void loadInventories()} disabled={loading}>
            {loading ? t("resources.loading") : t("resources.refresh")}
          </button>
        </div>

        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>{t("resources.resourceType")}</th>
                <th>{t("resources.quantity")}</th>
                <th>{t("resources.available")}</th>
                <th>{t("resources.reserved")}</th>
                <th>{t("resources.allocated")}</th>
                <th>{t("resources.dispatched")}</th>
                <th>{t("resources.utilised")}</th>
                <th>{t("resources.stockStatus")}</th><th>{t("resources.manage")}</th>
              </tr>
            </thead>
            <tbody>
              {inventories.items.length === 0 ? (
                <tr><td colSpan={9} className="muted">{t("resources.noInventories")}</td></tr>
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
                    <td>{row.lowStock ? <span className="badge badge-warn">{t("resources.reorderRequired")}</span> : t("resources.available")}</td>
                    <td>{canManageInventory ? <button type="button" className="secondary" onClick={() => startCorrection(row)}>{t("resources.correctStock")}</button> : "—"}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
        <p className="muted small">{t("resources.inventoryRows", { total: inventories.total, page: inventories.page, pages: Math.max(1, Math.ceil(inventories.total / inventories.pageSize)) })}</p>
      </section>

      {canManageInventory && <section className="panel" aria-labelledby="inventory-create-heading">
        <h2 id="inventory-create-heading">{t("resources.addInventory")}</h2>
        <p className="muted small">{t("resources.addInventoryHint")}</p>
        {inventoryFormError && <p className="error" role="alert">{inventoryFormError}</p>}
        <div className="form-grid">
          <label htmlFor="new-inventory-type">{t("resources.resourceType")}</label>
          <select id="new-inventory-type" value={inventoryForm.resourceTypeId} onChange={(event) => setInventoryForm({ ...inventoryForm, resourceTypeId: event.target.value })}>
            <option value="">{t("resources.selectType")}</option>{types.map((type) => <option key={type.id} value={type.id}>{type.name}</option>)}
          </select>
          <label htmlFor="new-inventory-org">{t("resources.organisationId")}</label>
          <input id="new-inventory-org" value={inventoryForm.orgId} onChange={(event) => setInventoryForm({ ...inventoryForm, orgId: event.target.value })} />
          <label htmlFor="new-inventory-qty">{t("resources.quantity")}</label>
          <input id="new-inventory-qty" type="number" min="0" step="any" value={inventoryForm.quantity} onChange={(event) => setInventoryForm({ ...inventoryForm, quantity: event.target.value })} />
          <label htmlFor="new-inventory-threshold">{t("resources.reorderThreshold")}</label>
          <input id="new-inventory-threshold" type="number" min="0" step="any" value={inventoryForm.reorderThreshold} onChange={(event) => setInventoryForm({ ...inventoryForm, reorderThreshold: event.target.value })} />
          <label htmlFor="new-inventory-expiry">{t("resources.expiryDateOptional")}</label>
          <input id="new-inventory-expiry" type="date" value={inventoryForm.expiryDate} onChange={(event) => setInventoryForm({ ...inventoryForm, expiryDate: event.target.value })} />
          <label htmlFor="new-inventory-lat">{t("resources.latitude")}</label>
          <input id="new-inventory-lat" type="number" step="any" value={inventoryForm.lat} onChange={(event) => setInventoryForm({ ...inventoryForm, lat: event.target.value })} />
          <label htmlFor="new-inventory-lng">{t("resources.longitude")}</label>
          <input id="new-inventory-lng" type="number" step="any" value={inventoryForm.lng} onChange={(event) => setInventoryForm({ ...inventoryForm, lng: event.target.value })} />
          <label htmlFor="new-inventory-access">{t("resources.accessibilityCapable")}</label>
          <input id="new-inventory-access" type="checkbox" checked={inventoryForm.accessibilityCapable} onChange={(event) => setInventoryForm({ ...inventoryForm, accessibilityCapable: event.target.checked })} />
        </div>
        <MapPicker height={240} value={{ lat: Number(inventoryForm.lat), lng: Number(inventoryForm.lng) }} onChange={(point) => setInventoryForm({ ...inventoryForm, lat: String(point.lat), lng: String(point.lng) })} />
        <button type="button" onClick={() => void createInventory()} disabled={inventoryFormBusy || !inventoryForm.resourceTypeId || !inventoryForm.orgId.trim() || !inventoryForm.quantity.trim()}>{inventoryFormBusy ? t("resources.saving") : t("resources.createInventory")}</button>
      </section>}

      {canManageInventory && editingInventory && (
        <section className="panel" aria-labelledby="inventory-correction-heading">
          <h2 id="inventory-correction-heading">{t("resources.correctInventory", { id: editingInventory.id.slice(0, 8) })}</h2>
          <p className="muted small">{t("resources.correctionHint")}</p>
          <div className="form-grid">
            <label htmlFor="correction-quantity">{t("resources.totalQuantity")}</label>
            <input id="correction-quantity" type="number" min="0" step="any" value={correction.quantity} onChange={(event) => setCorrection({ ...correction, quantity: event.target.value })} />
            <label htmlFor="correction-threshold">{t("resources.reorderThreshold")}</label>
            <input id="correction-threshold" type="number" min="0" step="any" value={correction.reorderThreshold} onChange={(event) => setCorrection({ ...correction, reorderThreshold: event.target.value })} />
            <label htmlFor="correction-expiry">{t("resources.expiryDate")}</label>
            <input id="correction-expiry" type="date" value={correction.expiryDate} onChange={(event) => setCorrection({ ...correction, expiryDate: event.target.value })} />
            <label htmlFor="correction-lat">{t("resources.latitude")}</label>
            <input id="correction-lat" type="number" step="any" value={correction.lat} onChange={(event) => setCorrection({ ...correction, lat: event.target.value })} />
            <label htmlFor="correction-lng">{t("resources.longitude")}</label>
            <input id="correction-lng" type="number" step="any" value={correction.lng} onChange={(event) => setCorrection({ ...correction, lng: event.target.value })} />
            <label htmlFor="correction-access">{t("resources.accessibilityCapable")}</label>
            <input id="correction-access" type="checkbox" checked={correction.accessibilityCapable} onChange={(event) => setCorrection({ ...correction, accessibilityCapable: event.target.checked })} />
          </div>
          <div className="inline"><button type="button" onClick={() => void saveCorrection()} disabled={inventoryFormBusy}>{inventoryFormBusy ? t("resources.saving") : t("resources.saveCorrection")}</button><button type="button" className="secondary" onClick={() => setEditingInventory(null)} disabled={inventoryFormBusy}>{t("resources.cancel")}</button></div>
        </section>
      )}

      <section className="panel" aria-labelledby="lifecycle-heading">
        <div className="inline" style={{ justifyContent: "space-between" }}>
          <h2 id="lifecycle-heading">{t("resources.lifecycleTitle")}</h2>
          <button type="button" className="secondary" onClick={() => void loadLifecycle()} disabled={lifecycleBusy}>
            {lifecycleBusy ? t("resources.refreshing") : t("resources.refreshWorkflow")}
          </button>
        </div>
        {lifecycleError && <p className="error" role="alert">{lifecycleError}</p>}
        <h3>{t("resources.allocations")}</h3>
        <div className="table-wrap">
          <table>
            <thead><tr><th>{t("resources.allocation")}</th><th>{t("resources.request")}</th><th>{t("resources.quantity")}</th><th>{t("resources.status")}</th><th>{t("resources.action")}</th></tr></thead>
            <tbody>
              {allocations.items.length === 0 ? <tr><td colSpan={5} className="muted">{t("resources.noAllocations")}</td></tr> : allocations.items.map((allocation) => (
                <tr key={allocation.id}>
                  <td><code>{allocation.id.slice(0, 8)}</code></td>
                  <td><code>{allocation.requestId.slice(0, 8)}</code></td>
                  <td>{allocation.quantity}</td>
                  <td>{allocation.status}</td>
                  <td>{canDispatch && allocation.status === "Allocated" ? <button type="button" onClick={() => void dispatchAllocation(allocation)} disabled={workflowSubmitting}>{t("resources.dispatch")}</button> : "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <h3>{t("resources.dispatches")}</h3>
        <div className="table-wrap">
          <table>
            <thead><tr><th>{t("resources.dispatch")}</th><th>{t("resources.allocation")}</th><th>{t("resources.quantity")}</th><th>{t("resources.status")}</th><th>{t("resources.confirmed")}</th><th>{t("resources.action")}</th></tr></thead>
            <tbody>
              {dispatches.items.length === 0 ? <tr><td colSpan={6} className="muted">{t("resources.noDispatches")}</td></tr> : dispatches.items.map((dispatch) => (
                <tr key={dispatch.id}>
                  <td><code>{dispatch.id.slice(0, 8)}</code></td>
                  <td><code>{dispatch.allocationId.slice(0, 8)}</code></td>
                  <td>{dispatch.quantity}</td>
                  <td>{dispatch.status}</td>
                  <td>{dispatch.confirmedAt ? formatDate(dispatch.confirmedAt, locale) : "—"}</td>
                  <td>{canDispatch && dispatch.status === "Dispatched" ? <button type="button" onClick={() => void confirmDelivery(dispatch)} disabled={workflowSubmitting}>{t("resources.confirmDelivery")}</button> : "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <p className="muted small">{t("resources.lifecycleHint")}</p>
        <h3>{t("resources.recoverTransitions")}</h3>
        {pendingTransitions.length === 0 ? <p className="muted">{t("resources.noPendingTransitions")}</p> : <div className="table-wrap"><table><thead><tr><th>{t("resources.operation")}</th><th>{t("resources.request")}</th><th>{t("resources.targetStatus")}</th><th>{t("resources.attempts")}</th><th>{t("resources.lastError")}</th><th>{t("resources.action")}</th></tr></thead><tbody>{pendingTransitions.map((transition) => <tr key={transition.id}><td>{transition.operation}</td><td><code>{transition.requestId.slice(0, 8)}</code></td><td>{transition.targetStatus}</td><td>{transition.attempts}</td><td className="small">{transition.lastError ?? "—"}</td><td><button type="button" onClick={() => void retryTransition(transition.id)} disabled={workflowSubmitting}>{t("resources.retry")}</button></td></tr>)}</tbody></table></div>}
      </section>

      {canMatchResources && <section className="panel" aria-labelledby="match-heading">
        <h2 id="match-heading">{t("resources.resourceMatches")}</h2>
        <p className="muted">{t("resources.matchHint")}</p>
        <div className="inline">
          <label htmlFor="match-request">{t("resources.requestId")}</label>
          <input id="match-request" value={matchRequest} onChange={(event) => setMatchRequest(event.target.value)} placeholder={t("resources.requestGuid")} />
          <button type="button" onClick={() => void findMatches()} disabled={!matchRequest.trim()}>{t("resources.findMatches")}</button>
        </div>
        {matchError && <p className="error" role="alert">{matchError}</p>}
        {matches && (matches.length === 0 ? <p className="muted">{t("resources.noEligibleInventories")}</p> : (
          <ol className="match-list">
            {matches.map((candidate) => (
              <li key={candidate.targetId}>
                <strong>{candidate.targetId.slice(0, 8)}</strong>
                <span className="badge">{Math.round(candidate.score * 100)}%</span>
                {candidate.isApproximate && <span className="badge badge-warn">{t("resources.approximateDistance")}</span>}
                <p className="small">{candidate.rationale}</p>
              </li>
            ))}
          </ol>
        ))}
        <button type="button" onClick={openAllocation}>{t("resources.allocateWithDecision")}</button>
      </section>}

      <section className="panel" aria-labelledby="shortage-heading">
        <h2 id="shortage-heading">{t("resources.shortageProposal")}</h2>
        <p className="muted">{t("resources.shortageHint")}</p>
        <div className="inline">
          <label htmlFor="shortage-type">{t("resources.resourceType")}</label>
          <select id="shortage-type" value={shortageType} onChange={(event) => setShortageType(event.target.value)}>
            <option value="">{t("resources.selectResourceType")}</option>
            {types.map((type) => <option key={type.id} value={type.id}>{type.name}</option>)}
          </select>
          <button type="button" onClick={() => void loadShortages()} disabled={!shortageType}>{t("resources.showProposal")}</button>
        </div>
        {shortageError && <p className="error" role="alert">{shortageError}</p>}
        {proposal && (
          <>
            <p><strong>{t("resources.availableAtStart")}</strong> {proposal.availableQuantity}</p>
            {proposal.humanApprovalRequired && <p className="badge badge-warn">{t("resources.humanApprovalRequired")}</p>}
            {proposal.items.length === 0 ? <p className="muted">{t("resources.noCompetingRequests")}</p> : (
              <table>
                <thead><tr><th>#</th><th>{t("resources.request")}</th><th>{t("resources.urgency")}</th><th>{t("resources.requested")}</th><th>{t("resources.available")}</th><th>{t("resources.proposed")}</th><th>{t("resources.explanation")}</th></tr></thead>
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
            <h2 id="allocate-title">{t("resources.allocateStock")}</h2>
            <p className="muted">{t("resources.allocateHint")}</p>
            <label htmlFor="allocate-decision">{t("resources.approvedDecisionId")}</label>
            <input id="allocate-decision" value={decisionId} onChange={(event) => setDecisionId(event.target.value)} placeholder={t("resources.decisionGuid")} />
            <p className="muted small">{t("resources.idempotencyKeyLabel")} <code>{idempotencyKey}</code></p>
            <p className="muted small">{t("resources.idempotencyHint")}</p>
            <button type="button" disabled={!decisionId.trim()} onClick={() => setIdempotencyKey(newIdempotencyKey())}>
              {t("resources.generateNewKey")}
            </button>
            {allocationError && <p className="error" role="alert">{allocationError}</p>}
            {allocationResult && <p className="success" role="status">{allocationResult}</p>}
            <div className="inline">
              <button type="button" onClick={() => void allocate()} disabled={submitting || !decisionId.trim()}>
                {submitting ? t("resources.allocating") : t("resources.allocate")}
              </button>
              <button type="button" className="secondary" onClick={() => setDialogOpen(false)} disabled={submitting}>{t("resources.close")}</button>
            </div>
          </section>
        </div>
      )}
    </>
  );
}


export default function LogisticsPage() {
  return (
    <AuthGuard allowedRoles={[RoleNames.LogisticsManager, RoleNames.EmergencyCoordinator, RoleNames.SystemAdmin]}>
      <div className="dev4-operations">
        <LogisticsContent />
      </div>
    </AuthGuard>
  );
}
