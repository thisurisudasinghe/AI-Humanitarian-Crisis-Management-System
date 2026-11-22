/**
 * DEV2 case API seam for the active SAFEWAY frontend.
 *
 * Every request below maps to a documented DEV2 backend endpoint. Shared
 * auth/refresh/error handling stays in `lib/api.ts`; this module only adds
 * DEV2-specific payload mapping and response types.
 */
import { api, apiJson, ApiError } from "@/lib/api";

export interface CrisisReportInput {
  category: string;
  description: string;
  location: string;
  urgency: string;
  lat: number;
  lng: number;
}

export interface CrisisReportReceipt {
  reference: string;
  submittedAt: string;
  evidenceAttached?: boolean;
  evidenceFileName?: string;
  evidenceError?: string;
}

export const CASE_CATEGORIES = [
  "Food",
  "Medicine",
  "Shelter",
  "Transport",
  "Accessibility",
  "Other"
] as const;
export type CaseCategory = (typeof CASE_CATEGORIES)[number];

export const CASE_URGENCIES = ["Low", "Medium", "High", "Critical"] as const;
export type CaseUrgency = (typeof CASE_URGENCIES)[number];

export const REQUEST_STATUSES = [
  "Submitted",
  "Verified",
  "Rejected",
  "Triaged",
  "Recommended",
  "Approved",
  "Allocated",
  "Dispatched",
  "InProgress",
  "Fulfilled",
  "Closed",
  "Escalated",
  "Merged",
  "OnHold",
  "Reopened"
] as const;
export type RequestStatus = (typeof REQUEST_STATUSES)[number];

export const CRISIS_STATUSES = ["Active", "Closed"] as const;
export type CrisisStatus = (typeof CRISIS_STATUSES)[number];

/** Mirrors RequestStateMachine.OpenStates in the DEV2 domain layer. */
export const OPEN_REQUEST_STATUSES: readonly RequestStatus[] = [
  "Submitted",
  "Verified",
  "Triaged",
  "Recommended",
  "Approved",
  "Allocated",
  "Dispatched",
  "InProgress",
  "Fulfilled"
];

export const RESOLVED_REQUEST_STATUSES: readonly RequestStatus[] = ["Fulfilled", "Closed"];

export interface Paged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface CaseRequestListItem {
  id: string;
  category: CaseCategory;
  urgency: CaseUrgency;
  status: RequestStatus;
  description: string;
  areaId?: string | null;
  submittedAt: string;
  slaAgeHours: number;
  isOverdue: boolean;
}

export interface CrisisRecord {
  id: string;
  name: string;
  type: string;
  description?: string | null;
  status: CrisisStatus;
  startedAt: string;
  endedAt?: string | null;
}

export interface CrisisSummaryRecord {
  crisisId: string;
  byStatus: Record<string, number>;
  byUrgency: Record<string, number>;
  byArea: Record<string, number>;
  openTotal: number;
  overdueTotal: number;
}

export interface AreaRecord {
  id: string;
  crisisId: string;
  name: string;
  description?: string | null;
  lat: number;
  lng: number;
  radiusKm?: number | null;
}

interface CreateCaseRequestPayload {
  category: CaseCategory;
  urgency: CaseUrgency;
  description: string;
  lat: number;
  lng: number;
  address: string;
  householdSize: number;
}

export interface StaffCaseQuery {
  crisisId?: string;
  status?: RequestStatus;
  urgency?: CaseUrgency;
  category?: CaseCategory;
  q?: string;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDescending?: boolean;
}

/**
 * The public report form uses human-facing labels. These maps translate only
 * to values that exist in the backend Category and Urgency enums.
 */
const reportCategoryToCaseCategory: Record<string, CaseCategory> = {
  Flooding: "Other",
  Fire: "Other",
  "Medical emergency": "Medicine",
  Displacement: "Shelter",
  "Food or water": "Food",
  Other: "Other"
};

const reportSeverityToCaseUrgency: Record<string, CaseUrgency> = {
  "Needs attention": "Low",
  Urgent: "High",
  "Immediate danger": "Critical"
};

