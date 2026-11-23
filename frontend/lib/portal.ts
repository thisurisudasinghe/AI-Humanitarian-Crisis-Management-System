/**
 * Authenticated API seam for the shared public portal.
 *
 * Every endpoint below is caller-scoped by the server. This module only maps
 * request and response shapes; role checks and data ownership stay in the API.
 */
import { api, apiJson, type AuthResponse } from "@/lib/api";
import type { PublicPurpose } from "@/lib/roles";
import type {
  CampaignDto,
  DonationDto,
  DonationReportDto,
  DonorDto,
  PagedResult
} from "@/lib/donations";

export type { CampaignDto, DonationDto, DonationReportDto, DonorDto, PagedResult };

export interface VolunteerProfile {
  id: string;
  userId: string;
  skillsJson: string;
  languagesJson: string;
  verificationStatus: string;
  active: boolean;
  lat?: number | null;
  lng?: number | null;
}

export interface VolunteerAvailability {
  id: string;
  volunteerId: string;
  startsAt: string;
  endsAt: string;
}

export interface VolunteerTask {
  id: string;
  requestId?: string | null;
  title: string;
  requiredSkillsJson: string;
  lat?: number | null;
  lng?: number | null;
  scheduledAt?: string | null;
  status: string;
  volunteerId?: string | null;
  completedAt?: string | null;
}

export interface VolunteerPortalData {
  profile: VolunteerProfile | null;
  availability: VolunteerAvailability[];
  completedTasks: number;
  opportunities: VolunteerTask[];
}

export interface OperationsPaged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface RecordDonationInput {
  donationType: "Monetary" | "Material";
  currency: string;
  amount?: number;
  itemDescription?: string;
  quantity?: number;
  estimatedValue?: number | null;
  notes?: string;
}

/** Adds a safe public role. Fresh tokens are returned with the updated claims. */
export function enrollPublicRole(purpose: PublicPurpose): Promise<AuthResponse> {
  return apiJson<AuthResponse>("/api/v1/portal/enroll", "POST", { purpose });
}

export function getVolunteerPortal(): Promise<VolunteerPortalData> {
  return api<VolunteerPortalData>("/api/v1/portal/volunteer/status");
}

export function getMyVolunteerTasks(): Promise<OperationsPaged<VolunteerTask>> {
  return api<OperationsPaged<VolunteerTask>>("/api/v1/volunteer-tasks/mine");
}

export function saveVolunteerProfile(input: {
  skillsJson: string;
  languagesJson: string;
  lat: number | null;
  lng: number | null;
  active: boolean;
}): Promise<VolunteerProfile> {
  return api<VolunteerProfile>("/api/v1/volunteers/profile", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(input)
  });
}

export function addVolunteerAvailability(input: {
  startsAt: string;
  endsAt: string;
}): Promise<unknown> {
  return api<unknown>("/api/v1/volunteers/availability", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(input)
  });
}

export function respondToTask(taskId: string, accept: boolean): Promise<unknown> {
  return api<unknown>(`/api/v1/volunteer-tasks/${encodeURIComponent(taskId)}/respond`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ accept })
  });
}

export function completeTask(taskId: string): Promise<unknown> {
  return api<unknown>(`/api/v1/volunteer-tasks/${encodeURIComponent(taskId)}/complete`, {
    method: "POST"
  });
}

export function getDonorProfile(): Promise<DonorDto> {
  return api<DonorDto>("/api/v1/portal/donor/profile");
}

export function getDonorCampaigns(page = 1, pageSize = 12): Promise<PagedResult<CampaignDto>> {
  return api<PagedResult<CampaignDto>>(
    `/api/v1/portal/donor/campaigns?page=${page}&pageSize=${pageSize}`
  );
}

export function getDonorDonations(page = 1, pageSize = 20): Promise<PagedResult<DonationDto>> {
  return api<PagedResult<DonationDto>>(
    `/api/v1/portal/donor/donations?page=${page}&pageSize=${pageSize}`
  );
}

/** Records an already-confirmed donation. No payment is processed here. */
export function recordDonation(input: RecordDonationInput): Promise<DonationDto> {
  return apiJson<DonationDto>("/api/v1/portal/donor/donations", "POST", input);
}

export function getDonorTransparency(from: string, to: string): Promise<DonationReportDto> {
  const params = new URLSearchParams({ from, to });
  return api<DonationReportDto>(`/api/v1/portal/donor/transparency?${params.toString()}`);
}
