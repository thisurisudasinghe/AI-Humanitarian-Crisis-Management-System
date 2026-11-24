/**
 * Volunteer-facing helpers.
 *
 * The backend stores skills and languages as JSON string arrays and exposes
 * plain-string statuses. This module keeps the mapping between those backend
 * values and the readable Volunteer Portal experience in one place, and never
 * invents history, hours, impact figures or certification data.
 */
import type { VolunteerProfile, VolunteerTask } from "@/lib/portal";

export type Translator = (key: string, vars?: Record<string, string | number>) => string;

export type VolunteerVerificationStatus = "None" | "Pending" | "Verified" | "Rejected" | "Unknown";
export type VolunteerTaskStatus = "Open" | "Assigned" | "Accepted" | "Completed" | "Unknown";

export const VOLUNTEER_SKILLS = [
  { value: "Food distribution", labelKey: "volunteerX.shared.skill.foodDistribution", icon: "box" },
  { value: "First aid", labelKey: "volunteerX.shared.skill.firstAid", icon: "medical" },
  { value: "Shelter support", labelKey: "volunteerX.shared.skill.shelterSupport", icon: "home" },
  { value: "Rescue support", labelKey: "volunteerX.shared.skill.rescueSupport", icon: "shield" },
  { value: "Transport", labelKey: "volunteerX.shared.skill.transport", icon: "truck" },
  { value: "Community support", labelKey: "volunteerX.shared.skill.communitySupport", icon: "users" },
  { value: "Translation", labelKey: "volunteerX.shared.skill.translation", icon: "megaphone" },
  { value: "Driving", labelKey: "volunteerX.shared.skill.driving", icon: "route" },
  { value: "Medical support", labelKey: "volunteerX.shared.skill.medical", icon: "medical" },
  { value: "Logistics", labelKey: "volunteerX.shared.skill.logistics", icon: "box" }
] as const;

export const VOLUNTEER_LANGUAGES = [
  { value: "English", labelKey: "volunteerX.shared.language.English" },
  { value: "Sinhala", labelKey: "volunteerX.shared.language.Sinhala" },
  { value: "Tamil", labelKey: "volunteerX.shared.language.Tamil" }
] as const;

const LANGUAGE_ALIASES: Record<string, string> = {
  en: "English",
  eng: "English",
  english: "English",
  si: "Sinhala",
  sinh: "Sinhala",
  sinhala: "Sinhala",
  ta: "Tamil",
  tamil: "Tamil"
};

const SKILL_ICONS: Record<string, string> = {
  "food distribution": "box",
  food: "box",
  "first aid": "medical",
  medical: "medical",
  "medical support": "medical",
  "shelter support": "home",
  shelter: "home",
  "rescue support": "shield",
  rescue: "shield",
  transport: "truck",
  driving: "route",
  "community support": "users",
  translation: "megaphone",
  logistics: "box"
};

export function parseBackendArray(value?: string | null): string[] {
  if (!value) return [];
  try {
    const parsed: unknown = JSON.parse(value);
    if (Array.isArray(parsed)) {
      return parsed
        .filter((item): item is string => typeof item === "string")
        .map((item) => item.trim())
        .filter(Boolean);
    }
  } catch {
    // Older records may contain plain comma-separated text. Preserve them.
  }
  return value
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean);
}

export function serializeBackendArray(values: readonly string[]): string {
  const unique = Array.from(
    new Set(values.map((value) => value.trim()).filter(Boolean))
  );
  return JSON.stringify(unique);
}

export function normalizeLanguageValue(value: string): string {
  return LANGUAGE_ALIASES[value.trim().toLowerCase()] ?? value.trim();
}

export function skillLabel(t: Translator, value: string): string {
  const option = VOLUNTEER_SKILLS.find(
    (item) => item.value.toLowerCase() === value.trim().toLowerCase()
  );
  if (!option) return value;
  return t(option.labelKey);
}

