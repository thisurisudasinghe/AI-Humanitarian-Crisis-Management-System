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
  firstName: string;
  lastName: string;
  phone: string;
  email?: string;
  consent: boolean;
}

export interface CrisisReportReceipt {
  reference: string;
  submittedAt: string;
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
 * The backend creates requests against the signed-in Citizen and does not
 * expose firstName/lastName/email/consent in CreateRequestDto, so those
 * fields are deliberately not transmitted. The free-text location is sent as
 * `address`; the current form does not collect coordinates.
 */
export async function submitCrisisReport(
  input: CrisisReportInput
): Promise<CrisisReportReceipt> {
  const payload: CreateCaseRequestPayload = {
    category: reportCategoryToCaseCategory[input.category] ?? "Other",
    urgency: reportSeverityToCaseUrgency[input.urgency] ?? "Low",
    description: clean(input.description),
    lat: 0,
    lng: 0,
    address: clean(input.location),
    householdSize: 1
  };

  const created = await apiJson<{ id: string }>("/api/v1/requests", "POST", payload);

  return {
    reference: created.id,
    submittedAt: new Date().toISOString()
  };
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
