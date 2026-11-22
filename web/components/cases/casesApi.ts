import { api } from "../../lib/api";

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

export const CATEGORIES = ["Food", "Medicine", "Shelter", "Transport", "Accessibility", "Other"] as const;
export const URGENCIES = ["Low", "Medium", "High", "Critical"] as const;
export const CRISIS_STATUSES = ["Active", "Closed"] as const;

export type RequestStatus = (typeof REQUEST_STATUSES)[number];
export type Category = (typeof CATEGORIES)[number];
export type Urgency = (typeof URGENCIES)[number];
export type CrisisStatus = (typeof CRISIS_STATUSES)[number];

export interface Paged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface RequestListItem {
  id: string;
  category: Category;
  urgency: Urgency;
  status: RequestStatus;
  description: string;
  areaId?: string | null;
  submittedAt: string;
  slaAgeHours: number;
  isOverdue: boolean;
}

export interface AttachmentMetadata {
  id: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  createdAt: string;
}

export interface TimelineEntry {
  id: string;
  fromStatus: RequestStatus;
  toStatus: RequestStatus;
  actorDisplayName?: string | null;
  reason?: string | null;
  createdAt: string;
}

export interface RequestDetail extends RequestListItem {
  requesterUserId: string;
  crisisId?: string | null;
  lat: number;
  lng: number;
  householdSize: number;
  accessibilityNeeds?: string | null;
  assignedToUserId?: string | null;
  mergedIntoRequestId?: string | null;
  verifiedAt?: string | null;
  triagedAt?: string | null;
  closedAt?: string | null;
  attachments: AttachmentMetadata[];
  timeline: TimelineEntry[];
}

export interface CreateRequestPayload {
  category: Category;
  urgency: Urgency;
  description: string;
  lat: number;
  lng: number;
  address?: string;
  householdSize: number;
  accessibilityNeeds?: string;
  crisisId?: string;
  areaId?: string;
}

export interface AlertItem {
  id: string;
  crisisId?: string | null;
  title: string;
  body: string;
  status: "Draft" | "Published";
  publishedAt?: string | null;
  createdAt: string;
}

export interface CreateAlertPayload {
  title: string;
  body: string;
  publish: boolean;
}

export interface ServiceCentre {
  id: string;
  crisisId?: string | null;
  name: string;
  address: string;
  lat: number;
  lng: number;
  services: string;
  openingHours?: string | null;
}

export interface ShelterSummary {
  id: string;
  name: string;
  lat: number;
  lng: number;
  freePlaces: number;
  services?: string | null;
}

export interface Household {
  id: string;
  size: number;
  accessibilityNeeds?: string | null;
  contactPhone?: string | null;
}

export interface Crisis {
  id: string;
  name: string;
  type: string;
  description?: string | null;
  status: CrisisStatus;
  startedAt: string;
  endedAt?: string | null;
}

export interface Area {
  id: string;
  crisisId: string;
  name: string;
  description?: string | null;
  lat: number;
  lng: number;
  radiusKm?: number | null;
}

export interface CrisisSummary {
  crisisId: string;
  byStatus: Record<string, number>;
  byUrgency: Record<string, number>;
  byArea: Record<string, number>;
  openTotal: number;
  overdueTotal: number;
}

export interface KpiReport {
  unresolvedRequestCount: number;
  averageResponseTimeHours?: number | null;
  aiOverrideRatePercent?: number | null;
  reservedResources?: number | null;
  dispatchedResources?: number | null;
  generatedAt: string;
}

export interface ShortageProposalItem {
  requestId: string;
  category: string;
  urgency: string;
  requestedQuantity: number;
  availableAtStart: number;
  priorityPosition: number;
  proposedPartialFulfilment: number;
  explanation: string;
}

export interface ShortageProposal {
  resourceTypeId: string;
  availableQuantity: number;
  items: ShortageProposalItem[];
  humanApprovalRequired: boolean;
}

export interface Recommendation {
  id: string;
  type: string;
  targetId: string;
  payloadJson: string;
  confidence: number;
  rationale: string;
  source: string;
  status?: string;
  createdAt: string;
}

export interface NotificationInbox {
  items: Array<{ id: string; templateKey: string; readAt?: string | null }>;
  unreadCount: number;
  totalCount: number;
}

export interface ResourceType {
  id: string;
  name: string;
}

export function queryString(
  params: Record<string, string | number | boolean | null | undefined>
): string {
  const search = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") {
      search.set(key, String(value));
    }
  });
  const value = search.toString();
  return value ? `?${value}` : "";
}

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "https://localhost:7001";

function tokenHeaders(): HeadersInit {
  const token = typeof window !== "undefined" ? localStorage.getItem("accessToken") : null;
  return token ? { Authorization: `Bearer ${token}` } : {};
}

async function problemMessage(response: Response): Promise<string> {
  const problem = await response.json().catch(() => null) as
    | { message?: string; code?: string }
    | null;
  return problem?.message ?? problem?.code ?? response.statusText;
}