export function languageLabel(t: Translator, value: string): string {
  const normalized = normalizeLanguageValue(value);
  const option = VOLUNTEER_LANGUAGES.find(
    (item) => item.value.toLowerCase() === normalized.toLowerCase()
  );
  if (!option) return normalized || value;
  return t(option.labelKey);
}

export function normalizeVerificationStatus(
  status?: string | null,
  hasProfile = true
): VolunteerVerificationStatus {
  if (!hasProfile) return "None";
  const normalized = (status ?? "").trim().toLowerCase();
  if (normalized === "pending") return "Pending";
  if (normalized === "verified") return "Verified";
  if (normalized === "rejected") return "Rejected";
  return "Unknown";
}

export function normalizeTaskStatus(status?: string | null): VolunteerTaskStatus {
  const normalized = (status ?? "").trim().toLowerCase();
  if (normalized === "open") return "Open";
  if (normalized === "assigned") return "Assigned";
  if (normalized === "accepted") return "Accepted";
  if (normalized === "completed") return "Completed";
  return "Unknown";
}

export function verificationTone(status: VolunteerVerificationStatus): string {
  if (status === "Verified") return "green";
  if (status === "Rejected" || status === "Unknown") return "coral";
  if (status === "Pending") return "blue";
  return "muted";
}

export function taskTone(status: VolunteerTaskStatus): string {
  if (status === "Completed") return "green";
  if (status === "Accepted") return "blue";
  if (status === "Assigned") return "amber";
  if (status === "Unknown") return "coral";
  return "muted";
}

export type TimelineState = "completed" | "current" | "pending" | "exceptional";

export interface VolunteerTimelineStep {
  key: string;
  labelKey: string;
  copyKey: string;
  icon: string;
  state: TimelineState;
  timestamp?: string | null;
}

export function buildVerificationTimeline(
  status: VolunteerVerificationStatus
): VolunteerTimelineStep[] {
  const steps: VolunteerTimelineStep[] = [
    {
      key: "profile",
      labelKey: "volunteerX.verifyStage.profileTitle",
      copyKey: "volunteerX.verifyStage.profileCopy",
      icon: "user",
      state: "pending"
    },
    {
      key: "review",
      labelKey: "volunteerX.verifyStage.reviewTitle",
      copyKey: "volunteerX.verifyStage.reviewCopy",
      icon: "clipboard",
      state: "pending"
    },
    {
      key: "verified",
      labelKey: "volunteerX.verifyStage.verifiedTitle",
      copyKey: "volunteerX.verifyStage.verifiedCopy",
      icon: "shield",
      state: "pending"
    }
  ];

  if (status === "None") {
    steps[0].state = "current";
    return steps;
  }
  if (status === "Pending") {
    steps[0].state = "completed";
    steps[1].state = "current";
    return steps;
  }
  if (status === "Verified") {
    steps.forEach((step) => {
      step.state = "completed";
    });
    return steps;
  }
  if (status === "Rejected") {
    steps[0].state = "completed";
    steps[1].state = "exceptional";
    return steps;
  }
  steps[1].state = "exceptional";
  return steps;
}

export function buildTaskTimeline(
  status: VolunteerTaskStatus,
  completedAt?: string | null
): VolunteerTimelineStep[] {
  const steps: VolunteerTimelineStep[] = [
    {
      key: "assigned",
      labelKey: "volunteerX.taskStage.assignedTitle",
      copyKey: "volunteerX.taskStage.assignedCopy",
      icon: "clipboard",
      state: "pending"
    },
    {
      key: "accepted",
      labelKey: "volunteerX.taskStage.acceptedTitle",
      copyKey: "volunteerX.taskStage.acceptedCopy",
      icon: "checkCircle",
      state: "pending"
    },
    {
      key: "progress",
      labelKey: "volunteerX.taskStage.progressTitle",
      copyKey: "volunteerX.taskStage.progressCopy",
      icon: "handHeart",
      state: "pending"
    },
    {
      key: "completed",
      labelKey: "volunteerX.taskStage.completedTitle",
      copyKey: "volunteerX.taskStage.completedCopy",
      icon: "checkCircle",
      state: "pending",
      timestamp: completedAt
    }
  ];

  if (status === "Open") {
    steps[0].state = "current";
    return steps;
  }
  if (status === "Assigned") {
    steps[0].state = "completed";
    steps[1].state = "current";
    return steps;
  }
  if (status === "Accepted") {
    steps[0].state = "completed";
    steps[1].state = "completed";
    steps[2].state = "current";
    return steps;
  }
  if (status === "Completed") {
    steps.forEach((step) => {
      step.state = "completed";
    });
    return steps;
  }
  steps[0].state = "exceptional";
  return steps;
}

