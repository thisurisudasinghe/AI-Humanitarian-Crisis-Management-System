import type { Category, CrisisStatus, RequestStatus, Urgency } from "./casesApi";

export const REQUEST_STATUS_LABELS: Record<RequestStatus, string> = {
  Submitted: "Submitted",
  Verified: "Verified",
  Rejected: "Rejected",
  Triaged: "Triaged",
  Recommended: "Recommended",
  Approved: "Approved",
  Allocated: "Allocated",
  Dispatched: "Dispatched",
  InProgress: "In progress",
  Fulfilled: "Fulfilled",
  Closed: "Closed",
  Escalated: "Escalated",
  Merged: "Merged",
  OnHold: "On hold",
  Reopened: "Reopened"
};

export const CATEGORY_LABELS: Record<Category, string> = {
  Food: "Food",
  Medicine: "Medicine",
  Shelter: "Shelter",
  Transport: "Transport",
  Accessibility: "Accessibility",
  Other: "Other"
};

export const URGENCY_LABELS: Record<Urgency, string> = {
  Low: "Low",
  Medium: "Medium",
  High: "High",
  Critical: "Critical"
};

export const CRISIS_STATUS_LABELS: Record<CrisisStatus, string> = {
  Active: "Active",
  Closed: "Closed"
};
