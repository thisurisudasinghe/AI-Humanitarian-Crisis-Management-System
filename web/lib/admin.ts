export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
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
  failedNotifications?: number | null;
  deadLetterCount?: number | null;
  isPlaceholder: boolean;
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

export function formatDate(value?: string | null): string {
  if (!value) {
    return "—";
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

export function formatMetric(value?: number | null, suffix = ""): string {
  return value === null || value === undefined ? "—" : `${value}${suffix}`;
}
