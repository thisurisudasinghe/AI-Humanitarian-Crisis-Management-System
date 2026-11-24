/**
 * Donor portal presentation helpers.
 *
 * Everything here is derived from backend fields. No progress stage, impact
 * figure, category, location or status is invented: where SAFEWAY has no data,
 * these helpers deliberately return "pending" or an empty result.
 */
import { ApiError } from "@/lib/api";
import type { CampaignDto, DonationDto, DonationStatus } from "@/lib/donations";
import { formatMoney } from "@/lib/donations";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { formatDate, type Locale } from "@/lib/i18n/core";

type T = (key: string, vars?: Record<string, string | number>) => string;

export const SUPPORTED_CURRENCIES = ["LKR", "USD"] as const;
export type SupportedCurrency = (typeof SUPPORTED_CURRENCIES)[number];

export const SUGGESTED_AMOUNTS: Record<SupportedCurrency, number[]> = {
  LKR: [500, 1000, 2500, 5000],
  USD: [10, 25, 50, 100]
};

/** Supply categories shown in the donation builder. */
export const DONOR_CATEGORIES = [
  { value: "Food", labelKey: "donorX.categories.Food", icon: "box" },
  { value: "DrinkingWater", labelKey: "donorX.categories.DrinkingWater", icon: "droplet" },
  { value: "Clothing", labelKey: "donorX.categories.Clothing", icon: "layers" },
  { value: "MedicalSupplies", labelKey: "donorX.categories.MedicalSupplies", icon: "medical" },
  { value: "ShelterSupplies", labelKey: "donorX.categories.ShelterSupplies", icon: "home" },
  { value: "HygieneSupplies", labelKey: "donorX.categories.HygieneSupplies", icon: "shield" },
  { value: "EducationalSupplies", labelKey: "donorX.categories.EducationalSupplies", icon: "file" },
  { value: "Other", labelKey: "donorX.categories.Other", icon: "box" }
] as const;

/** Units shown in the donation builder. "Other" keeps custom text unchanged. */
export const DONOR_UNITS = [
  { value: "Pieces", labelKey: "donorX.units.Pieces" },
  { value: "Kg", labelKey: "donorX.units.Kg" },
  { value: "Litres", labelKey: "donorX.units.Litres" },
  { value: "Bottles", labelKey: "donorX.units.Bottles" },
  { value: "Packets", labelKey: "donorX.units.Packets" },
  { value: "Boxes", labelKey: "donorX.units.Boxes" },
  { value: "Sets", labelKey: "donorX.units.Sets" },
  { value: "Other", labelKey: "donorX.units.Other" }
] as const;

export const DELIVERY_METHODS = ["Drop-off", "Request collection", "To be arranged"] as const;
export type DeliveryMethod = (typeof DELIVERY_METHODS)[number];

export type DonorTimelineState = "completed" | "current" | "pending" | "exceptional";

export interface DonorTimelineStep {
  key: string;
  labelKey: string;
  copyKey: string;
  icon: string;
  state: DonorTimelineState;
  timestamp?: string | null;
}

export function normalizeDonationStatus(status?: string | null): DonationStatus | "Unknown" {
  return status === "Received" || status === "Allocated" || status === "Cancelled"
    ? status
    : "Unknown";
}

export function donationStatusLabel(t: T, status?: string | null): string {
  return t(`donorX.shared.statuses.${normalizeDonationStatus(status)}`);
}

export function donationStatusNote(t: T, status?: string | null): string {
  return t(`donorX.shared.statusNotes.${normalizeDonationStatus(status)}`);
}

export function donationStatusTone(status?: string | null): string {
  const value = normalizeDonationStatus(status);
  if (value === "Allocated") return "green";
  if (value === "Received") return "blue";
  if (value === "Cancelled") return "coral";
  return "muted";
}

/**
 * Four honest stages. The server only records Received / Allocated / Cancelled,
 * so "Distributed" can never be marked complete from donor-side data.
 */
