"use client";

import Link from "next/link";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import { Badge, Button, Input, Skeleton, Toast } from "@/components/ui";
import { formatDate } from "@/lib/admin";
import { errorMessage } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import {
  DecisionOutcomes,
  getRecommendationDecisions,
  recordDecision,
  type DecisionOutcome,
  type DecisionRecordDto
} from "@/lib/decisions";
import {
  getRecommendation,
  getRecommendations,
  type RecommendationDto
} from "@/lib/recommendations";
import { RoleNames, StaffRoles } from "@/lib/roles";

type Notice = { message: string; tone: "error" | "success" } | null;
type Filters = { type: string; targetId: string };

// Mirrors Cms.Application.Governance.DecisionRolePolicy.RolesFor across all
// recommendation types so the UI does not hide decisions the API would accept.
const DecisionRoles = [
  RoleNames.EmergencyCoordinator,
  RoleNames.CaseManager,
  RoleNames.LogisticsManager,
  RoleNames.ShelterAdmin,
  RoleNames.VolunteerCoordinator,
  RoleNames.SystemAdmin
];

const guidPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function isGuid(value: string): boolean {
  return guidPattern.test(value.trim());
}

function confidencePercent(value: number): number {
  // The API stores confidence as a decimal between 0 and 1.
  const scaled = value <= 1 ? value * 100 : value;
  return Math.round(Math.min(100, Math.max(0, scaled)));
}

function PayloadDetails({ value }: { value?: string | null }) {
  if (!value) {
    return <span>—</span>;
  }

  return (
    <details>
      <summary>View payload JSON</summary>
      <pre
        style={{
          whiteSpace: "pre-wrap",
          overflowWrap: "anywhere",
          fontSize: 11,
          padding: 10,
          background: "#f5f8fa",
          border: "1px solid var(--line)"
        }}
      >
        {value}
      </pre>
    </details>
  );
}