export const casesApi = {
  publicAlerts: () => api<AlertItem[]>("/public/alerts"),
  publicServiceCentres: () => api<ServiceCentre[]>("/public/service-centres"),
  publicShelters: () => api<ShelterSummary[]>("/shelters/public"),

  household: () => api<Household>("/households/me"),
  saveHousehold: (payload: { size: number; accessibilityNeeds?: string; contactPhone?: string }) =>
    api<{ id: string }>("/households/me", {
      method: "PUT",
      body: JSON.stringify(payload)
    }),

  myRequests: (page: number, pageSize: number) =>
    api<Paged<RequestListItem>>(`/requests/mine${queryString({ page, pageSize, sortBy: "submittedAt" })}`),
  request: (id: string) => api<RequestDetail>(`/requests/${id}`),
  timeline: (id: string) => api<TimelineEntry[]>(`/requests/${id}/timeline`),
  submitRequest: (payload: CreateRequestPayload) =>
    api<{ id: string }>("/requests", { method: "POST", body: JSON.stringify(payload) }),
  feedback: (id: string, comment: string) =>
    api<{ id: string }>(`/requests/${id}/feedback`, {
      method: "POST",
      body: JSON.stringify({ comment })
    }),

  staffRequests: (params: {
    page: number;
    pageSize: number;
    status?: string;
    urgency?: string;
    category?: string;
    areaId?: string;
    crisisId?: string;
    assignedTo?: string;
    overdue?: boolean;
    q?: string;
    sortBy?: string;
    sortDescending?: boolean;
  }) => api<Paged<RequestListItem>>(`/requests${queryString(params)}`),

  transition: (id: string, to: string, reason?: string, decisionId?: string) =>
    api<void>(`/requests/${id}/transition`, {
      method: "POST",
      body: JSON.stringify({ to, reason: reason || undefined, decisionId: decisionId || undefined })
    }),
  assign: (id: string, assignedToUserId: string) =>
    api<void>(`/requests/${id}/assign`, {
      method: "PATCH",
      body: JSON.stringify({ assignedToUserId })
    }),
  merge: (id: string, targetRequestId: string, reason: string) =>
    api<void>(`/requests/${id}/merge`, {
      method: "POST",
      body: JSON.stringify({ targetRequestId, reason })
    }),

  crises: () => api<Crisis[]>("/crises"),
  createCrisis: (payload: { name: string; type: string; description?: string; startedAt: string }) =>
    api<{ id: string }>("/crises", { method: "POST", body: JSON.stringify(payload) }),
  updateCrisis: (
    id: string,
    payload: { name?: string; type?: string; description?: string; status?: CrisisStatus; endedAt?: string }
  ) => api<void>(`/crises/${id}`, { method: "PATCH", body: JSON.stringify(payload) }),
  areas: (crisisId?: string) => api<Area[]>(`/areas${queryString({ crisisId })}`),
  createArea: (
    crisisId: string,
    payload: { name: string; description?: string; lat: number; lng: number; radiusKm?: number }
  ) => api<{ id: string }>(`/crises/${crisisId}/areas`, { method: "POST", body: JSON.stringify(payload) }),
  crisisSummary: (id: string) => api<CrisisSummary>(`/crises/${id}/summary`),
  createAlert: (crisisId: string, payload: CreateAlertPayload) =>
    api<{ id: string }>(`/crises/${crisisId}/alerts`, {
      method: "POST",
      body: JSON.stringify(payload)
    }),

  kpis: (from: string, to: string) => api<KpiReport>(`/kpis${queryString({ from, to })}`),
  resourceTypes: () => api<ResourceType[]>("/resources/types"),
  shortageProposal: (resourceTypeId: string) =>
    api<ShortageProposal>(`/shortages/proposal${queryString({ resourceTypeId })}`),

  // M3-owned endpoint consumed by D2-10; show an unavailable state when absent.
  recommendationsByTarget: (targetId: string) =>
    api<Recommendation[]>(`/recommendations${queryString({ targetId })}`),

  notifications: () => api<NotificationInbox>("/notifications"),

  uploadAttachment: async (requestId: string, file: File): Promise<void> => {
    const body = new FormData();
    body.append("file", file);
    const response = await fetch(`${API_BASE}/api/v1/requests/${requestId}/attachments`, {
      method: "POST",
      headers: tokenHeaders(),
      body
    });
    if (!response.ok) {
      throw new Error(await problemMessage(response));
    }
  },

  downloadAttachment: async (requestId: string, attachmentId: string, fileName: string): Promise<void> => {
    const response = await fetch(`${API_BASE}/api/v1/requests/${requestId}/attachments/${attachmentId}`, {
      headers: tokenHeaders()
    });
    if (!response.ok) {
      throw new Error(await problemMessage(response));
    }
    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    // Defer revocation so the browser has consumed the blob URL.
    window.setTimeout(() => URL.revokeObjectURL(url), 0);
  }
};
