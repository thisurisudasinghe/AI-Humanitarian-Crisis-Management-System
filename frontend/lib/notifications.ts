import { api } from "./api";

export const NotificationChannels = ["InApp", "Email"] as const;

export const NotificationStatuses = ["Pending", "Sent", "Failed"] as const;

export type NotificationChannel = (typeof NotificationChannels)[number];
export type NotificationStatus = (typeof NotificationStatuses)[number];

export interface NotificationDto {
  id: string;
  recipientUserId: string;
  channel: NotificationChannel;
  templateKey: string;
  subject?: string | null;
  body?: string | null;
  status: NotificationStatus;
  sentAt?: string | null;
  readAt?: string | null;
  isRead: boolean;
  requestId?: string | null;
  createdAt: string;
}

export interface NotificationInboxDto {
  items: NotificationDto[];
  unreadCount: number;
  totalCount: number;
}

/** Lists the caller's in-app notifications, newest first. */
export function getInbox(options: { unreadOnly?: boolean; limit?: number } = {}) {
  const params = new URLSearchParams();

  if (options.unreadOnly) {
    params.set("unreadOnly", "true");
  }

  if (options.limit) {
    params.set("limit", String(options.limit));
  }

  const query = params.toString();

  return api<NotificationInboxDto>(
    `/api/v1/notifications${query ? `?${query}` : ""}`
  );
}

/** Marks a single notification as read. */
export function markRead(id: string) {
  return api<NotificationDto>(`/api/v1/notifications/${id}/read`, {
    method: "PATCH"
  });
}

/** Marks every unread notification as read and returns how many changed. */
export function markAllRead() {
  return api<{ updated: number }>("/api/v1/notifications/read-all", {
    method: "POST"
  });
}
