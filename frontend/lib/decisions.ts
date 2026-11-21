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
  /** Optional until the post-incident-review feed publishes this column. */
  finalRequestStatus?: string | null;
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

export interface DecisionReviewOptions {
  crisisId?: string;
  /** Inclusive UTC start of the requested date range. */
  from?: string;
  /** Exclusive/end UTC bound of the requested date range. */
  to?: string;
  outcome?: string;
  page: number;
  pageSize: number;
}

function reviewParams(options: DecisionReviewOptions): URLSearchParams {
  const params = new URLSearchParams({
    page: String(options.page),
    pageSize: String(options.pageSize)
  });

  if (options.crisisId?.trim()) {
    params.set("crisisId", options.crisisId.trim());
  }

  if (options.from?.trim()) {
    params.set("from", options.from.trim());
  }

  if (options.to?.trim()) {
    params.set("to", options.to.trim());
  }

  if (options.outcome?.trim()) {
    params.set("outcome", options.outcome.trim());
  }

  return params;
}

/**
 * Required Dev 3 post-incident review feed. The response keeps the shared
 * `{items,total,page,pageSize}` paged shape; `finalRequestStatus` is optional
 * for compatibility with older responses.
 */
export function getDecisionReviews(
  options: DecisionReviewOptions
): Promise<CmsPagedResult<ReviewEntryDto>> {
  return api<CmsPagedResult<ReviewEntryDto>>(
    `/api/v1/post-incident-review?${reviewParams(options).toString()}`
  );
}

/** Compatibility alias for the original review route; new code uses the required route. */
export function getLegacyDecisionReviews(
  options: DecisionReviewOptions
): Promise<CmsPagedResult<ReviewEntryDto>> {
  return api<CmsPagedResult<ReviewEntryDto>>(
    `/api/v1/reviews/decisions?${reviewParams(options).toString()}`
  );
}
