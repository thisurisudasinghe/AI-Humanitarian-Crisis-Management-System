"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import {
  Badge,
  Button,
  Input,
  Modal,
  Select,
  ServerTable,
  Skeleton,
  Toast,
  type Column
} from "@/components/ui";
import { AuthGuard } from "@/components/AuthGuard";
import { AdminNav } from "@/components/safeway/AdminNav";
import { formatDate, type CmsPagedResult } from "@/lib/admin";
import { errorMessage } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import {
  AnnouncementChannels,
  approveAnnouncement,
  createAnnouncement,
  getAnnouncements,
  rejectAnnouncement,
  type AnnouncementChannel,
  type AnnouncementDto
} from "@/lib/announcements";
import { AllRoles, RoleNames, StaffRoles } from "@/lib/roles";

type Notice = { message: string; tone: "error" | "success" } | null;

const AnnouncementApproverRoles = [
  RoleNames.EmergencyCoordinator,
  RoleNames.SystemAdmin
];

const guidPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const pageSize = 20;

const emptyForm = {
  title: "",
  message: "",
  channel: "InApp" as AnnouncementChannel,
  roles: [] as string[],
  userIds: "",
  allActiveUsers: false,
  idempotencyKey: ""
};

function isGuid(value: string): boolean {
  return guidPattern.test(value.trim());
}

function audienceSummary(announcement: AnnouncementDto): string {
  const parts: string[] = [];

  if (announcement.audience.allActiveUsers) {
    parts.push("All active users");
  }

  if (announcement.audience.roles.length > 0) {
    parts.push(announcement.audience.roles.join(", "));
  }

  if (announcement.audience.userIds.length > 0) {
    parts.push(`${announcement.audience.userIds.length} named user(s)`);
  }

  return parts.length > 0 ? parts.join(" · ") : "No audience selected";
}

function StatusBadge({ status }: { status: string }) {
  const className =
    status === "Approved"
      ? "badge--ok"
      : status === "Rejected"
        ? "badge--danger"
        : "badge--muted";

  return <span className={`badge ${className}`}>{status}</span>;
}

