export const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5210";

const REFRESH_TOKEN_KEY = "cms.refreshToken";
export const AUTH_INVALIDATED_EVENT = "cms:auth-invalidated";
let accessToken: string | null = null;
let refreshPromise: Promise<string | null> | null = null;

export interface ProblemDetails {
  status?: number;
  title?: string;
  detail?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[] | string>;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  userId: string;
  fullName: string;
  email: string;
  roles: string[];
  isEmailVerified: boolean;
}

export interface CurrentUser {
  userId: string;
  fullName: string;
  email?: string | null;
  roles: string[];
  isEmailVerified: boolean;
}

export interface EmailVerificationResponse {
  isEmailVerified: boolean;
  alreadyVerified: boolean;
  message: string;
}

export const EMAIL_VERIFICATION_REQUIRED = "EMAIL_VERIFICATION_REQUIRED";

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly details: ProblemDetails;

  constructor(status: number, details: ProblemDetails) {
    super(
      details.detail ??
        details.title ??
        (details.code ? `Request failed (${details.code})` : "Request failed")
    );
    this.name = "ApiError";
    this.status = status;
    this.code = details.code ?? "REQUEST_FAILED";
    this.details = details;
  }
}

export function setTokens(auth: AuthResponse): void {
  accessToken = auth.accessToken;
  if (typeof window !== "undefined") {
    window.sessionStorage.setItem(REFRESH_TOKEN_KEY, auth.refreshToken);
  }
}

export function setAccessToken(token: string): void {
  accessToken = token;
}

export function clearTokens(): void {
  accessToken = null;
  if (typeof window !== "undefined") {
    window.sessionStorage.removeItem(REFRESH_TOKEN_KEY);
  }
}

export function getAccessToken(): string | null {
  return accessToken;
}

export function getRefreshToken(): string | null {
  if (typeof window === "undefined") {
    return null;
  }

  return window.sessionStorage.getItem(REFRESH_TOKEN_KEY);
}

/** Clear credentials and notify AuthGuard-backed pages to force a re-login. */
export function invalidateAuth(): void {
  clearTokens();
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event(AUTH_INVALIDATED_EVENT));
  }
}

function jsonHeaders(): HeadersInit {
  return {
    "Content-Type": "application/json",
    Accept: "application/json"
  };
}

async function toApiError(response: Response): Promise<ApiError> {
  let details: ProblemDetails = {};

  try {
    details = (await response.json()) as ProblemDetails;
  } catch {
    details = {
      status: response.status,
      title: response.statusText || "Request failed"
    };
  }

  return new ApiError(response.status, details);
}

async function send(path: string, init: RequestInit, authenticated: boolean) {
  const headers = new Headers(init.headers);

  if (!headers.has("Accept")) {
    headers.set("Accept", "application/json");
  }

  if (authenticated && accessToken && !headers.has("Authorization")) {
    headers.set("Authorization", `Bearer ${accessToken}`);
  }

  return fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers,
    credentials: "omit"
  });
}

export async function refreshAccessToken(): Promise<string | null> {
  // Single-flight: parallel 401s must share one rotation, otherwise the second
  // call presents an already-rotated token and trips reuse detection.
  if (!refreshPromise) {
    refreshPromise = performRefresh().finally(() => {
      refreshPromise = null;
    });
  }

  return refreshPromise;
}

async function performRefresh(): Promise<string | null> {
  const refreshToken = getRefreshToken();
  if (!refreshToken) {
    return null;
  }

  const response = await send(
    "/api/v1/auth/refresh",
    {
      method: "POST",
      headers: jsonHeaders(),
      body: JSON.stringify({ refreshToken })
    },
    false
  );

  if (!response.ok) {
    invalidateAuth();
    return null;
  }

  const auth = (await response.json()) as AuthResponse;
  setTokens(auth);
  return auth.accessToken;
}

export async function api<T>(
  path: string,
  init: RequestInit = {},
  retryOnUnauthorized = true
): Promise<T> {
  let response = await send(path, init, true);

  if (
    response.status === 401 &&
    retryOnUnauthorized &&
    !path.startsWith("/api/v1/auth/")
  ) {
    const refreshed = await refreshAccessToken();

    if (refreshed) {
      response = await send(path, init, true);
    } else {
      invalidateAuth();
    }
  }

  if (!response.ok) {
    throw await toApiError(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export function apiJson<T>(
  path: string,
  method: "POST" | "PUT" | "PATCH",
  body: unknown
): Promise<T> {
  return api<T>(path, {
    method,
    headers: jsonHeaders(),
    body: JSON.stringify(body)
  });
}

/** Fetches a binary attachment with the same auth/refresh handling as `api`. */
export async function apiBlob(
  path: string,
  init: RequestInit = {},
  retryOnUnauthorized = true
): Promise<{ blob: Blob; fileName: string | null }> {
  let response = await send(path, init, true);

  if (
    response.status === 401 &&
    retryOnUnauthorized &&
    !path.startsWith("/api/v1/auth/")
  ) {
    const refreshed = await refreshAccessToken();

    if (refreshed) {
      response = await send(path, init, true);
    } else {
      invalidateAuth();
    }
  }

  if (!response.ok) {
    throw await toApiError(response);
  }

  const disposition = response.headers.get("Content-Disposition") ?? "";
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);

  return {
    blob: await response.blob(),
    fileName: match ? decodeURIComponent(match[1]) : null
  };
}

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const fieldErrors = Object.values(error.details.errors ?? {})
      .flat()
      .filter(Boolean);

    if (fieldErrors.length > 0) {
      return fieldErrors.join(" ");
    }

    return error.message;
  }

  return error instanceof Error ? error.message : "Something went wrong.";
}
