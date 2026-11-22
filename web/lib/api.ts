// Member 4 / D4-11: thin API client for the operations web app.
// It only transports data. All business rules stay on the server.

const API = process.env.NEXT_PUBLIC_API_BASE_URL ?? "https://localhost:7001";

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  code?: string;
  errors?: Record<string, string[]>;
}

/** A ProblemDetails response (RFC 9457) with the extra `code` used by CMS. */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly problem: ProblemDetails;

  constructor(problem: ProblemDetails, status: number) {
    super(problem.detail ?? problem.title ?? `Request failed (${status})`);
    this.name = "ApiError";
    this.status = status;
    this.code = problem.code ?? "";
    this.problem = problem;
  }

  /** Text safe to show to an end user. Never exposes stack traces or raw bodies. */
  get userMessage(): string {
    const fieldErrors = this.problem.errors
      ? Object.values(this.problem.errors).flat().filter(Boolean).join(" ")
      : "";
    const message = fieldErrors || this.message;
    return this.code ? `${message} (${this.code})` : message;
  }
}

function accessToken(): string | null {
  if (typeof window === "undefined") return null;
  try {
    return window.localStorage.getItem("accessToken");
  } catch {
    return null;
  }
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = accessToken();
  const headers = new Headers(init.headers);
  if (!headers.has("Content-Type") && init.body) headers.set("Content-Type", "application/json");
  if (token && !headers.has("Authorization")) headers.set("Authorization", `Bearer ${token}`);

  const response = await fetch(`${API}/api/v1${path}`, { ...init, headers });
  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as ProblemDetails;
    throw new ApiError(
      { ...problem, status: problem.status ?? response.status },
      response.status,
    );
  }
  if (response.status === 204) return undefined as T;
  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export interface Paged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

/** Fresh per allocation attempt; the same value is reused when retrying. */
export function newIdempotencyKey(): string {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) return crypto.randomUUID();
  return `alloc-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

/** Human-readable ProblemDetails message for any error shown in the UI. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.userMessage;
  if (error instanceof Error) return error.message;
  return "Something went wrong. Please try again.";
}

/** True for errors the UI can explain without a generic "contact support" message. */
export function isExpectedError(error: unknown): boolean {
  return error instanceof ApiError && error.status >= 400 && error.status < 500;
}