export default function AnnouncementsPage() {
  const { can } = useAuth();
  const canApprove = can(AnnouncementApproverRoles);
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<CmsPagedResult<AnnouncementDto> | null>(null);
  const [pending, setPending] = useState<AnnouncementDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [approveTarget, setApproveTarget] = useState<AnnouncementDto | null>(null);
  const [rejectTarget, setRejectTarget] = useState<AnnouncementDto | null>(null);
  const [approveNote, setApproveNote] = useState("");
  const [rejectReason, setRejectReason] = useState("");
  const [notice, setNotice] = useState<Notice>(null);

  const load = useCallback(async () => {
    setLoading(true);

    try {
      const [list, queue] = await Promise.all([
        getAnnouncements(page, pageSize),
        getAnnouncements(1, 100)
      ]);
      setResult(list);
      setPending(queue.items.filter((item) => item.status === "PendingApproval"));
    } catch (error) {
      setNotice({ message: errorMessage(error), tone: "error" });
    } finally {
      setLoading(false);
    }
  }, [page]);

  useEffect(() => {
    void load();
  }, [load]);

  async function submitCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!form.title.trim() || !form.message.trim()) {
      setNotice({ message: "Title and message are required.", tone: "error" });
      return;
    }

    const userIds = form.userIds
      .split(/[\s,;]+/)
      .map((value) => value.trim())
      .filter(Boolean);

    if (userIds.some((id) => !isGuid(id))) {
      setNotice({
        message: "Every recipient user ID must be a valid GUID.",
        tone: "error"
      });
      return;
    }

    if (!form.allActiveUsers && form.roles.length === 0 && userIds.length === 0) {
      setNotice({
        message: "Select at least one audience: all active users, roles or user IDs.",
        tone: "error"
      });
      return;
    }

    setSaving(true);
    setNotice(null);

    try {
      const created = await createAnnouncement({
        title: form.title.trim(),
        message: form.message.trim(),
        channel: form.channel,
        audience: {
          userIds,
          roles: form.roles,
          allActiveUsers: form.allActiveUsers
        },
        idempotencyKey: form.idempotencyKey.trim() || undefined
      });
      setForm(emptyForm);
      setPage(1);
      setNotice({
        message: `${created.title} queued for approval.`,
        tone: "success"
      });
      await load();
    } catch (error) {
      setNotice({ message: errorMessage(error), tone: "error" });
    } finally {
      setSaving(false);
    }
  }

  async function submitApproval() {
    if (!approveTarget) {
      return;
    }

    setSaving(true);
    setNotice(null);

    try {
      const updated = await approveAnnouncement(approveTarget.id, approveNote);
      setApproveTarget(null);
      setApproveNote("");
      setNotice({
        message: `${updated.title} approved for ${updated.recipientCount} recipient(s).`,
        tone: "success"
      });
      await load();
    } catch (error) {
      setNotice({ message: errorMessage(error), tone: "error" });
    } finally {
      setSaving(false);
    }
  }

  async function submitRejection() {
    if (!rejectTarget) {
      return;
    }

    if (!rejectReason.trim()) {
      setNotice({ message: "A rejection reason is required.", tone: "error" });
      return;
    }

    setSaving(true);
    setNotice(null);

    try {
      const updated = await rejectAnnouncement(rejectTarget.id, rejectReason);
      setRejectTarget(null);
      setRejectReason("");
      setNotice({ message: `${updated.title} rejected.`, tone: "success" });
      await load();
    } catch (error) {
      setNotice({ message: errorMessage(error), tone: "error" });
    } finally {
      setSaving(false);
    }
  }

  const columns: Column<AnnouncementDto>[] = [
    {
      key: "title",
      header: "Announcement",
      render: (item) => (
        <div>
          <strong>{item.title}</strong>
          <div className="metric__label">{item.message}</div>
        </div>
      )
    },
    { key: "status", header: "Status", render: (item) => <StatusBadge status={item.status} /> },
    {
      key: "channel",
      header: "Channel",
      render: (item) => <Badge>{item.channel}</Badge>
    },
    {
      key: "audience",
      header: "Audience",
      render: (item) => audienceSummary(item)
    },
    {
      key: "recipients",
      header: "Recipients",
      render: (item) => item.recipientCount || "—"
    },
    {
      key: "created",
      header: "Created",
      render: (item) => formatDate(item.createdAt)
    },
    {
      key: "decision",
      header: "Approval",
      render: (item) => (
        <div>
          {item.status === "PendingApproval" ? (
            <span className="metric__label">Awaiting review</span>
          ) : (
            <>
              <div className="metric__label">{item.approvedByUserId ?? "—"}</div>
              <div className="metric__label">
                {formatDate(item.approvedAt)}
                {item.rejectionReason ? ` · ${item.rejectionReason}` : ""}
              </div>
            </>
          )}
        </div>
      )
    }
  ];

  return (
    <AuthGuard allowedRoles={StaffRoles}>
      <div className="page">
        <header className="page__header">
          <div>
            <h1>Announcements</h1>
            <p>
              Compose emergency announcements, then let an authorized coordinator approve or
              reject them before delivery is queued.
            </p>
          </div>
          <Button variant="secondary" onClick={() => void load()} disabled={loading}>
            Refresh
          </Button>
        </header>

        <AdminNav />

        <Toast
          message={notice?.message ?? null}
          tone={notice?.tone ?? "error"}
          onDismiss={() => setNotice(null)}
        />

        <section className="card">
          <div style={{ marginBottom: 16 }}>
            <h3>Create announcement</h3>
            <p className="metric__label">New announcements always start in PendingApproval</p>
          </div>

          <form onSubmit={submitCreate}>
            <div className="filters">
              <Input
                label="Title"
                value={form.title}
                onChange={(event) => setForm({ ...form, title: event.target.value })}
                required
              />
              <Select
                label="Channel"
                value={form.channel}
                onChange={(event) =>
                  setForm({ ...form, channel: event.target.value as AnnouncementChannel })
                }
              >
                {AnnouncementChannels.map((channel) => (
                  <option key={channel} value={channel}>
                    {channel}
                  </option>
                ))}
              </Select>
            </div>

            <label className="field" style={{ marginTop: 16 }}>
              <span className="field__label">Message</span>
              <textarea
                className="input"
                rows={3}
                value={form.message}
                onChange={(event) => setForm({ ...form, message: event.target.value })}
                required
              />
            </label>

            <div className="filters">
              <label className="field">
                <span className="field__label">Audience roles</span>
                <select
                  className="input"
                  multiple
                  size={6}
                  value={form.roles}
                  onChange={(event) =>
                    setForm({
                      ...form,
                      roles: Array.from(event.target.selectedOptions, (option) => option.value)
                    })
                  }
                >
                  {AllRoles.map((role) => (
                    <option key={role} value={role}>
                      {role}
                    </option>
                  ))}
                </select>
                <span className="field__hint">Hold Ctrl/Cmd to select multiple roles.</span>
              </label>

              <label className="field">
                <span className="field__label">Recipient user IDs</span>
                <textarea
                  className="input"
                  rows={6}
                  placeholder="Guid, comma or new-line separated"
                  value={form.userIds}
                  onChange={(event) => setForm({ ...form, userIds: event.target.value })}
                />
                <span className="field__hint">Optional. Each entry must be a valid GUID.</span>
              </label>

              <div>
                <label className="field">
                  <span className="field__label">Broadcast</span>
                  <label style={{ display: "flex", gap: 8, alignItems: "center" }}>
                    <input
                      type="checkbox"
                      checked={form.allActiveUsers}
                      onChange={(event) =>
                        setForm({ ...form, allActiveUsers: event.target.checked })
                      }
                    />
                    All active users
                  </label>
                </label>
                <Input
                  label="Idempotency key (optional)"
                  placeholder="Prevents duplicate creates on retry"
                  value={form.idempotencyKey}
                  onChange={(event) =>
                    setForm({ ...form, idempotencyKey: event.target.value })
                  }
                />
              </div>
            </div>

            <div className="actions">
              <Button type="submit" disabled={saving}>
                {saving ? "Queuing…" : "Queue for approval"}
              </Button>
            </div>
          </form>
        </section>

        <section className="card">
          <div style={{ marginBottom: 16 }}>
            <h3>Pending approval queue</h3>
            <p className="metric__label">
              {canApprove
                ? "Approve to queue delivery, or reject with a reason."
                : "Only Emergency Coordinators and System Admins can approve or reject."}
            </p>
          </div>

          {loading ? (
            <Skeleton rows={3} />
          ) : pending.length === 0 ? (
            <div className="empty-state">No announcements are awaiting approval.</div>
          ) : (
            <div className="table-wrap">
              <table className="table">
                <thead>
                  <tr>
                    <th scope="col">Title</th>
                    <th scope="col">Audience</th>
                    <th scope="col">Channel</th>
                    <th scope="col">Created</th>
                    <th scope="col">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {pending.map((item) => (
                    <tr key={item.id}>
                      <td>
                        <strong>{item.title}</strong>
                        <div className="metric__label">{item.message}</div>
                      </td>
                      <td>{audienceSummary(item)}</td>
                      <td>
                        <Badge>{item.channel}</Badge>
                      </td>
                      <td>{formatDate(item.createdAt)}</td>
                      <td>
                        {canApprove ? (
                          <div className="actions">
                            <Button
                              onClick={() => {
                                setApproveTarget(item);
                                setApproveNote("");
                              }}
                            >
                              Approve
                            </Button>
                            <Button
                              variant="danger"
                              onClick={() => {
                                setRejectTarget(item);
                                setRejectReason("");
                              }}
                            >
                              Reject
                            </Button>
                          </div>
                        ) : (
                          <span className="metric__label">Awaiting an approver</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>

        <section className="card">
          <div style={{ marginBottom: 16 }}>
            <h3>All announcements</h3>
            <p className="metric__label">Latest first, including status and delivery outcome</p>
          </div>

          {loading ? (
            <Skeleton rows={6} />
          ) : (
            <ServerTable
              columns={columns}
              rows={result?.items ?? []}
              page={result?.page ?? page}
              pageSize={result?.pageSize ?? pageSize}
              totalCount={result?.total ?? 0}
              onPageChange={setPage}
              emptyMessage="No announcements have been created."
            />
          )}
        </section>
      </div>

      {approveTarget ? (
        <Modal title="Approve announcement" onClose={() => setApproveTarget(null)}>
          <p>
            Approving <strong>{approveTarget.title}</strong> queues delivery to the selected
            audience. This cannot be undone.
          </p>
          <label className="field">
            <span className="field__label">Note (optional)</span>
            <textarea
              className="input"
              rows={3}
              value={approveNote}
              onChange={(event) => setApproveNote(event.target.value)}
            />
          </label>
          <div className="actions">
            <Button onClick={() => void submitApproval()} disabled={saving}>
              {saving ? "Approving…" : "Approve & queue delivery"}
            </Button>
            <Button variant="secondary" onClick={() => setApproveTarget(null)}>
              Cancel
            </Button>
          </div>
        </Modal>
      ) : null}

      {rejectTarget ? (
        <Modal title="Reject announcement" onClose={() => setRejectTarget(null)}>
          <p>
            Reject <strong>{rejectTarget.title}</strong>? The reason is recorded in the audit
            trail.
          </p>
          <label className="field">
            <span className="field__label">Reason (required)</span>
            <textarea
              className="input"
              rows={3}
              value={rejectReason}
              onChange={(event) => setRejectReason(event.target.value)}
            />
          </label>
          <div className="actions">
            <Button variant="danger" onClick={() => void submitRejection()} disabled={saving}>
              {saving ? "Rejecting…" : "Reject announcement"}
            </Button>
            <Button variant="secondary" onClick={() => setRejectTarget(null)}>
              Cancel
            </Button>
          </div>
        </Modal>
      ) : null}
    </AuthGuard>
  );
}
