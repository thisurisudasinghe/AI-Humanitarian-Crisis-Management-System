import type { PagedResult } from "./admin";

export type { PagedResult };

export const DonorTypes = [
  "Individual",
  "Organisation",
  "Corporate",
  "Foundation",
  "Government"
] as const;

export const CampaignStatuses = [
  "Draft",
  "Active",
  "Completed",
  "Cancelled"
] as const;

export const DonationTypes = ["Monetary", "Material"] as const;

export const DonationStatuses = ["Received", "Allocated", "Cancelled"] as const;

export type DonorType = (typeof DonorTypes)[number];
export type CampaignStatus = (typeof CampaignStatuses)[number];
export type DonationType = (typeof DonationTypes)[number];
export type DonationStatus = (typeof DonationStatuses)[number];

export interface DonorDto {
  id: string;
  name: string;
  donorType: DonorType;
  contactEmail?: string | null;
  contactPhone?: string | null;
  isAnonymous: boolean;
  isActive: boolean;
  donationCount: number;
  totalContributed: number;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CampaignDto {
  id: string;
  name: string;
  description?: string | null;
  goalAmount: number;
  currency: string;
  startsAt: string;
  endsAt?: string | null;
  status: CampaignStatus;
  raisedAmount: number;
  spentAmount: number;
  remainingAmount: number;
  progressPercent: number;
  donationCount: number;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CampaignExpenseDto {
  id: string;
  campaignId: string;
  description: string;
  amount: number;
  currency: string;
  spentAt: string;
  createdAt: string;
}

export interface DonationAllocationDto {
  id: string;
  donationId: string;
  campaignId: string;
  campaignName: string;
  amount: number;
  note?: string | null;
  isSuspicious: boolean;
  suspiciousReason?: string | null;
  createdAt: string;
}

export interface DonationDto {
  id: string;
  reference: string;
  donorId: string;
  donorName: string;
  donationType: DonationType;
  amount?: number | null;
  currency: string;
  itemDescription?: string | null;
  quantity?: number | null;
  estimatedValue?: number | null;
  allocatableValue?: number | null;
  allocatedAmount: number;
  unallocatedAmount?: number | null;
  receivedAt: string;
  status: DonationStatus;
  isSuspicious: boolean;
  suspiciousReason?: string | null;
  notes?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  allocations: DonationAllocationDto[];
}

export interface DonationCurrencyTotalDto {
  currency: string;
  monetaryTotal: number;
  materialEstimatedTotal: number;
  allocatedTotal: number;
  spentTotal: number;
  donationCount: number;
  donorCount: number;
  suspiciousCount: number;
}

export interface CampaignReportLineDto {
  campaignId: string;
  name: string;
  status: CampaignStatus;
  currency: string;
  goalAmount: number;
  raisedAmount: number;
  spentAmount: number;
  remainingAmount: number;
  progressPercent: number;
}

export interface DonationReportDto {
  from: string;
  to: string;
  generatedAt: string;
  totalDonationCount: number;
  totalDonorCount: number;
  suspiciousDonationCount: number;
  suspiciousAllocationCount: number;
  currencyTotals: DonationCurrencyTotalDto[];
  campaigns: CampaignReportLineDto[];
}

/** Formats an amount without assuming the currency code is in the runtime's ICU data. */
export function formatMoney(value: number | null | undefined, currency: string): string {
  if (value === null || value === undefined) {
    return "—";
  }

  const amount = new Intl.NumberFormat(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  }).format(value);

  return `${amount} ${currency}`;
}
