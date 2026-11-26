"use client";

import { useCallback, useEffect, useState } from "react";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import {
  Badge,
  Button,
  ConfirmDialog,
  Input,
  Modal,
  Select,
  ServerTable,
  Skeleton,
  StatusBadge,
  Toast,
  type Column
} from "@/components/ui";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import { api, apiJson } from "@/lib/api";
import {
  formatDate,
  type OrganisationDto,
  type PagedResult,
  type UserSummary
} from "@/lib/admin";
import { humanizeRole } from "@/lib/format";
import { useT } from "@/lib/i18n";
import { AllRoles, RoleNames } from "@/lib/roles";
import { useDebouncedValue } from "@/lib/useDebouncedValue";

type Notice = { message: string; tone: "error" | "success" } | null;

const emptyForm = {
  fullName: "",
  email: "",
  password: "",
  organisationId: "",
  roles: [RoleNames.Citizen] as string[]
};

export default function UsersPage() {
  const t = useT();
  const [search, setSearch] = useState("");
  const [role, setRole] = useState("");
  const [active, setActive] = useState("all");
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<PagedResult<UserSummary> | null>(null);
  const [organisations, setOrganisations] = useState<OrganisationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [notice, setNotice] = useState<Notice>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [saving, setSaving] = useState(false);
  const [roleTarget, setRoleTarget] = useState<{
    user: UserSummary;
    roles: string[];
  } | null>(null);
  const [editTarget, setEditTarget] = useState<{
    user: UserSummary;
    firstName: string;
    lastName: string;
    organisationId: string;
    status: string;
  } | null>(null);
  const [statusTarget, setStatusTarget] = useState<UserSummary | null>(null);

  const debouncedSearch = useDebouncedValue(search);

  const load = useCallback(async () => {
    const params = new URLSearchParams({
      page: String(page),
      pageSize: "20"
    });

    if (debouncedSearch.trim()) {
      params.set("search", debouncedSearch.trim());
    }

    if (role) {
      params.set("role", role);
    }

    if (active !== "all") {
      params.set("isActive", active);
    }

    return api<PagedResult<UserSummary>>(`/api/v1/users?${params.toString()}`);
  }, [active, debouncedSearch, page, role]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);

    void load()
      .then((value) => {
        if (!cancelled) {
          setResult(value);
        }
      })
      .catch((caught: unknown) => {
        if (!cancelled) {
          setNotice({ message: localizedErrorMessage(caught, t), tone: "error" });
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [load]);

  useEffect(() => {
    let cancelled = false;

    void api<PagedResult<OrganisationDto>>("/api/v1/organisations?pageSize=200")
      .then((value) => {
        if (!cancelled) {
          setOrganisations(value.items);
        }
      })
      .catch(() => undefined);

    return () => {
      cancelled = true;
    };
  }, []);

  async function refresh() {
    const value = await load();
    setResult(value);
  }

  async function createUser() {
    setSaving(true);
    setNotice(null);

    const body: Record<string, unknown> = {
      fullName: form.fullName,
      email: form.email,
      password: form.password,
      roles: form.roles
    };

    if (form.organisationId) {
      body.organisationId = form.organisationId;
    }

    try {
      await apiJson<UserSummary>("/api/v1/users", "POST", body);
      setCreateOpen(false);
      setForm(emptyForm);
      setNotice({ message: t("admin.users.created"), tone: "success" });
      await refresh();
    } catch (caught) {
      setNotice({ message: localizedErrorMessage(caught, t), tone: "error" });
    } finally {
      setSaving(false);
    }
  }

  function splitName(fullName: string): { firstName: string; lastName: string } {
    const parts = fullName.trim().split(/\s+/);
    return {
      firstName: parts[0] ?? "",
      lastName: parts.slice(1).join(" ")
    };
  }

  function openEdit(user: UserSummary) {
    const { firstName, lastName } = splitName(user.fullName);
    setEditTarget({
      user,
      firstName,
      lastName,
      organisationId: user.organisationId ?? "",
      status: user.status
    });
  }

  async function saveUser() {
    if (!editTarget) {
      return;
    }

    const firstName = editTarget.firstName.trim();
    if (!firstName) {
      setNotice({ message: t("admin.users.validationFirstName"), tone: "error" });
      return;
    }

    setSaving(true);
    setNotice(null);

    try {
      await apiJson<UserSummary>(`/api/v1/users/${editTarget.user.id}`, "PATCH", {
        firstName,
        lastName: editTarget.lastName.trim(),
        // Guid.Empty explicitly clears the organisation on the API.
        organisationId:
          editTarget.organisationId || "00000000-0000-0000-0000-000000000000",
        status: editTarget.status
      });
      setEditTarget(null);
      setNotice({ message: t("admin.users.updated"), tone: "success" });
      await refresh();
    } catch (caught) {
      setNotice({ message: localizedErrorMessage(caught, t), tone: "error" });
    } finally {
      setSaving(false);
    }
  }

  async function toggleStatus(user: UserSummary) {
    setStatusTarget(null);
    setNotice(null);

    try {
      await apiJson<UserSummary>(`/api/v1/users/${user.id}`, "PATCH", {
        status: user.isActive ? "Inactive" : "Active"
      });
      setNotice({
        message: user.isActive
          ? t("admin.users.deactivated")
          : t("admin.users.activated"),
        tone: "success"
      });
      await refresh();
    } catch (caught) {
      setNotice({ message: localizedErrorMessage(caught, t), tone: "error" });
    }
  }

  async function saveRoles() {
    if (!roleTarget) {
      return;
    }

    if (roleTarget.roles.length === 0) {
      setNotice({ message: t("admin.users.validationRoleRequired"), tone: "error" });
      return;
    }

    setSaving(true);
    setNotice(null);

    try {
      await apiJson<UserSummary>(
        `/api/v1/users/${roleTarget.user.id}/roles`,
        "PUT",
        { roles: roleTarget.roles }
      );
      setRoleTarget(null);
      setNotice({ message: t("admin.users.rolesUpdated"), tone: "success" });
      await refresh();
    } catch (caught) {
      setNotice({ message: localizedErrorMessage(caught, t), tone: "error" });
    } finally {
      setSaving(false);
    }
  }

  const columns: Column<UserSummary>[] = [
    {
      key: "user",
      header: t("admin.users.colUser"),
      render: (user) => (
        <div>
          <strong>{user.fullName}</strong>
          <div className="metric__label">{user.email ?? "—"}</div>
        </div>
      )
    },
    {
      key: "roles",
      header: t("admin.users.colRoles"),
      render: (user) => (
        <div className="actions">
          {user.roles.map((item) => (
            <Badge key={item}>{humanizeRole(item, t)}</Badge>
          ))}
        </div>
      )
    },
    {
      key: "organisation",
      header: t("admin.users.colOrganisation"),
      render: (user) => user.organisationName ?? "—"
    },
    {
      key: "status",
      header: t("admin.users.colStatus"),
      render: (user) => <StatusBadge active={user.isActive} />
    },
    {
      key: "lastLogin",
      header: t("admin.users.colLastLogin"),
      render: (user) => formatDate(user.lastLoginAt)
    },
    {
      key: "actions",
      header: t("admin.users.colActions"),
      render: (user) => {
        const statusLabel = user.isActive
          ? t("admin.users.deactivate")
          : t("admin.users.activate");
        return (
          <div className="row-actions">
            <Button
              variant="secondary"
              className="row-action"
              onClick={() => openEdit(user)}
              aria-label={t("admin.users.edit")}
              title={t("admin.users.edit")}
            >
              <Icon name="file" size={15} />
            </Button>
            <Button
              variant="secondary"
              className="row-action"
              onClick={() => setRoleTarget({ user, roles: [...user.roles] })}
              aria-label={t("admin.users.editRoles")}
              title={t("admin.users.editRoles")}
            >
              <Icon name="shield" size={15} />
            </Button>
            <Button
              variant="secondary"
              className="row-action"
              onClick={() => setStatusTarget(user)}
              aria-label={statusLabel}
              title={statusLabel}
            >
              <Icon name={user.isActive ? "close" : "check"} size={15} />
            </Button>
          </div>
        );
      }
    }
  ];

  return (
    <AuthGuard allowedRoles={[RoleNames.SystemAdmin]}>
      <div className="page">
        <header className="page__header">
          <div>
            <h1>{t("admin.users.title")}</h1>
            <p>{t("admin.users.subtitle")}</p>
          </div>
          <Button onClick={() => setCreateOpen(true)}>{t("admin.users.create")}</Button>
        </header>

        <section className="card">
          <div className="filters">
            <Input
              label={t("admin.users.fieldSearch")}
              placeholder={t("admin.users.placeholderSearch")}
              value={search}
              onChange={(event) => {
                setSearch(event.target.value);
                setPage(1);
              }}
            />
            <Select
              label={t("admin.users.fieldRole")}
              value={role}
              onChange={(event) => {
                setRole(event.target.value);
                setPage(1);
              }}
            >
              <option value="">{t("admin.users.allRoles")}</option>
              {AllRoles.map((item) => (
                <option key={item} value={item}>
                  {item}
                </option>
              ))}
            </Select>
            <Select
              label={t("admin.users.fieldStatus")}
              value={active}
              onChange={(event) => {
                setActive(event.target.value);
                setPage(1);
              }}
            >
              <option value="all">{t("admin.users.allStatuses")}</option>
              <option value="true">{t("admin.users.active")}</option>
              <option value="false">{t("admin.users.inactive")}</option>
            </Select>
          </div>
        </section>

        <section className="card">
          {loading ? (
            <Skeleton rows={6} />
          ) : (
            <ServerTable
              columns={columns}
              rows={result?.items ?? []}
              page={result?.page ?? page}
              pageSize={result?.pageSize ?? 20}
              totalCount={result?.totalCount ?? 0}
              onPageChange={setPage}
              emptyMessage={t("admin.users.empty")}
            />
          )}
        </section>
      </div>

      {createOpen ? (
        <Modal title={t("admin.users.modalCreateTitle")} onClose={() => setCreateOpen(false)}>
          <Input
            label={t("admin.users.fieldFullName")}
            value={form.fullName}
            onChange={(event) =>
              setForm({ ...form, fullName: event.target.value })
            }
            required
          />
          <Input
            label={t("admin.users.fieldEmail")}
            type="email"
            value={form.email}
            onChange={(event) => setForm({ ...form, email: event.target.value })}
            required
          />
          <Input
            label={t("admin.users.fieldTemporaryPassword")}
            type="password"
            minLength={8}
            value={form.password}
            onChange={(event) =>
              setForm({ ...form, password: event.target.value })
            }
            required
          />
          <Select
            label={t("admin.users.fieldOrganisation")}
            value={form.organisationId}
            onChange={(event) =>
              setForm({ ...form, organisationId: event.target.value })
            }
          >
            <option value="">{t("admin.users.noOrganisation")}</option>
            {organisations.map((organisation) => (
              <option key={organisation.id} value={organisation.id}>
                {organisation.name}
              </option>
            ))}
          </Select>
          <label className="field">
            <span className="field__label">{t("admin.users.fieldRoles")}</span>
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
              {AllRoles.map((item) => (
                <option key={item} value={item}>
                  {item}
                </option>
              ))}
            </select>
            <span className="field__hint">{t("admin.users.hintMultiSelectRoles")}</span>
          </label>
          <div className="actions">
            <Button onClick={() => void createUser()} disabled={saving}>
              {saving ? t("admin.users.creating") : t("admin.users.create")}
            </Button>
            <Button variant="secondary" onClick={() => setCreateOpen(false)}>
              {t("admin.users.cancel")}
            </Button>
          </div>
        </Modal>
      ) : null}

      {roleTarget ? (
        <Modal
          title={t("admin.users.modalEditRolesTitle", { name: roleTarget.user.fullName })}
          onClose={() => setRoleTarget(null)}
        >
          <label className="field">
            <span className="field__label">{t("admin.users.fieldAssignedRoles")}</span>
            <select
              className="input"
              multiple
              size={10}
              value={roleTarget.roles}
              onChange={(event) =>
                setRoleTarget({
                  ...roleTarget,
                  roles: Array.from(event.target.selectedOptions, (option) => option.value)
                })
              }
            >
              {AllRoles.map((item) => (
                <option key={item} value={item}>
                  {item}
                </option>
              ))}
            </select>
          </label>
          <div className="actions">
            <Button onClick={() => void saveRoles()} disabled={saving}>
              {saving ? t("admin.users.saving") : t("admin.users.saveRoles")}
            </Button>
            <Button variant="secondary" onClick={() => setRoleTarget(null)}>
              {t("admin.users.cancel")}
            </Button>
          </div>
        </Modal>
      ) : null}

      {editTarget ? (
        <Modal
          title={t("admin.users.modalEditUserTitle", { name: editTarget.user.fullName })}
          onClose={() => setEditTarget(null)}
        >
          <Input
            label={t("admin.users.fieldFirstName")}
            value={editTarget.firstName}
            onChange={(event) =>
              setEditTarget({ ...editTarget, firstName: event.target.value })
            }
            required
          />
          <Input
            label={t("admin.users.fieldLastName")}
            value={editTarget.lastName}
            onChange={(event) =>
              setEditTarget({ ...editTarget, lastName: event.target.value })
            }
          />
          <Select
            label={t("admin.users.fieldOrganisation")}
            value={editTarget.organisationId}
            onChange={(event) =>
              setEditTarget({ ...editTarget, organisationId: event.target.value })
            }
          >
            <option value="">{t("admin.users.noOrganisation")}</option>
            {organisations.map((organisation) => (
              <option key={organisation.id} value={organisation.id}>
                {organisation.name}
              </option>
            ))}
          </Select>
          <Select
            label={t("admin.users.fieldStatus")}
            value={editTarget.status}
            onChange={(event) =>
              setEditTarget({ ...editTarget, status: event.target.value })
            }
          >
            <option value="Active">{t("admin.users.active")}</option>
            <option value="Inactive">{t("admin.users.inactive")}</option>
            <option value="Suspended">{t("admin.users.suspended")}</option>
          </Select>
          <div className="actions">
            <Button onClick={() => void saveUser()} disabled={saving}>
              {saving ? t("admin.users.saving") : t("admin.users.saveChanges")}
            </Button>
            <Button variant="secondary" onClick={() => setEditTarget(null)}>
              {t("admin.users.cancel")}
            </Button>
          </div>
        </Modal>
      ) : null}

      {statusTarget ? (
        <ConfirmDialog
          title={statusTarget.isActive
            ? t("admin.users.confirmDeactivateTitle")
            : t("admin.users.confirmActivateTitle")}
          message={
            statusTarget.isActive
              ? t("admin.users.confirmDeactivateMessage", { name: statusTarget.fullName })
              : t("admin.users.confirmActivateMessage", { name: statusTarget.fullName })
          }
          confirmLabel={statusTarget.isActive
            ? t("admin.users.deactivate")
            : t("admin.users.activate")}
          onConfirm={() => void toggleStatus(statusTarget)}
          onCancel={() => setStatusTarget(null)}
        />
      ) : null}

      <Toast message={notice?.message ?? null} tone={notice?.tone} onDismiss={() => setNotice(null)} />
    </AuthGuard>
  );
}