function queryString(params: Record<string, string | number | boolean | null | undefined>): string {
  const search = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") {
      search.set(key, String(value));
    }
  });
  const value = search.toString();
  return value ? `?${value}` : "";
}

function clean(value: string): string {
  return value.trim();
}

/**
 * Submits the supported report fields to `POST /api/v1/requests`.
 *
 * The backend creates requests against the signed-in Citizen. Contact and
 * consent fields are not part of CreateRequestDto and are not collected by the
 * form. Free-text location is persisted as `address`. Coordinates are
 * required: an absent or non-finite value is rejected instead of silently
 * becoming (0, 0).
 */
export async function submitCrisisReport(
  input: CrisisReportInput
): Promise<CrisisReportReceipt> {
  if (!Number.isFinite(input.lat) || !Number.isFinite(input.lng)) {
    throw new Error("Valid latitude and longitude are required.");
  }

  const payload: CreateCaseRequestPayload = {
    category: reportCategoryToCaseCategory[input.category] ?? "Other",
    urgency: reportSeverityToCaseUrgency[input.urgency] ?? "Low",
    description: clean(input.description),
    lat: input.lat,
    lng: input.lng,
    address: clean(input.location),
    householdSize: 1
  };

  const created = await apiJson<{ id: string }>("/api/v1/requests", "POST", payload);

  return {
    reference: created.id,
    submittedAt: new Date().toISOString()
  };
}

/**
 * Uploads evidence to the case that was just created (`reference` is the
 * request id). Uses the shared authenticated api() wrapper; FormData lets the
 * browser set the multipart boundary, so no Content-Type header is added.
 */
export async function uploadCrisisReportEvidence(
  reference: string,
  file: File
): Promise<void> {
  if (!reference.trim()) {
    throw new Error("A report reference is required before evidence can be uploaded.");
  }

  const body = new FormData();
  body.append("file", file);

  await api<void>(
    `/api/v1/requests/${encodeURIComponent(reference)}/attachments`,
    { method: "POST", body }
  );
}

export function listMyCaseRequests(
  page = 1,
  pageSize = 50
): Promise<Paged<CaseRequestListItem>> {
  return api<Paged<CaseRequestListItem>>(
    `/api/v1/requests/mine${queryString({ page, pageSize, sortBy: "submittedAt" })}`
  );
}

export function listCrises(): Promise<CrisisRecord[]> {
  return api<CrisisRecord[]>("/api/v1/crises");
}

export function listAreas(crisisId: string): Promise<AreaRecord[]> {
  return api<AreaRecord[]>(`/api/v1/areas${queryString({ crisisId })}`);
}

export function getCrisisSummary(crisisId: string): Promise<CrisisSummaryRecord> {
  return api<CrisisSummaryRecord>(`/api/v1/crises/${encodeURIComponent(crisisId)}/summary`);
}

export function listStaffCaseRequests(
  params: StaffCaseQuery = {}
): Promise<Paged<CaseRequestListItem>> {
  return api<Paged<CaseRequestListItem>>(
    `/api/v1/requests${queryString({
      crisisId: params.crisisId,
      status: params.status,
      urgency: params.urgency,
      category: params.category,
      q: params.q,
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 50,
      sortBy: params.sortBy,
      sortDescending: params.sortDescending
    })}`
  );
}

export function listUnresolvedCaseRequests(
  pageSize = 50
): Promise<Paged<CaseRequestListItem>> {
  return api<Paged<CaseRequestListItem>>(
    `/api/v1/requests/unresolved${queryString({ page: 1, pageSize })}`
  );
}

/** User-facing message for the shared ApiError produced by `lib/api.ts`. */
export function caseRequestErrorMessage(cause: unknown, fallback: string): string {
  if (cause instanceof ApiError && cause.status === 401) {
    return "Sign in as a citizen to submit and track reports.";
  }
  return cause instanceof Error ? cause.message : fallback;
}