export default function AiDecisionSupportPage() {
  const { can } = useAuth();
  const canDecide = can(DecisionRoles);
  const [typeFilter, setTypeFilter] = useState("");
  const [targetFilter, setTargetFilter] = useState("");
  const [filters, setFilters] = useState<Filters>({ type: "", targetId: "" });
  const [recommendations, setRecommendations] = useState<RecommendationDto[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [selected, setSelected] = useState<RecommendationDto | null>(null);
  const [decisions, setDecisions] = useState<DecisionRecordDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [decisionsLoading, setDecisionsLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [outcome, setOutcome] = useState<DecisionOutcome>("Approved");
  const [reason, setReason] = useState("");
  const [finalPayloadJson, setFinalPayloadJson] = useState("");
  const [notice, setNotice] = useState<Notice>(null);

  const load = useCallback(async (next: Filters) => {
    setLoading(true);
    setNotice(null);

    try {
      const items = await getRecommendations(next);
      setRecommendations(items);
      setSelectedId((current) =>
        current && items.some((item) => item.id === current)
          ? current
          : (items[0]?.id ?? null)
      );
    } catch (error) {
      setNotice({ message: errorMessage(error), tone: "error" });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load(filters);
  }, [filters, load]);

  useEffect(() => {
    if (!selectedId) {
      setSelected(null);
      setDecisions([]);
      return;
    }

    let cancelled = false;
    setDetailLoading(true);

    void getRecommendation(selectedId)
      .then((item) => {
        if (!cancelled) {
          setSelected(item);
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setNotice({ message: errorMessage(error), tone: "error" });
          setSelected(null);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setDetailLoading(false);
        }
      });

    setOutcome("Approved");
    setReason("");
    setFinalPayloadJson("");

    return () => {
      cancelled = true;
    };
  }, [selectedId]);

  const loadDecisions = useCallback(async () => {
    if (!selectedId) {
      setDecisions([]);
      return;
    }

    setDecisionsLoading(true);

    try {
      setDecisions(await getRecommendationDecisions(selectedId));
    } catch (error) {
      setNotice({ message: errorMessage(error), tone: "error" });
    } finally {
      setDecisionsLoading(false);
    }
  }, [selectedId]);

  useEffect(() => {
    void loadDecisions();
  }, [loadDecisions]);

  function applyFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (targetFilter.trim() && !isGuid(targetFilter)) {
      setNotice({
        message: "Target ID must be a valid GUID.",
        tone: "error"
      });
      return;
    }

    const next = { type: typeFilter.trim(), targetId: targetFilter.trim() };
    setNotice(null);
    setFilters(next);

    if (next.type === filters.type && next.targetId === filters.targetId) {
      void load(next);
    }
  }

  async function submitDecision(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!selected || !canDecide) {
      return;
    }

    if (!reason.trim()) {
      setNotice({ message: "A decision reason is required.", tone: "error" });
      return;
    }

    let payload: string | null = null;

    if (outcome === "Modified") {
      if (!finalPayloadJson.trim()) {
        setNotice({
          message: "A final payload is required when modifying a recommendation.",
          tone: "error"
        });
        return;
      }

      try {
        JSON.parse(finalPayloadJson);
      } catch {
        setNotice({
          message: "Final payload must be valid JSON.",
          tone: "error"
        });
        return;
      }

      payload = finalPayloadJson.trim();
    }

    setSaving(true);
    setNotice(null);

    try {
      const recorded = await recordDecision(selected.id, {
        outcome,
        reason: reason.trim(),
        finalPayloadJson: payload
      });
      setNotice({
        message: `${recorded.outcome} decision recorded.`,
        tone: "success"
      });
      setReason("");
      setFinalPayloadJson("");
      await Promise.all([loadDecisions(), load(filters)]);
    } catch (error) {
      setNotice({ message: errorMessage(error), tone: "error" });
    } finally {
      setSaving(false);
    }
  }

  return (
    <AuthGuard allowedRoles={StaffRoles}>
      <div className="ops-title">
        <div>
          <p>AI DECISION SUPPORT</p>
          <h1>Recommendations &amp; human review</h1>
          <span>
            Every suggestion is advisory. A named officer records the decision and the
            full history stays reviewable.
          </span>
        </div>
        <div>
          <Link className="btn btn-outline" href="/operations/decisions">
            <Icon name="file" /> Decision history
          </Link>
        </div>
      </div>

      <Toast
        message={notice?.message ?? null}
        tone={notice?.tone ?? "error"}
        onDismiss={() => setNotice(null)}
      />

      <section className="ops-card">
        <div className="card-heading">
          <div>
            <h3>Recommendation queue</h3>
            <p>Live output from the decision-support API</p>
          </div>
          <Button variant="secondary" onClick={() => void load(filters)} disabled={loading}>
            Refresh
          </Button>
        </div>

        <form className="filters" onSubmit={applyFilters} style={{ padding: 20 }}>
          <Input
            label="Type"
            placeholder="e.g. ShelterDispatch"
            value={typeFilter}
            onChange={(event) => setTypeFilter(event.target.value)}
          />
          <Input
            label="Target ID"
            placeholder="Guid"
            value={targetFilter}
            onChange={(event) => setTargetFilter(event.target.value)}
          />
          <Button type="submit" disabled={loading}>
            Apply filters
          </Button>
        </form>

        {loading ? (
          <div style={{ padding: 20 }}>
            <Skeleton rows={5} />
          </div>
        ) : recommendations.length === 0 ? (
          <div className="empty-state">No recommendations match the current filters.</div>
        ) : (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th scope="col">Type</th>
                  <th scope="col">Target</th>
                  <th scope="col">Confidence</th>
                  <th scope="col">Source</th>
                  <th scope="col">Created</th>
                  <th scope="col">Review</th>
                </tr>
              </thead>
              <tbody>
                {recommendations.map((item) => (
                  <tr key={item.id}>
                    <td>
                      <strong>{item.type}</strong>
                      <div className="metric__label">{item.rationale}</div>
                    </td>
                    <td>
                      <code>{item.targetId}</code>
                    </td>
                    <td>{confidencePercent(item.confidence)}%</td>
                    <td>{item.source}</td>
                    <td>{formatDate(item.createdAt)}</td>
                    <td>
                      <Button
                        variant="secondary"
                        onClick={() => setSelectedId(item.id)}
                        disabled={selectedId === item.id}
                      >
                        {selectedId === item.id ? "Selected" : "View"}
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="ops-card ai-review" style={{ marginTop: 16 }}>
        <div className="ai-review-title">
          <span>
            <Icon name="brain" />
          </span>
          <div>
            <i>AI DECISION SUPPORT</i>
            <h3>Selected recommendation</h3>
          </div>
          <small>
            <Icon name="shield" size={13} /> Requires human review
          </small>
        </div>

        {detailLoading ? (
          <div style={{ padding: 22 }}>
            <Skeleton rows={4} />
          </div>
        ) : !selected ? (
          <div className="empty-state">
            Select a recommendation from the queue to review its evidence and record a decision.
          </div>
        ) : (
          <>
            <div className="recommendation">
              <div className="rec-score">
                <span>RECOMMENDED TYPE</span>
                <b>{selected.type}</b>
                <em>
                  Target <code>{selected.targetId}</code>
                </em>
                <em>Generated {formatDate(selected.createdAt)}</em>
              </div>
              <div
                className="confidence-ring"
                style={{
                  background: `conic-gradient(var(--blue) ${confidencePercent(
                    selected.confidence
                  )}%, #dbe8f0 0)`
                }}
              >
                <span>
                  {confidencePercent(selected.confidence)}
                  <small>%</small>
                </span>
              </div>
            </div>

            <div className="reasoning">
              <b>Supporting signals</b>
              <ul>
                <li>{selected.rationale}</li>
                <li>Source: {selected.source}</li>
              </ul>
            </div>

            <div className="suggested">
              <b>AI payload (advisory)</b>
              <PayloadDetails value={selected.payloadJson} />
            </div>

            {canDecide ? (
              <form onSubmit={submitDecision} style={{ padding: "0 22px 20px" }}>
                <label className="field">
                  <span className="field__label">Decision</span>
                  <select
                    className="input"
                    value={outcome}
                    onChange={(event) => {
                      const next = event.target.value as DecisionOutcome;
                      setOutcome(next);
                      if (next === "Modified" && !finalPayloadJson) {
                        setFinalPayloadJson(selected.payloadJson);
                      }
                    }}
                  >
                    {DecisionOutcomes.map((item) => (
                      <option key={item} value={item}>
                        {item}
                      </option>
                    ))}
                  </select>
                </label>

                <label className="field">
                  <span className="field__label">Reason (required)</span>
                  <textarea
                    className="input"
                    rows={3}
                    value={reason}
                    onChange={(event) => setReason(event.target.value)}
                    placeholder="Why is this the right human decision?"
                  />
                </label>

                {outcome === "Modified" ? (
                  <label className="field">
                    <span className="field__label">Final payload JSON (required)</span>
                    <textarea
                      className="input"
                      rows={5}
                      value={finalPayloadJson}
                      onChange={(event) => setFinalPayloadJson(event.target.value)}
                    />
                  </label>
                ) : null}

                <div className="actions">
                  <Button type="submit" disabled={saving}>
                    {saving ? "Saving…" : "Record decision"}
                  </Button>
                  <span className="metric__label">
                    Recording a decision appends to the review history; it never edits AI output.
                  </span>
                </div>
              </form>
            ) : (
              <p className="metric__label" style={{ padding: "0 22px 20px" }}>
                Your role can review recommendations, but only an Emergency Coordinator,
                Case Manager or System Admin can record a decision.
              </p>
            )}

            <div style={{ padding: "0 22px 22px" }}>
              <b>Decision history</b>
              {decisionsLoading ? (
                <Skeleton rows={2} />
              ) : decisions.length === 0 ? (
                <p className="metric__label">No decisions recorded for this recommendation yet.</p>
              ) : (
                <div className="table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th scope="col">Outcome</th>
                        <th scope="col">Reason</th>
                        <th scope="col">Final payload</th>
                        <th scope="col">Decided by</th>
                        <th scope="col">Decided at</th>
                      </tr>
                    </thead>
                    <tbody>
                      {decisions.map((decision) => (
                        <tr key={decision.id}>
                          <td>
                            <Badge>{decision.outcome}</Badge>
                          </td>
                          <td>{decision.reason}</td>
                          <td>
                            <PayloadDetails value={decision.finalPayloadJson} />
                          </td>
                          <td>
                            <code>{decision.decidedBy}</code>
                          </td>
                          <td>{formatDate(decision.decidedAt)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </>
        )}
      </section>

      <section className="ops-card" style={{ marginTop: 16 }}>
        <div className="card-heading">
          <div>
            <h3>Human oversight rules</h3>
            <p>How this platform handles automated suggestions</p>
          </div>
        </div>
        <div className="resource-bars">
          <div>
            <span>
              <b>Advisory only</b>
              <i>AI output never triggers a response on its own.</i>
            </span>
          </div>
          <div>
            <span>
              <b>Named approver</b>
              <i>An authorized officer approves or rejects each recommendation.</i>
            </span>
          </div>
          <div>
            <span>
              <b>Append-only record</b>
              <i>Approvals and overrides are written to the audit trail.</i>
            </span>
          </div>
        </div>
      </section>
    </AuthGuard>
  );
}