export function taskIcon(task: Pick<VolunteerTask, "title" | "requiredSkillsJson">): string {
  const text = `${task.title ?? ""} ${parseBackendArray(task.requiredSkillsJson).join(" ")}`.toLowerCase();
  for (const [needle, icon] of Object.entries(SKILL_ICONS)) {
    if (text.includes(needle)) return icon;
  }
  return "handHeart";
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

/**
 * Backend timestamps are UTC. After a database read, ASP.NET may serialize a
 * `DateTime` without a zone suffix, which JavaScript would otherwise interpret
 * as local time. Treat those values as UTC so every volunteer screen shows the
 * user's local time consistently.
 */
export function backendTimestamp(value?: string | null): string | null {
  if (!value) return null;
  const trimmed = value.trim();
  if (!trimmed) return null;
  return /(?:z|[+-]\d{2}:\d{2})$/i.test(trimmed) ? trimmed : `${trimmed}Z`;
}

export function fromDateTimeLocal(value: string): string | null {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  return date.toISOString();
}

export function toDateTimeLocal(value?: string | null): string {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
}

export function windowsOverlap(
  startsAt: string,
  endsAt: string,
  windows: readonly { startsAt: string; endsAt: string }[]
): boolean {
  const start = new Date(backendTimestamp(startsAt) ?? startsAt).getTime();
  const end = new Date(backendTimestamp(endsAt) ?? endsAt).getTime();
  return windows.some((window) => {
    const existingStart = new Date(backendTimestamp(window.startsAt) ?? window.startsAt).getTime();
    const existingEnd = new Date(backendTimestamp(window.endsAt) ?? window.endsAt).getTime();
    return start < existingEnd && end > existingStart;
  });
}

export function isVerificationIncomplete(profile: VolunteerProfile | null): boolean {
  if (!profile) return true;
  return (
    parseBackendArray(profile.skillsJson).length === 0 &&
    parseBackendArray(profile.languagesJson).length === 0 &&
    !hasValidPoint(profile.lat, profile.lng)
  );
}

export function activeAssignments(tasks: readonly VolunteerTask[]): VolunteerTask[] {
  return tasks
    .filter((task) => {
      const status = normalizeTaskStatus(task.status);
      return status === "Assigned" || status === "Accepted";
    })
    .sort((a, b) => {
      const first = a.scheduledAt ? new Date(backendTimestamp(a.scheduledAt) ?? a.scheduledAt).getTime() : Number.MAX_SAFE_INTEGER;
      const second = b.scheduledAt ? new Date(backendTimestamp(b.scheduledAt) ?? b.scheduledAt).getTime() : Number.MAX_SAFE_INTEGER;
      return first - second;
    });
}

export function completedAssignments(tasks: readonly VolunteerTask[]): VolunteerTask[] {
  return tasks
    .filter((task) => normalizeTaskStatus(task.status) === "Completed")
    .sort((a, b) => {
      const first = a.completedAt ? new Date(backendTimestamp(a.completedAt) ?? a.completedAt).getTime() : 0;
      const second = b.completedAt ? new Date(backendTimestamp(b.completedAt) ?? b.completedAt).getTime() : 0;
      return second - first;
    });
}
