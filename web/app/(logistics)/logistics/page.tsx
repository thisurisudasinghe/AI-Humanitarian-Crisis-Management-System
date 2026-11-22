"use client";

// Member 4 / D4-11 logistics dashboard. The server owns every guard; this page
// only collects input, displays ProblemDetails and prevents double submits.

import { useEffect, useState } from "react";
import { api, errorMessage, newIdempotencyKey, Paged } from "../../../lib/api";

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

export default function Logistics() {
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

  useEffect(() => {
    api<ResourceType[]>("/resources/types")
      .then(setTypes)
      .catch((error) => setLoadError(errorMessage(error)));
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
      await loadInventories();
    } catch (error) {
      // Keep the same key on failure: retry must be idempotent.
      setAllocationError(errorMessage(error));
    } finally {
      setSubmitting(false);
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
                <th>Stock status</th>
              </tr>
            </thead>
            <tbody>
              {inventories.items.length === 0 ? (
                <tr><td colSpan={8} className="muted">No inventories match these filters.</td></tr>
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
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
        <p className="muted small">{inventories.total} inventory rows · page {inventories.page} of {Math.max(1, Math.ceil(inventories.total / inventories.pageSize))}</p>
      </section>

      <section className="panel" aria-labelledby="match-heading">
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
      </section>

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

      {dialogOpen && (
        <div className="dialog-backdrop" role="presentation">
          <section className="dialog panel" role="dialog" aria-modal="true" aria-labelledby="allocate-title">
            <h2 id="allocate-title">Allocate stock</h2>
            <p className="muted">The inventory and quantity come from the decision payload, never from this form.</p>
            <label htmlFor="allocate-decision">Approved decision ID</label>
            <input id="allocate-decision" value={decisionId} onChange={(event) => setDecisionId(event.target.value)} placeholder="Decision GUID" />
            <p className="muted small">Idempotency-Key: <code>{idempotencyKey}</code></p>
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
