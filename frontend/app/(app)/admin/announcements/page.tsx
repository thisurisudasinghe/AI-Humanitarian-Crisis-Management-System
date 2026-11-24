"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import { localizedErrorMessage } from "@/lib/i18n/errors";
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
import { formatDate, type CmsPagedResult } from "@/lib/admin";

import { useAuth } from "@/lib/auth";
import { useT } from "@/lib/i18n";
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
type Translator = ReturnType<typeof useT>;

const AnnouncementApproverRoles = [RoleNames.EmergencyCoordinator];

const guidPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const pageSize = 20;

const emptyForm = {
  title: "",
  message: "",
  channel: "InApp" as AnnouncementChannel,
  roles: [] as string[],
  userIds: "",
  areaIds: "",
  allActiveUsers: false,
  idempotencyKey: ""
};

function isGuid(value: string): boolean {
  return guidPattern.test(value.trim());
}

function audienceSummary(announcement: AnnouncementDto, t: Translator): string {
  const parts: string[] = [];

  if (announcement.audience.allActiveUsers) {
    parts.push(t("admin.announcements.audienceAllActiveUsers"));
  }

  if (announcement.audience.roles.length > 0) {
    parts.push(announcement.audience.roles.join(", "));
  }

  if (announcement.audience.userIds.length > 0) {
    parts.push(t("admin.announcements.audienceNamedUsers", {
      count: announcement.audience.userIds.length
    }));
  }

  if (announcement.audience.areaIds.length > 0) {
    parts.push(t("admin.announcements.audienceAreas", {
      count: announcement.audience.areaIds.length
    }));
  }

  return parts.length > 0 ? parts.join(" · ") : t("admin.announcements.audienceNone");
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
  const t = useT();
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
      setNotice({ message: localizedErrorMessage(error, t), tone: "error" });
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
      setNotice({ message: t("admin.announcements.validationRequiredFields"), tone: "error" });
      return;
    }

    const userIds = form.userIds
      .split(/[\s,;]+/)
      .map((value) => value.trim())
      .filter(Boolean);

    const areaIds = form.areaIds
      .split(/[\s,;]+/)
      .map((value) => value.trim())
      .filter(Boolean);

    if (userIds.some((id) => !isGuid(id)) || areaIds.some((id) => !isGuid(id))) {
      setNotice({
        message: t("admin.announcements.validationInvalidGuid"),
        tone: "error"
      });
      return;
    }

    if (
      !form.allActiveUsers &&
      form.roles.length === 0 &&
      userIds.length === 0 &&
      areaIds.length === 0
    ) {
      setNotice({
        message: t("admin.announcements.validationAudienceRequired"),
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
          areaIds,
          allActiveUsers: form.allActiveUsers
        },
        idempotencyKey: form.idempotencyKey.trim() || undefined
      });
      setForm(emptyForm);
      setPage(1);
      setNotice({
        message: t("admin.announcements.created", { title: created.title }),
        tone: "success"
      });
      await load();
    } catch (error) {
      setNotice({ message: localizedErrorMessage(error, t), tone: "error" });
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
        message: t("admin.announcements.approved", {
          title: updated.title,
          count: updated.recipientCount
        }),
        tone: "success"
      });
      await load();
    } catch (error) {
      setNotice({ message: localizedErrorMessage(error, t), tone: "error" });
    } finally {
      setSaving(false);
    }
  }

  async function submitRejection() {
    if (!rejectTarget) {
      return;
    }

    if (!rejectReason.trim()) {
      setNotice({ message: t("admin.announcements.validationRejectionReason"), tone: "error" });
      return;
    }

    setSaving(true);
    setNotice(null);

    try {
      const updated = await rejectAnnouncement(rejectTarget.id, rejectReason);
      setRejectTarget(null);
      setRejectReason("");
      setNotice({ message: t("admin.announcements.rejected", { title: updated.title }), tone: "success" });
      await load();
    } catch (error) {
      setNotice({ message: localizedErrorMessage(error, t), tone: "error" });
    } finally {
      setSaving(false);
    }
  }

  const columns: Column<AnnouncementDto>[] = [
    {
      key: "title",
      header: t("admin.announcements.colAnnouncement"),
      render: (item) => (
        <div>
          <strong>{item.title}</strong>
          <div className="metric__label">{item.message}</div>
        </div>
      )
    },
    { key: "status", header: t("admin.announcements.colStatus"), render: (item) => <StatusBadge status={item.status} /> },
    {
      key: "channel",
      header: t("admin.announcements.colChannel"),
      render: (item) => <Badge>{item.channel}</Badge>
    },
    {
      key: "audience",
      header: t("admin.announcements.colAudience"),
      render: (item) => audienceSummary(item, t)
    },
    {
      key: "recipients",
      header: t("admin.announcements.colRecipients"),
      render: (item) => item.recipientCount || "—"
    },
    {
      key: "created",
      header: t("admin.announcements.colCreated"),
      render: (item) => formatDate(item.createdAt)
    },
    {
      key: "decision",
      header: t("admin.announcements.colApproval"),
      render: (item) => (
        <div>
          {item.status === "PendingApproval" ? (
            <span className="metric__label">{t("admin.announcements.awaitingReview")}</span>
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
            <h1>{t("admin.announcements.title")}</h1>
            <p>{t("admin.announcements.subtitle")}</p>
          </div>
          <Button variant="secondary" onClick={() => void load()} disabled={loading}>
            {t("admin.announcements.refresh")}
          </Button>
        </header>

        <Toast
          message={notice?.message ?? null}
          tone={notice?.tone ?? "error"}
          onDismiss={() => setNotice(null)}
        />

        <section className="card">
          <div style={{ marginBottom: 16 }}>
            <h3>{t("admin.announcements.createTitle")}</h3>
            <p className="metric__label">{t("admin.announcements.createHint")}</p>
          </div>

          <form onSubmit={submitCreate}>
            <div className="filters">
              <Input
                label={t("admin.announcements.fieldTitle")}
                value={form.title}
                onChange={(event) => setForm({ ...form, title: event.target.value })}
                required
              />
              <Select
                label={t("admin.announcements.fieldChannel")}
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
              <span className="field__label">{t("admin.announcements.fieldMessage")}</span>
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
                <span className="field__label">{t("admin.announcements.fieldAudienceRoles")}</span>
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
                <span className="field__hint">{t("admin.announcements.hintMultiSelectRoles")}</span>
              </label>

              <label className="field">
                <span className="field__label">{t("admin.announcements.fieldRecipientUserIds")}</span>
                <textarea
                  className="input"
                  rows={6}
                  placeholder={t("admin.announcements.placeholderGuidList")}
                  value={form.userIds}
                  onChange={(event) => setForm({ ...form, userIds: event.target.value })}
                />
                <span className="field__hint">{t("admin.announcements.hintRecipientIds")}</span>
              </label>

              <label className="field">
                <span className="field__label">{t("admin.announcements.fieldAreaIds")}</span>
                <textarea
                  className="input"
                  rows={6}
                  placeholder={t("admin.announcements.placeholderGuidList")}
                  value={form.areaIds}
                  onChange={(event) => setForm({ ...form, areaIds: event.target.value })}
                />
                <span className="field__hint">{t("admin.announcements.hintAreaIds")}</span>
              </label>

              <div>
                <label className="field">
                  <span className="field__label">{t("admin.announcements.fieldBroadcast")}</span>
                  <label style={{ display: "flex", gap: 8, alignItems: "center" }}>
                    <input
                      type="checkbox"
                      checked={form.allActiveUsers}
                      onChange={(event) =>
                        setForm({ ...form, allActiveUsers: event.target.checked })
                      }
                    />
                    {t("admin.announcements.audienceAllActiveUsers")}
                  </label>
                </label>
                <Input
                  label={t("admin.announcements.fieldIdempotencyKey")}
                  placeholder={t("admin.announcements.placeholderIdempotencyKey")}
                  value={form.idempotencyKey}
                  onChange={(event) =>
                    setForm({ ...form, idempotencyKey: event.target.value })
                  }
                />
              </div>
            </div>

            <div className="actions">
              <Button type="submit" disabled={saving}>
                {saving ? t("admin.announcements.submitQueuing") : t("admin.announcements.submitQueue")}
              </Button>
            </div>
          </form>
        </section>

        <section className="card">
          <div style={{ marginBottom: 16 }}>
            <h3>{t("admin.announcements.pendingTitle")}</h3>
            <p className="metric__label">
              {canApprove
                ? t("admin.announcements.pendingHintApprover")
                : t("admin.announcements.pendingHintViewer")}
            </p>
          </div>

          {loading ? (
            <Skeleton rows={3} />
          ) : pending.length === 0 ? (
            <div className="empty-state">{t("admin.announcements.pendingEmpty")}</div>
          ) : (
            <div className="table-wrap">
              <table className="table">
                <thead>
                  <tr>
                    <th scope="col">{t("admin.announcements.fieldTitle")}</th>
                    <th scope="col">{t("admin.announcements.colAudience")}</th>
                    <th scope="col">{t("admin.announcements.colChannel")}</th>
                    <th scope="col">{t("admin.announcements.colCreated")}</th>
                    <th scope="col">{t("admin.announcements.colActions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {pending.map((item) => (
                    <tr key={item.id}>
                      <td>
                        <strong>{item.title}</strong>
                        <div className="metric__label">{item.message}</div>
                      </td>
                      <td>{audienceSummary(item, t)}</td>
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
                              {t("admin.announcements.approve")}
                            </Button>
                            <Button
                              variant="danger"
                              onClick={() => {
                                setRejectTarget(item);
                                setRejectReason("");
                              }}
                            >
                              {t("admin.announcements.reject")}
                            </Button>
                          </div>
                        ) : (
                          <span className="metric__label">{t("admin.announcements.awaitingApprover")}</span>
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
            <h3>{t("admin.announcements.allTitle")}</h3>
            <p className="metric__label">{t("admin.announcements.allHint")}</p>
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
              emptyMessage={t("admin.announcements.allEmpty")}
            />
          )}
        </section>
      </div>

      {approveTarget ? (
        <Modal title={t("admin.announcements.approveModalTitle")} onClose={() => setApproveTarget(null)}>
          <p>
            {t("admin.announcements.approveModalBodyStart")}{" "}
            <strong>{approveTarget.title}</strong>{" "}
            {t("admin.announcements.approveModalBodyEnd")}
          </p>
          <label className="field">
            <span className="field__label">{t("admin.announcements.noteLabel")}</span>
            <textarea
              className="input"
              rows={3}
              value={approveNote}
              onChange={(event) => setApproveNote(event.target.value)}
            />
          </label>
          <div className="actions">
            <Button onClick={() => void submitApproval()} disabled={saving}>
              {saving ? t("admin.announcements.approving") : t("admin.announcements.approveAndQueue")}
            </Button>
            <Button variant="secondary" onClick={() => setApproveTarget(null)}>
              {t("admin.announcements.cancel")}
            </Button>
          </div>
        </Modal>
      ) : null}

      {rejectTarget ? (
        <Modal title={t("admin.announcements.rejectModalTitle")} onClose={() => setRejectTarget(null)}>
          <p>
            {t("admin.announcements.rejectModalBodyStart")}{" "}
            <strong>{rejectTarget.title}</strong>
            {t("admin.announcements.rejectModalBodyEnd")}
          </p>
          <label className="field">
            <span className="field__label">{t("admin.announcements.reasonLabel")}</span>
            <textarea
              className="input"
              rows={3}
              value={rejectReason}
              onChange={(event) => setRejectReason(event.target.value)}
            />
          </label>
          <div className="actions">
            <Button variant="danger" onClick={() => void submitRejection()} disabled={saving}>
              {saving ? t("admin.announcements.rejecting") : t("admin.announcements.rejectAnnouncement")}
            </Button>
            <Button variant="secondary" onClick={() => setRejectTarget(null)}>
              {t("admin.announcements.cancel")}
            </Button>
          </div>
        </Modal>
      ) : null}
    </AuthGuard>
  );
}
