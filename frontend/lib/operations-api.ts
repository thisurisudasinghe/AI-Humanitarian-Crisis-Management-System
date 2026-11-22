// Member 4 operations API adapter for the integrated SAFEWAY frontend.
// Reuses the main application's access-token refresh and ProblemDetails handling.
import { api as coreApi, ApiError } from "@/lib/api";

export interface Paged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const normalized = path.startsWith("/") ? path : `/${path}`;
  const headers = new Headers(init.headers);
  if (init.body && !(init.body instanceof FormData) && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }
  return coreApi<T>(`/api/v1${normalized}`, { ...init, headers });
}

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const details = error.details;
    const fieldErrors = details.errors
      ? Object.values(details.errors).flat().filter(Boolean).join(" ")
      : "";
    const message = fieldErrors || details.detail || details.title || error.message;
    return details.code ? `${message} (${details.code})` : message;
  }
  if (error instanceof Error) return error.message;
  return "Something went wrong. Please try again.";
}

export function newIdempotencyKey(): string {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) return crypto.randomUUID();
  return `alloc-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}
