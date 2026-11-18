"use client";

import { useCallback, useEffect, useState } from "react";
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
import { AdminNav } from "@/components/safeway/AdminNav";
import { api, apiJson, errorMessage } from "@/lib/api";
import {
  formatDate,
  type OrganisationDto,
  type PagedResult,
  type UserSummary
} from "@/lib/admin";
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
          setNotice({ message: errorMessage(caught), tone: "error" });
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
      setNotice({ message: "User created.", tone: "success" });
      await refresh();
    } catch (caught) {
      setNotice({ message: errorMessage(caught), tone: "error" });
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
      setNotice({ message: "First name is required.", tone: "error" });
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
      setNotice({ message: "User updated.", tone: "success" });
      await refresh();
    } catch (caught) {
      setNotice({ message: errorMessage(caught), tone: "error" });
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
        message: user.isActive ? "User deactivated." : "User activated.",
        tone: "success"
      });
      await refresh();
    } catch (caught) {
      setNotice({ message: errorMessage(caught), tone: "error" });
    }
  }

  async function saveRoles() {
    if (!roleTarget) {
      return;
    }

    if (roleTarget.roles.length === 0) {
      setNotice({ message: "Select at least one role.", tone: "error" });
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
      setNotice({ message: "Roles updated.", tone: "success" });
      await refresh();
    } catch (caught) {
      setNotice({ message: errorMessage(caught), tone: "error" });
    } finally {
      setSaving(false);
    }
  }

  const columns: Column<UserSummary>[] = [
    {
      key: "user",
      header: "User",
      render: (user) => (
        <div>
          <strong>{user.fullName}</strong>
          <div className="metric__label">{user.email ?? "—"}</div>
        </div>
      )
    },
    {
      key: "roles",
      header: "Roles",
      render: (user) => (
        <div className="actions">
          {user.roles.map((item) => (
            <Badge key={item}>{item}</Badge>
          ))}
        </div>
      )
    },
    {
      key: "organisation",
      header: "Organisation",
      render: (user) => user.organisationName ?? "—"
    },
    {
      key: "status",
      header: "Status",
      render: (user) => <StatusBadge active={user.isActive} />
    },
    {
      key: "lastLogin",
      header: "Last login",
      render: (user) => formatDate(user.lastLoginAt)
    },
    {
      key: "actions",
      header: "Actions",
      render: (user) => (
        <div className="actions">
          <Button variant="secondary" onClick={() => openEdit(user)}>
            Edit
          </Button>
          <Button
            variant="secondary"
            onClick={() => setRoleTarget({ user, roles: [...user.roles] })}
          >
            Edit roles
          </Button>
          <Button variant="secondary" onClick={() => setStatusTarget(user)}>
            {user.isActive ? "Deactivate" : "Activate"}
          </Button>
        </div>
      )
    }
  ];

  return (
    <AuthGuard allowedRoles={[RoleNames.SystemAdmin]}>
      <div className="page">
        <header className="page__header">
          <div>
            <h1>Users</h1>
            <p>Create staff accounts, assign roles and manage account status.</p>
          </div>
          <Button onClick={() => setCreateOpen(true)}>Create user</Button>
        </header>

        <AdminNav />

        <section className="card">
          <div className="filters">
            <Input
              label="Search"
              placeholder="Name or email"
              value={search}
              onChange={(event) => {
                setSearch(event.target.value);
                setPage(1);
              }}
            />
            <Select
              label="Role"
              value={role}
              onChange={(event) => {
                setRole(event.target.value);
                setPage(1);
              }}
            >
              <option value="">All roles</option>
              {AllRoles.map((item) => (
                <option key={item} value={item}>
                  {item}
                </option>
              ))}
            </Select>
            <Select
              label="Status"
              value={active}
              onChange={(event) => {
                setActive(event.target.value);
                setPage(1);
              }}
            >
              <option value="all">All statuses</option>
              <option value="true">Active</option>
              <option value="false">Inactive</option>
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
              emptyMessage="No users match the current filters."
            />
          )}
        </section>
      </div>

      {createOpen ? (
        <Modal title="Create user" onClose={() => setCreateOpen(false)}>
          <Input
            label="Full name"
            value={form.fullName}
            onChange={(event) =>
              setForm({ ...form, fullName: event.target.value })
            }
            required
          />
          <Input
            label="Email"
            type="email"
            value={form.email}
            onChange={(event) => setForm({ ...form, email: event.target.value })}
            required
          />
          <Input
            label="Temporary password"
            type="password"
            minLength={8}
            value={form.password}
            onChange={(event) =>
              setForm({ ...form, password: event.target.value })
            }
            required
          />
          <Select
            label="Organisation"
            value={form.organisationId}
            onChange={(event) =>
              setForm({ ...form, organisationId: event.target.value })
            }
          >
            <option value="">No organisation</option>
            {organisations.map((organisation) => (
              <option key={organisation.id} value={organisation.id}>
                {organisation.name}
              </option>
            ))}
          </Select>
          <label className="field">
            <span className="field__label">Roles</span>
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
            <span className="field__hint">Hold Ctrl/Cmd to select multiple roles.</span>
          </label>
          <div className="actions">
            <Button onClick={() => void createUser()} disabled={saving}>
              {saving ? "Creating…" : "Create user"}
            </Button>
            <Button variant="secondary" onClick={() => setCreateOpen(false)}>
              Cancel
            </Button>
          </div>
        </Modal>
      ) : null}

      {roleTarget ? (
        <Modal
          title={`Edit roles — ${roleTarget.user.fullName}`}
          onClose={() => setRoleTarget(null)}
        >
          <label className="field">
            <span className="field__label">Assigned roles</span>
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
              {saving ? "Saving…" : "Save roles"}
            </Button>
            <Button variant="secondary" onClick={() => setRoleTarget(null)}>
              Cancel
            </Button>
          </div>
        </Modal>
      ) : null}

      {editTarget ? (
        <Modal
          title={`Edit user — ${editTarget.user.fullName}`}
          onClose={() => setEditTarget(null)}
        >
          <Input
            label="First name"
            value={editTarget.firstName}
            onChange={(event) =>
              setEditTarget({ ...editTarget, firstName: event.target.value })
            }
            required
          />
          <Input
            label="Last name"
            value={editTarget.lastName}
            onChange={(event) =>
              setEditTarget({ ...editTarget, lastName: event.target.value })
            }
          />
          <Select
            label="Organisation"
            value={editTarget.organisationId}
            onChange={(event) =>
              setEditTarget({ ...editTarget, organisationId: event.target.value })
            }
          >
            <option value="">No organisation</option>
            {organisations.map((organisation) => (
              <option key={organisation.id} value={organisation.id}>
                {organisation.name}
              </option>
            ))}
          </Select>
          <Select
            label="Status"
            value={editTarget.status}
            onChange={(event) =>
              setEditTarget({ ...editTarget, status: event.target.value })
            }
          >
            <option value="Active">Active</option>
            <option value="Inactive">Inactive</option>
            <option value="Suspended">Suspended</option>
          </Select>
          <div className="actions">
            <Button onClick={() => void saveUser()} disabled={saving}>
              {saving ? "Saving…" : "Save changes"}
            </Button>
            <Button variant="secondary" onClick={() => setEditTarget(null)}>
              Cancel
            </Button>
          </div>
        </Modal>
      ) : null}

      {statusTarget ? (
        <ConfirmDialog
          title={statusTarget.isActive ? "Deactivate user" : "Activate user"}
          message={
            statusTarget.isActive
              ? `Deactivate ${statusTarget.fullName}? They will no longer be able to sign in.`
              : `Activate ${statusTarget.fullName}? They will be able to sign in again.`
          }
          confirmLabel={statusTarget.isActive ? "Deactivate" : "Activate"}
          onConfirm={() => void toggleStatus(statusTarget)}
          onCancel={() => setStatusTarget(null)}
        />
      ) : null}

      <Toast message={notice?.message ?? null} tone={notice?.tone} onDismiss={() => setNotice(null)} />
    </AuthGuard>
  );
}
