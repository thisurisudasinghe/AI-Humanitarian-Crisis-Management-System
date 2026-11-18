"use client";

import { useCallback, useEffect, useState } from "react";
import {
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
import { api, apiJson, errorMessage } from "@/lib/api";
import {
  OrganisationTypes,
  type OrganisationDto,
  type PagedResult
} from "@/lib/admin";
import { RoleNames } from "@/lib/roles";

type Notice = { message: string; tone: "error" | "success" } | null;

type FormState = {
  name: string;
  organisationType: string;
  registrationNumber: string;
  contactEmail: string;
  contactPhone: string;
  address: string;
};

const emptyForm: FormState = {
  name: "",
  organisationType: "NonGovernmentalOrganisation",
  registrationNumber: "",
  contactEmail: "",
  contactPhone: "",
  address: ""
};

export default function OrganisationsPage() {
  const [search, setSearch] = useState("");
  const [active, setActive] = useState("all");
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<PagedResult<OrganisationDto> | null>(null);
  const [loading, setLoading] = useState(true);
  const [notice, setNotice] = useState<Notice>(null);
  const [editing, setEditing] = useState<OrganisationDto | null>(null);
  const [modalOpen, setModalOpen] = useState(false);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [saving, setSaving] = useState(false);
  const [statusTarget, setStatusTarget] = useState<OrganisationDto | null>(null);

  const load = useCallback(async () => {
    const params = new URLSearchParams({ page: String(page), pageSize: "20" });

    if (search.trim()) {
      params.set("search", search.trim());
    }

    if (active !== "all") {
      params.set("isActive", active);
    }

    return api<PagedResult<OrganisationDto>>(
      `/api/v1/organisations?${params.toString()}`
    );
  }, [active, page, search]);

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

  async function refresh() {
    setResult(await load());
  }

  function openCreate() {
    setEditing(null);
    setForm(emptyForm);
    setModalOpen(true);
  }

  function openEdit(organisation: OrganisationDto) {
    setEditing(organisation);
    setForm({
      name: organisation.name,
      organisationType: organisation.organisationType,
      registrationNumber: organisation.registrationNumber ?? "",
      contactEmail: organisation.contactEmail ?? "",
      contactPhone: organisation.contactPhone ?? "",
      address: organisation.address ?? ""
    });
    setModalOpen(true);
  }

  function trimmed(value: string): string | undefined {
    const result = value.trim();
    return result.length > 0 ? result : undefined;
  }

  async function save() {
    setSaving(true);
    setNotice(null);

    const body = {
      name: form.name,
      organisationType: form.organisationType,
      registrationNumber: trimmed(form.registrationNumber),
      contactEmail: trimmed(form.contactEmail),
      contactPhone: trimmed(form.contactPhone),
      address: trimmed(form.address)
    };

    try {
      if (editing) {
        await apiJson<OrganisationDto>(
          `/api/v1/organisations/${editing.id}`,
          "PATCH",
          body
        );
        setNotice({ message: "Organisation updated.", tone: "success" });
      } else {
        await apiJson<OrganisationDto>("/api/v1/organisations", "POST", body);
        setNotice({ message: "Organisation created.", tone: "success" });
      }

      setModalOpen(false);
      await refresh();
    } catch (caught) {
      setNotice({ message: errorMessage(caught), tone: "error" });
    } finally {
      setSaving(false);
    }
  }

  async function toggleStatus(organisation: OrganisationDto) {
    setStatusTarget(null);
    setNotice(null);

    try {
      await apiJson<OrganisationDto>(
        `/api/v1/organisations/${organisation.id}`,
        "PATCH",
        { isActive: !organisation.isActive }
      );
      setNotice({
        message: organisation.isActive
          ? "Organisation deactivated."
          : "Organisation activated.",
        tone: "success"
      });
      await refresh();
    } catch (caught) {
      setNotice({ message: errorMessage(caught), tone: "error" });
    }
  }

  const columns: Column<OrganisationDto>[] = [
    {
      key: "name",
      header: "Name",
      render: (organisation) => (
        <div>
          <strong>{organisation.name}</strong>
          <div className="metric__label">
            {organisation.registrationNumber ?? "No registration number"}
          </div>
        </div>
      )
    },
    {
      key: "type",
      header: "Type",
      render: (organisation) => organisation.organisationType
    },
    {
      key: "contact",
      header: "Contact",
      render: (organisation) => (
        <div>
          <div>{organisation.contactEmail ?? "—"}</div>
          <div className="metric__label">{organisation.contactPhone ?? ""}</div>
        </div>
      )
    },
    {
      key: "status",
      header: "Status",
      render: (organisation) => <StatusBadge active={organisation.isActive} />
    },
    {
      key: "actions",
      header: "Actions",
      render: (organisation) => (
        <div className="actions">
          <Button variant="secondary" onClick={() => openEdit(organisation)}>
            Edit
          </Button>
          <Button
            variant="secondary"
            onClick={() => setStatusTarget(organisation)}
          >
            {organisation.isActive ? "Deactivate" : "Activate"}
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
            <h1>Organisations</h1>
            <p>Manage partner agencies and lead organisations.</p>
          </div>
          <Button onClick={openCreate}>Create organisation</Button>
        </header>

        <section className="card">
          <div className="filters">
            <Input
              label="Search"
              placeholder="Organisation name"
              value={search}
              onChange={(event) => {
                setSearch(event.target.value);
                setPage(1);
              }}
            />
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
              emptyMessage="No organisations match the current filters."
            />
          )}
        </section>
      </div>

      {modalOpen ? (
        <Modal
          title={editing ? "Edit organisation" : "Create organisation"}
          onClose={() => setModalOpen(false)}
        >
          <Input
            label="Name"
            value={form.name}
            onChange={(event) => setForm({ ...form, name: event.target.value })}
            required
          />
          <Select
            label="Type"
            value={form.organisationType}
            onChange={(event) =>
              setForm({ ...form, organisationType: event.target.value })
            }
          >
            {OrganisationTypes.map((type) => (
              <option key={type} value={type}>
                {type}
              </option>
            ))}
          </Select>
          <Input
            label="Registration number"
            value={form.registrationNumber}
            onChange={(event) =>
              setForm({ ...form, registrationNumber: event.target.value })
            }
          />
          <Input
            label="Contact email"
            type="email"
            value={form.contactEmail}
            onChange={(event) =>
              setForm({ ...form, contactEmail: event.target.value })
            }
          />
          <Input
            label="Contact phone"
            value={form.contactPhone}
            onChange={(event) =>
              setForm({ ...form, contactPhone: event.target.value })
            }
          />
          <Input
            label="Address"
            value={form.address}
            onChange={(event) => setForm({ ...form, address: event.target.value })}
          />
          <div className="actions">
            <Button onClick={() => void save()} disabled={saving}>
              {saving ? "Saving…" : "Save"}
            </Button>
            <Button variant="secondary" onClick={() => setModalOpen(false)}>
              Cancel
            </Button>
          </div>
        </Modal>
      ) : null}

      {statusTarget ? (
        <ConfirmDialog
          title={
            statusTarget.isActive
              ? "Deactivate organisation"
              : "Activate organisation"
          }
          message={
            statusTarget.isActive
              ? `Deactivate ${statusTarget.name}? Linked accounts keep their data but the organisation is marked inactive.`
              : `Activate ${statusTarget.name}? The organisation becomes selectable again.`
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
