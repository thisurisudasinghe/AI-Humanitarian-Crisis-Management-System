"use client";

import Link from "next/link";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import { Badge, Button, Input, Skeleton, Toast } from "@/components/ui";
import { formatDate } from "@/lib/admin";

import { useT } from "@/lib/i18n";
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

// Mirrors Cms.Application.Governance.DecisionRolePolicy.RolesFor. SystemAdmin
// is deliberately not a decider; only the roles below can record a decision.
function decisionRolesFor(type: string): readonly string[] {
  switch (type) {
    case "ResourceMatch":
      return [RoleNames.LogisticsManager, RoleNames.EmergencyCoordinator];
    case "ShelterMatch":
      return [RoleNames.ShelterAdmin, RoleNames.EmergencyCoordinator];
    case "VolunteerMatch":
      return [RoleNames.VolunteerCoordinator, RoleNames.EmergencyCoordinator];
    case "Announcement":
    case "CloseCase":
      return [RoleNames.EmergencyCoordinator];
    default:
      return [RoleNames.CaseManager, RoleNames.EmergencyCoordinator];
  }
}

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
  const t = useT();
  if (!value) {
    return <span>—</span>;
  }

  return (
    <details>
      <summary>{t("operations.ai.viewPayloadJson")}</summary>
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
  const t = useT();
  const { can } = useAuth();
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
      setNotice({ message: localizedErrorMessage(error, t), tone: "error" });
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
          setNotice({ message: localizedErrorMessage(error, t), tone: "error" });
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
      setNotice({ message: localizedErrorMessage(error, t), tone: "error" });
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
        message: t("operations.ai.errTargetId"),
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

    if (!selected || !can(decisionRolesFor(selected.type))) {
      return;
    }

    if (!reason.trim()) {
      setNotice({ message: t("operations.ai.errReasonRequired"), tone: "error" });
      return;
    }

    let payload: string | null = null;

    if (outcome === "Modified") {
      if (!finalPayloadJson.trim()) {
        setNotice({
          message: t("operations.ai.errFinalPayloadRequired"),
          tone: "error"
        });
        return;
      }

      try {
        JSON.parse(finalPayloadJson);
      } catch {
        setNotice({
          message: t("operations.ai.errInvalidJson"),
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
        message: t("operations.ai.decisionRecorded", { outcome: recorded.outcome }),
        tone: "success"
      });
      setReason("");
      setFinalPayloadJson("");
      await Promise.all([loadDecisions(), load(filters)]);
    } catch (error) {
      setNotice({ message: localizedErrorMessage(error, t), tone: "error" });
    } finally {
      setSaving(false);
    }
  }

  return (
    <AuthGuard allowedRoles={StaffRoles}>
      <div className="ops-title">
        <div>
          <p>{t("operations.shared.aiDecisionSupport")}</p>
          <h1>{t("operations.ai.title")}</h1>
          <span>
            {t("operations.ai.subtitle")}
          </span>
        </div>
        <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
          <Link className="btn btn-outline" href="/operations/ai/chat">
            <Icon name="brain" /> {t("operations.ai.faqChat")}
          </Link>
          <Link className="btn btn-outline" href="/operations/decisions">
            <Icon name="file" /> {t("operations.shared.decisionHistory")}
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
            <h3>{t("operations.shared.recommendationQueue")}</h3>
            <p>{t("operations.ai.queueSubtitle")}</p>
          </div>
          <Button variant="secondary" onClick={() => void load(filters)} disabled={loading}>
            {t("operations.shared.refresh")}
          </Button>
        </div>

        <form className="filters" onSubmit={applyFilters} style={{ padding: 20 }}>
          <Input
            label={t("operations.ai.typeLabel")}
            placeholder={t("operations.ai.typePlaceholder")}
            value={typeFilter}
            onChange={(event) => setTypeFilter(event.target.value)}
          />
          <Input
            label={t("operations.ai.targetIdLabel")}
            placeholder={t("operations.shared.guid")}
            value={targetFilter}
            onChange={(event) => setTargetFilter(event.target.value)}
          />
          <Button type="submit" disabled={loading}>
            {t("operations.shared.applyFilters")}
          </Button>
        </form>

        {loading ? (
          <div style={{ padding: 20 }}>
            <Skeleton rows={5} />
          </div>
        ) : recommendations.length === 0 ? (
          <div className="empty-state">{t("operations.ai.emptyQueue")}</div>
        ) : (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th scope="col">{t("operations.ai.typeLabel")}</th>
                  <th scope="col">{t("operations.ai.colTarget")}</th>
                  <th scope="col">{t("operations.ai.colConfidence")}</th>
                  <th scope="col">{t("operations.ai.colSource")}</th>
                  <th scope="col">{t("operations.ai.colCreated")}</th>
                  <th scope="col">{t("operations.ai.colReview")}</th>
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
                        {selectedId === item.id ? t("operations.shared.selected") : t("operations.shared.view")}
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
            <i>{t("operations.shared.aiDecisionSupport")}</i>
            <h3>{t("operations.ai.selectedTitle")}</h3>
          </div>
          <small>
            <Icon name="shield" size={13} /> {t("operations.shared.requiresHumanReview")}
          </small>
        </div>

        {detailLoading ? (
          <div style={{ padding: 22 }}>
            <Skeleton rows={4} />
          </div>
        ) : !selected ? (
          <div className="empty-state">
            {t("operations.ai.selectPrompt")}
          </div>
        ) : (
          <>
            <div className="recommendation">
              <div className="rec-score">
                <span>{t("operations.ai.recommendedType")}</span>
                <b>{selected.type}</b>
                <em>
                  {t("operations.shared.target")} <code>{selected.targetId}</code>
                </em>
                <em>{t("operations.ai.generated", { date: formatDate(selected.createdAt) })}</em>
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
              <b>{t("operations.shared.supportingSignals")}</b>
              <ul>
                <li>{selected.rationale}</li>
                <li>{t("operations.shared.source", { source: selected.source })}</li>
              </ul>
            </div>

            <div className="suggested">
              <b>{t("operations.ai.aiPayloadAdvisory")}</b>
              <PayloadDetails value={selected.payloadJson} />
            </div>

            {can(decisionRolesFor(selected.type)) ? (
              <form onSubmit={submitDecision} style={{ padding: "0 22px 20px" }}>
                <label className="field">
                  <span className="field__label">{t("operations.ai.decisionLabel")}</span>
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
                  <span className="field__label">{t("operations.ai.reasonLabel")}</span>
                  <textarea
                    className="input"
                    rows={3}
                    value={reason}
                    onChange={(event) => setReason(event.target.value)}
                    placeholder={t("operations.ai.reasonPlaceholder")}
                  />
                </label>

                {outcome === "Modified" ? (
                  <label className="field">
                    <span className="field__label">{t("operations.ai.finalPayloadLabel")}</span>
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
                    {saving ? t("operations.ai.saving") : t("operations.ai.recordDecision")}
                  </Button>
                  <span className="metric__label">
                    {t("operations.ai.decisionNote")}
                  </span>
                </div>
              </form>
            ) : (
              <p className="metric__label" style={{ padding: "0 22px 20px" }}>
                {t("operations.ai.roleNote")}
              </p>
            )}

            <div style={{ padding: "0 22px 22px" }}>
              <b>{t("operations.shared.decisionHistory")}</b>
              {decisionsLoading ? (
                <Skeleton rows={2} />
              ) : decisions.length === 0 ? (
                <p className="metric__label">{t("operations.ai.noDecisions")}</p>
              ) : (
                <div className="table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th scope="col">{t("operations.ai.colOutcome")}</th>
                        <th scope="col">{t("operations.ai.colReason")}</th>
                        <th scope="col">{t("operations.decisions.finalPayload")}</th>
                        <th scope="col">{t("operations.ai.colDecidedBy")}</th>
                        <th scope="col">{t("operations.ai.colDecidedAt")}</th>
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
            <h3>{t("operations.ai.oversightTitle")}</h3>
            <p>{t("operations.ai.oversightSubtitle")}</p>
          </div>
        </div>
        <div className="resource-bars">
          <div>
            <span>
              <b>{t("operations.ai.advisoryOnly")}</b>
              <i>{t("operations.ai.advisoryBody")}</i>
            </span>
          </div>
          <div>
            <span>
              <b>{t("operations.ai.namedApprover")}</b>
              <i>{t("operations.ai.namedApproverBody")}</i>
            </span>
          </div>
          <div>
            <span>
              <b>{t("operations.ai.appendOnly")}</b>
              <i>{t("operations.ai.appendOnlyBody")}</i>
            </span>
          </div>
        </div>
      </section>
    </AuthGuard>
  );
}
