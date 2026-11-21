/**
 * Typed client for human decisions and the post-incident decision review feed.
 */
import type { CmsPagedResult } from "./admin";
import { api, apiJson } from "./api";

export const DecisionOutcomes = [
  "Approved",
  "Modified",
  "Rejected",
  "Deferred"
] as const;

export type DecisionOutcome = (typeof DecisionOutcomes)[number];

export interface DecisionRecordDto {
  id: string;
  recommendationId: string;
  outcome: string;
  finalPayloadJson?: string | null;
  reason: string;
  decidedBy: string;
  decidedAt: string;
}

export interface DecisionInput {
  outcome: DecisionOutcome;
  finalPayloadJson?: string | null;
  reason: string;
}

/** One recommendation/human-decision pair from the reviews feed. */
export interface ReviewEntryDto {
  recommendationId: string;
  type: string;
  targetId: string;
  source: string;
  confidence: number;
  rationale: string;
  payloadJson: string;
  decisionId?: string | null;
  outcome?: string | null;
  reason?: string | null;
  finalPayloadJson?: string | null;
  decidedBy?: string | null;
  decidedAt?: string | null;
  isOverride: boolean;
}

export function recordDecision(
  recommendationId: string,
  input: DecisionInput
): Promise<DecisionRecordDto> {
  return apiJson<DecisionRecordDto>(
    `/api/v1/recommendations/${encodeURIComponent(recommendationId)}/decisions`,
    "POST",
    input
  );
}

export function getRecommendationDecisions(
  recommendationId: string
): Promise<DecisionRecordDto[]> {
  return api<DecisionRecordDto[]>(
    `/api/v1/recommendations/${encodeURIComponent(recommendationId)}/decisions`
  );
}

export function getDecision(id: string): Promise<DecisionRecordDto> {
  return api<DecisionRecordDto>(`/api/v1/decisions/${encodeURIComponent(id)}`);
}

/** Paged decision history across recommendations, optionally by crisis. */
export function getDecisionReviews(
  options: { crisisId?: string; page: number; pageSize: number }
): Promise<CmsPagedResult<ReviewEntryDto>> {
  const params = new URLSearchParams({
    page: String(options.page),
    pageSize: String(options.pageSize)
  });

  if (options.crisisId?.trim()) {
    params.set("crisisId", options.crisisId.trim());
  }

  return api<CmsPagedResult<ReviewEntryDto>>(
    `/api/v1/reviews/decisions?${params.toString()}`
  );
}
