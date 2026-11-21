/**
 * Typed client for emergency announcements.
 *
 * Creation is intentionally advisory: the API stores every announcement as
 * PendingApproval until an authorized coordinator approves it.
 */
import type { CmsPagedResult } from "./admin";
import { api, apiJson } from "./api";

export const AnnouncementChannels = ["InApp", "Email"] as const;
export type AnnouncementChannel = (typeof AnnouncementChannels)[number];

export const AnnouncementStatuses = [
  "PendingApproval",
  "Approved",
  "Rejected",
  "Dispatched"
] as const;
export type AnnouncementStatus = (typeof AnnouncementStatuses)[number];

export interface AnnouncementAudienceDto {
  userIds: string[];
  roles: string[];
  areaIds: string[];
  allActiveUsers: boolean;
}

export interface AnnouncementDto {
  id: string;
  title: string;
  message: string;
  audience: AnnouncementAudienceDto;
  channel: AnnouncementChannel;
  status: AnnouncementStatus;
  requestedByUserId: string;
  approvedByUserId?: string | null;
  approvedAt?: string | null;
  rejectionReason?: string | null;
  dispatchedAt?: string | null;
  recipientCount: number;
  createdAt: string;
}

export interface CreateAnnouncementInput {
  title: string;
  message: string;
  audience: AnnouncementAudienceDto;
  channel: AnnouncementChannel;
  idempotencyKey?: string;
}

export function getAnnouncements(
  page = 1,
  pageSize = 20
): Promise<CmsPagedResult<AnnouncementDto>> {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize)
  });

  return api<CmsPagedResult<AnnouncementDto>>(
    `/api/v1/announcements?${params.toString()}`
  );
}

export function getAnnouncement(id: string): Promise<AnnouncementDto> {
  return api<AnnouncementDto>(`/api/v1/announcements/${encodeURIComponent(id)}`);
}

export function createAnnouncement(
  input: CreateAnnouncementInput
): Promise<AnnouncementDto> {
  return apiJson<AnnouncementDto>("/api/v1/announcements", "POST", input);
}

export function approveAnnouncement(
  id: string,
  note?: string
): Promise<AnnouncementDto> {
  return apiJson<AnnouncementDto>(
    `/api/v1/announcements/${encodeURIComponent(id)}/approve`,
    "POST",
    { note: note?.trim() ? note.trim() : null }
  );
}

export function rejectAnnouncement(
  id: string,
  reason: string
): Promise<AnnouncementDto> {
  return apiJson<AnnouncementDto>(
    `/api/v1/announcements/${encodeURIComponent(id)}/reject`,
    "POST",
    { reason: reason.trim() }
  );
}
