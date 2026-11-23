import {
  formatDate as formatDateForLocale,
  formatNumber as formatNumberForLocale,
  resolveLocale,
  type Locale
} from "./i18n/core";

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/** Shape returned by Cms.Application.DTOs.Common.PagedResult<T> (v1 APIs). */
export interface CmsPagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

export interface UserSummary {
  id: string;
  fullName: string;
  email?: string | null;
  organisationId?: string | null;
  organisationName?: string | null;
  roles: string[];
  status: string;
  isActive: boolean;
  lastLoginAt?: string | null;
}

export interface OrganisationDto {
  id: string;
  name: string;
  organisationType: string;
  registrationNumber?: string | null;
  contactEmail?: string | null;
  contactPhone?: string | null;
  address?: string | null;
  isActive: boolean;
}

export interface SystemSettingDto {
  id: string;
  key: string;
  value: string;
  description?: string | null;
}

export interface AuditEventDto {
  id: string;
  action: string;
  entityType: string;
  entityId?: string | null;
  actorUserId?: string | null;
  details?: string | null;
  ipAddress?: string | null;
  createdAt: string;
}

export interface KpiAreaBreakdown {
  areaId: string;
  areaName: string;
  count: number;
}

import type { DonationCurrencyTotalDto } from "./donations";

export interface KpiRoleCount {
  role: string;
  count: number;
}

export interface KpiReportDto {
  from: string;
  to: string;
  crisisEventId?: string | null;
  areaId?: string | null;
  generatedAt: string;
  unresolvedRequestCount: number;
  averageResponseTimeHours?: number | null;
  resourceUtilisationPercent?: number | null;
  reservedResources?: number | null;
  dispatchedResources?: number | null;
  shelterOccupancyPercent?: number | null;
  sheltersAtCapacity?: number | null;
  aiOverrideRatePercent?: number | null;
  totalNotificationCount: number;
  sentNotificationCount: number;
  failedNotifications: number;
  deadLetterCount: number;
  totalUserCount: number;
  activeUserCount: number;
  newUserCount: number;
  usersByRole: KpiRoleCount[];
  auditEventCount: number;
  activeCampaignCount: number;
  donationTotals: DonationCurrencyTotalDto[];
  suspiciousDonationCount: number;
  suspiciousAllocationCount: number;
  availableSections: string[];
  pendingSections: string[];
  requestsByArea: KpiAreaBreakdown[];
  sheltersByArea: KpiAreaBreakdown[];
  isPlaceholder: boolean;
}

export type OutboxMessageStatus =
  | "Pending"
  | "Processing"
  | "Processed"
  | "Failed"
  | "DeadLettered";

export interface OutboxMessageDto {
  id: string;
  type: string;
  entityId: string;
  status: OutboxMessageStatus;
  retryCount: number;
  nextRetryAt?: string | null;
  processedAt?: string | null;
  errorMessage?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export const OrganisationTypes = [
  "Government",
  "NonGovernmentalOrganisation",
  "UnitedNationsAgency",
  "Donor",
  "PrivateSector",
  "CommunityBasedOrganisation",
  "Other"
] as const;

export const ConfigKeys = [
  "SlaHours",
  "ReorderThreshold",
  "ServiceTypes",
  "RequestCategories",
  "UrgencyThresholds",
  "SuspiciousDonationAmount"
] as const;

export function formatDate(value?: string | null, locale?: Locale): string {
  const resolved = locale ?? (typeof document === "undefined" ? "en" : resolveLocale(document.documentElement.lang));
  return formatDateForLocale(value, resolved);
}

export function formatMetric(value?: number | null, suffix = ""): string {
  if (value === null || value === undefined) return "—";
  const resolved = typeof document === "undefined" ? "en" : resolveLocale(document.documentElement.lang);
  const formatted = formatNumberForLocale(value, resolved);
  return suffix ? `${formatted}${suffix}` : formatted;
}
