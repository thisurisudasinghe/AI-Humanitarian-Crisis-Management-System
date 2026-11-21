/**
 * Typed client for the Dev 3 recommendation endpoints.
 *
 * All calls go through `api`/`apiJson`, so bearer auth, refresh and RFC 7807
 * error mapping are shared with the rest of the frontend.
 */
import { api, apiJson } from "./api";

export interface RecommendationDto {
  id: string;
  type: string;
  targetId: string;
  payloadJson: string;
  confidence: number;
  rationale: string;
  source: string;
  createdAt: string;
}

export interface CreateRecommendationInput {
  type: string;
  targetId: string;
  payloadJson: string;
  confidence: number;
  rationale: string;
  source: string;
}

export interface MatchRequestInput {
  targetId: string;
  targetType: string;
}

export interface MatchCandidateDto {
  targetId: string;
  targetType: string;
  score: number;
  rationale: string;
  isApproximate: boolean;
}

export interface GeneratedRecommendation {
  recommendationId: string;
  message: string;
}

/** Lists recommendations, optionally narrowed by type and/or target id. */
export function getRecommendations(
  filters: { type?: string; targetId?: string } = {}
): Promise<RecommendationDto[]> {
  const params = new URLSearchParams();

  if (filters.type?.trim()) {
    params.set("type", filters.type.trim());
  }

  if (filters.targetId?.trim()) {
    params.set("targetId", filters.targetId.trim());
  }

  const query = params.toString();
  return api<RecommendationDto[]>(
    `/api/v1/recommendations${query ? `?${query}` : ""}`
  );
}

/** Loads one recommendation for the detail/review view. */
export function getRecommendation(id: string): Promise<RecommendationDto> {
  return api<RecommendationDto>(`/api/v1/recommendations/${encodeURIComponent(id)}`);
}

/** Loads every recommendation attached to a specific target. */
export function getRecommendationsForTarget(
  targetId: string,
  type?: string
): Promise<RecommendationDto[]> {
  const params = new URLSearchParams();

  if (type?.trim()) {
    params.set("type", type.trim());
  }

  const query = params.toString();

  return api<RecommendationDto[]>(
    `/api/v1/recommendations/target/${encodeURIComponent(targetId)}${
      query ? `?${query}` : ""
    }`
  );
}

export function createRecommendation(
  input: CreateRecommendationInput
): Promise<{ id: string }> {
  return apiJson<{ id: string }>("/api/v1/recommendations", "POST", input);
}

export function getMatches(input: MatchRequestInput): Promise<MatchCandidateDto[]> {
  return apiJson<MatchCandidateDto[]>("/api/v1/recommendations/matches", "POST", input);
}

export function generateCrisisRecommendations(
  crisisId: string
): Promise<GeneratedRecommendation> {
  return apiJson<GeneratedRecommendation>(
    `/api/v1/recommendations/crisis/${encodeURIComponent(crisisId)}/generate`,
    "POST",
    {}
  );
}