export function buildDonationTimeline(donation: DonationDto): DonorTimelineStep[] {
  const status = normalizeDonationStatus(donation.status);
  const steps: DonorTimelineStep[] = [
    {
      key: "submitted",
      labelKey: "donorX.detail.stageSubmittedTitle",
      copyKey: "donorX.detail.stageSubmittedCopy",
      icon: "clipboard",
      state: "completed",
      timestamp: donation.createdAt
    },
    {
      key: "received",
      labelKey: "donorX.detail.stageReceivedTitle",
      copyKey: "donorX.detail.stageReceivedCopy",
      icon: "checkCircle",
      state: "pending",
      timestamp: donation.receivedAt
    },
    {
      key: "allocated",
      labelKey: "donorX.detail.stageAllocatedTitle",
      copyKey: "donorX.detail.stageAllocatedCopy",
      icon: "handHeart",
      state: "pending",
      timestamp: donation.allocations?.[0]?.createdAt ?? null
    },
    {
      key: "distributed",
      labelKey: "donorX.detail.stageDistributedTitle",
      copyKey: "donorX.detail.stageDistributedCopy",
      icon: "box",
      state: "pending",
      timestamp: null
    }
  ];

  if (status === "Cancelled") {
    steps[1].state = "completed";
    steps[2].state = "exceptional";
    return steps;
  }

  if (status === "Received") {
    steps[1].state = "current";
    return steps;
  }

  if (status === "Allocated") {
    steps[1].state = "completed";
    steps[2].state = "completed";
    return steps;
  }

  steps[1].state = "exceptional";
  return steps;
}

export function donationIcon(donation: Pick<DonationDto, "donationType">): string {
  return donation.donationType === "Monetary" ? "heart" : "box";
}

/** Campaigns have no category field in the backend, so use a neutral icon. */
export function campaignIcon(_campaign?: Pick<CampaignDto, "id">): string {
  return "megaphone";
}

export function categoryIcon(value?: string | null): string {
  const category = DONOR_CATEGORIES.find((option) => option.value === value);
  return category?.icon ?? "box";
}

export function categoryLabel(t: T, value?: string | null): string {
  if (!value) return t("donorX.shared.notProvided");
  const category = DONOR_CATEGORIES.find((option) => option.value === value);
  if (!category) return value; // custom text is shown exactly as entered
  return t(category.labelKey);
}

export function unitLabel(t: T, value?: string | null): string {
  if (!value) return t("donorX.shared.notProvided");
  const unit = DONOR_UNITS.find((option) => option.value === value);
  return unit ? t(unit.labelKey) : value; // custom units are never rewritten
}

export function donationSummary(t: T, donation: DonationDto): string {
  if (donation.donationType === "Monetary") {
    return formatMoney(donation.amount, donation.currency);
  }

  if (donation.items?.length) {
    const names = donation.items.slice(0, 3).map((item) => item.name).join(", ");
    const extra = donation.items.length > 3 ? ` +${donation.items.length - 3}` : "";
    return `${names}${extra}`;
  }

  return donation.itemDescription || t("donorX.shared.notProvided");
}

export function deliveryLabel(t: T, value?: string | null): string {
  if (!value) return t("donorX.shared.notProvided");
  const keys: Record<string, string> = {
    "Drop-off": "donorX.donate.deliveryDropoff",
    "Request collection": "donorX.donate.deliveryPickup",
    "To be arranged": "donorX.donate.deliveryTbc"
  };
  const key = keys[value];
  return key ? t(key) : value; // unknown values are custom text and stay untouched
}

export function hasValidPoint(lat?: number | null, lng?: number | null): boolean {
  return (
    typeof lat === "number" &&
    typeof lng === "number" &&
    Number.isFinite(lat) &&
    Number.isFinite(lng) &&
    lat >= -90 &&
    lat <= 90 &&
    lng >= -180 &&
    lng <= 180
  );
}

export function mapLink(lat: number, lng: number): string {
  return `https://www.openstreetmap.org/?mlat=${lat}&mlng=${lng}#map=14/${lat}/${lng}`;
}

