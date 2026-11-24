/**
 * Citizen-facing mapping from the real DEV2 request state machine to a small
 * visual timeline. No stage is marked complete unless the backend status
 * history (or the current status itself) proves that it was reached.
 */
import type { RequestStatus, RequestTimelineEntry } from "@/lib/crisis";

export type CitizenStageKey = "submitted" | "review" | "progress" | "resolved";

export const CITIZEN_STAGES: readonly CitizenStageKey[] = [
  "submitted",
  "review",
  "progress",
  "resolved"
];

const STAGE_STATUSES: Record<CitizenStageKey, readonly RequestStatus[]> = {
  submitted: ["Submitted"],
  review: ["Verified", "Triaged", "Recommended", "Approved", "Escalated", "OnHold", "Reopened"],
  progress: ["Allocated", "Dispatched", "InProgress"],
  resolved: ["Fulfilled", "Closed"]
};

const EXCEPTIONAL_STATUSES: readonly RequestStatus[] = [
  "Rejected",
  "Merged",
  "OnHold",
  "Escalated",
  "Reopened"
];

const EXCEPTIONAL_STAGE: Partial<Record<RequestStatus, CitizenStageKey>> = {
  Rejected: "review",
  Merged: "review",
  OnHold: "review",
  Escalated: "review",
  Reopened: "review"
};

export type CitizenStageState = "completed" | "current" | "pending" | "exceptional";

export interface CitizenStageView {
  key: CitizenStageKey;
  state: CitizenStageState;
  timestamp?: string;
  reachedStatuses: RequestStatus[];
}

export function isExceptionalStatus(status: RequestStatus): boolean {
  return EXCEPTIONAL_STATUSES.includes(status);
}

export function stageForStatus(status: RequestStatus): CitizenStageKey {
  for (const stage of CITIZEN_STAGES) {
    if (STAGE_STATUSES[stage].includes(status)) return stage;
  }
  return EXCEPTIONAL_STAGE[status] ?? "review";
}

export function statusTone(status: RequestStatus): string {
  if (status === "Fulfilled" || status === "Closed") return "green";
  if (status === "Rejected" || status === "Merged") return "coral";
  if (status === "Escalated") return "amber";
  if (status === "OnHold") return "muted";
  return "blue";
}

export function buildCitizenStages(
  status: RequestStatus,
  timeline: readonly RequestTimelineEntry[] = []
): CitizenStageView[] {
  const firstSeenAt = new Map<RequestStatus, string>();
  const reached = new Set<RequestStatus>();

  timeline.forEach((entry) => {
    reached.add(entry.toStatus);
    if (entry.createdAt && !firstSeenAt.has(entry.toStatus)) {
      firstSeenAt.set(entry.toStatus, entry.createdAt);
    }
  });
  reached.add(status);

  const anchorStage = stageForStatus(status);
  const anchorIndex = CITIZEN_STAGES.indexOf(anchorStage);
  const exceptional = isExceptionalStatus(status);
  const completed = status === "Fulfilled" || status === "Closed";

  return CITIZEN_STAGES.map((key, index) => {
    const stageStatuses = STAGE_STATUSES[key];
    const reachedStatuses = stageStatuses.filter((item) => reached.has(item));
    const timestamp = stageStatuses
      .map((item) => firstSeenAt.get(item))
      .filter((item): item is string => Boolean(item))
      .sort()[0];

    let state: CitizenStageState;
    if (index > anchorIndex) {
      state = "pending";
    } else if (index < anchorIndex) {
      state = reachedStatuses.length > 0 ? "completed" : "pending";
    } else if (completed && key === "resolved") {
      state = "completed";
    } else {
      state = exceptional ? "exceptional" : "current";
    }

    return { key, state, timestamp, reachedStatuses };
  });
}
