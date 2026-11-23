import { ApiError } from "@/lib/api";
import type { Vars } from "./core";

type Translator = (key: string, vars?: Vars) => string;

const CODE_KEYS: Record<string, string> = {
  VALIDATION_FAILED: "apiErrors.validation",
  NOT_FOUND: "apiErrors.notFound",
  FORBIDDEN: "apiErrors.forbidden",
  UNAUTHORIZED: "apiErrors.unauthorized",
  CONFLICT: "apiErrors.conflict",
  DOMAIN_ERROR: "apiErrors.domain",
  REQUEST_FAILED: "apiErrors.generic",
  GEOCODE_NOT_FOUND: "apiErrors.geocodeNotFound",
  ADMIN_SELF_LOCKOUT: "apiErrors.adminSelfLockout",
  ADMIN_LAST_ADMIN: "apiErrors.adminLastAdmin",
  SHELTER_FULL: "apiErrors.shelterFull",
  SHELTER_CAPACITY_BELOW_OCCUPANCY: "apiErrors.shelterCapacityBelowOccupancy"
};

const STATUS_KEYS: Record<number, string> = {
  401: "apiErrors.unauthorized",
  403: "apiErrors.forbidden",
  404: "apiErrors.notFound",
  409: "apiErrors.conflict",
  413: "apiErrors.payloadTooLarge",
  422: "apiErrors.validation",
  429: "apiErrors.rateLimited"
};

/**
 * Maps stable API problem codes/statuses to localized UI messages.
 * Unknown codes keep their original API detail; no trace/internal fields are exposed.
 */
export function localizedErrorMessage(error: unknown, t: Translator): string {
  if (error instanceof ApiError) {
    const fieldErrors = Object.values(error.details.errors ?? {})
      .flat()
      .filter(Boolean);

    if (fieldErrors.length > 0) return fieldErrors.join(" ");

    const key = CODE_KEYS[error.code] ?? STATUS_KEYS[error.status];
    if (key) return t(key);

    return error.message || t("apiErrors.generic");
  }

  if (error instanceof Error) return error.message || t("apiErrors.generic");
  return t("apiErrors.generic");
}