/** ASP.NET may return UTC timestamps without a suffix; treat them as UTC. */
export function backendTimestamp(value?: string | null): string | null {
  if (!value) return null;
  const trimmed = value.trim();
  if (!trimmed) return null;
  return /(?:z|[+-]\d{2}:\d{2})$/i.test(trimmed) ? trimmed : `${trimmed}Z`;
}

export function formatDonationDate(value: string | null | undefined, locale: Locale): string {
  const timestamp = backendTimestamp(value);
  if (!timestamp) return "—";
  return formatDate(timestamp, locale, { dateStyle: "medium" });
}

/**
 * Backend expiry values are UTC calendar dates. Formatting a bare date in the
 * browser's local zone can display the previous day, so keep date-only values
 * anchored to UTC.
 */
export function formatDonationCalendarDate(value: string | null | undefined, locale: Locale): string {
  if (!value) return "—";
  const dateOnly = /^\d{4}-\d{2}-\d{2}$/.test(value.trim());
  return formatDate(
    dateOnly ? `${value.trim()}T00:00:00Z` : backendTimestamp(value),
    locale,
    { dateStyle: "medium", timeZone: "UTC" }
  );
}

/** The same UTC calendar date the backend uses for expiry validation. */
export function utcDateInputValue(date = new Date()): string {
  return date.toISOString().slice(0, 10);
}

export function newIdempotencyKey(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return `donor-${crypto.randomUUID()}`;
  }
  return `donor-${Date.now()}-${Math.random().toString(36).slice(2, 12)}`;
}

/**
 * Backend validation strings are stable but English. Map the donor-flow ones
 * to translated copy and use the localized generic message for anything else.
 */
export function localizeDonationError(error: unknown, t: T): string {
  if (error instanceof ApiError) {
    if (error.code === "IDEMPOTENCY_KEY_REUSED") {
      return t("donorX.donate.duplicateError");
    }

    const messages = Object.values(error.details.errors ?? {})
      .flat()
      .filter(Boolean)
      .map(String);

    for (const message of messages) {
      const lower = message.toLowerCase();
      if (lower.includes("amount greater than zero")) return t("donorX.validation.positiveAmount");
      if (lower.includes("three-letter code")) return t("donorX.validation.currencyCode");
      if (lower.includes("every item needs a name")) return t("donorX.validation.itemName");
      if (lower.includes("quantity greater than zero")) return t("donorX.validation.quantity");
      if (lower.includes("expired")) return t("donorX.validation.expiryPast");
      if (lower.includes("requires an item description")) return t("donorX.validation.atLeastOneItem");
      if (lower.includes("coordinates")) return t("donorX.validation.locationInvalid");
    }

    if (error.status === 422 || error.code === "VALIDATION_FAILED") {
      return t("apiErrors.validation");
    }
  }

  return localizedErrorMessage(error, t);
}

export interface CurrencyBucket {
  currency: string;
  monetary: number;
  materialEstimated: number;
  allocated: number;
  count: number;
}

/** Groups a donor's own records by currency; values in different currencies are never added together. */
export function groupDonationsByCurrency(donations: DonationDto[]): CurrencyBucket[] {
  const buckets = new Map<string, CurrencyBucket>();
  for (const donation of donations) {
    const currency = donation.currency?.trim().toUpperCase() || "—";
    const bucket = buckets.get(currency) ?? {
      currency,
      monetary: 0,
      materialEstimated: 0,
      allocated: 0,
      count: 0
    };
    if (donation.donationType === "Monetary") bucket.monetary += Number(donation.amount ?? 0);
    else bucket.materialEstimated += Number(donation.estimatedValue ?? 0);
    bucket.allocated += Number(donation.allocatedAmount ?? 0);
    bucket.count += 1;
    buckets.set(currency, bucket);
  }
  return [...buckets.values()].sort((a, b) => a.currency.localeCompare(b.currency));
}
