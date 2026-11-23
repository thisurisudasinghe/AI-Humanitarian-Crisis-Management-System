"use client";

import { useCallback, useEffect, useState } from "react";
import { localizedErrorMessage } from "@/lib/i18n/errors";
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
import { AdminNav } from "@/components/safeway/AdminNav";
import { api, apiJson } from "@/lib/api";
import {
  OrganisationTypes,
  type OrganisationDto,
  type PagedResult
} from "@/lib/admin";
import { useT } from "@/lib/i18n";
import { RoleNames } from "@/lib/roles";
import { useDebouncedValue } from "@/lib/useDebouncedValue";

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
  const t = useT();
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

  const debouncedSearch = useDebouncedValue(search);

  const load = useCallback(async () => {
    const params = new URLSearchParams({ page: String(page), pageSize: "20" });

    if (debouncedSearch.trim()) {
      params.set("search", debouncedSearch.trim());
    }

    if (active !== "all") {
      params.set("isActive", active);
    }

    return api<PagedResult<OrganisationDto>>(
      `/api/v1/organisations?${params.toString()}`
    );
  }, [active, debouncedSearch, page]);

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
        setNotice({ message: t("admin.organisations.updated"), tone: "success" });
      } else {
        await apiJson<OrganisationDto>("/api/v1/organisations", "POST", body);
        setNotice({ message: t("admin.organisations.created"), tone: "success" });
      }

      setModalOpen(false);
      await refresh();
    } catch (caught) {
      setNotice({ message: localizedErrorMessage(caught, t), tone: "error" });
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
          ? t("admin.organisations.deactivated")
          : t("admin.organisations.activated"),
        tone: "success"
      });
      await refresh();
    } catch (caught) {
      setNotice({ message: localizedErrorMessage(caught, t), tone: "error" });
    }
  }

  const columns: Column<OrganisationDto>[] = [
    {
      key: "name",
      header: t("admin.organisations.colName"),
      render: (organisation) => (
        <div>
          <strong>{organisation.name}</strong>
          <div className="metric__label">
            {organisation.registrationNumber ?? t("admin.organisations.noRegistrationNumber")}
          </div>
        </div>
      )
    },
    {
      key: "type",
      header: t("admin.organisations.colType"),
      render: (organisation) => organisation.organisationType
    },
    {
      key: "contact",
      header: t("admin.organisations.colContact"),
      render: (organisation) => (
        <div>
          <div>{organisation.contactEmail ?? "—"}</div>
          <div className="metric__label">{organisation.contactPhone ?? ""}</div>
        </div>
      )
    },
    {
      key: "status",
      header: t("admin.organisations.colStatus"),
      render: (organisation) => <StatusBadge active={organisation.isActive} />
    },
    {
      key: "actions",
      header: t("admin.organisations.colActions"),
      render: (organisation) => (
        <div className="actions">
          <Button variant="secondary" onClick={() => openEdit(organisation)}>
            {t("admin.organisations.edit")}
          </Button>
          <Button
            variant="secondary"
            onClick={() => setStatusTarget(organisation)}
          >
            {organisation.isActive
              ? t("admin.organisations.deactivate")
              : t("admin.organisations.activate")}
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
            <h1>{t("admin.organisations.title")}</h1>
            <p>{t("admin.organisations.subtitle")}</p>
          </div>
          <Button onClick={openCreate}>{t("admin.organisations.create")}</Button>
        </header>

        <AdminNav />

        <section className="card">
          <div className="filters">
            <Input
              label={t("admin.organisations.fieldSearch")}
              placeholder={t("admin.organisations.placeholderSearch")}
              value={search}
              onChange={(event) => {
                setSearch(event.target.value);
                setPage(1);
              }}
            />
            <Select
              label={t("admin.organisations.fieldStatus")}
              value={active}
              onChange={(event) => {
                setActive(event.target.value);
                setPage(1);
              }}
            >
              <option value="all">{t("admin.organisations.allStatuses")}</option>
              <option value="true">{t("admin.organisations.active")}</option>
              <option value="false">{t("admin.organisations.inactive")}</option>
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
              emptyMessage={t("admin.organisations.empty")}
            />
          )}
        </section>
      </div>

      {modalOpen ? (
        <Modal
          title={editing
            ? t("admin.organisations.modalEditTitle")
            : t("admin.organisations.modalCreateTitle")}
          onClose={() => setModalOpen(false)}
        >
          <Input
            label={t("admin.organisations.fieldName")}
            value={form.name}
            onChange={(event) => setForm({ ...form, name: event.target.value })}
            required
          />
          <Select
            label={t("admin.organisations.fieldType")}
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
            label={t("admin.organisations.fieldRegistrationNumber")}
            value={form.registrationNumber}
            onChange={(event) =>
              setForm({ ...form, registrationNumber: event.target.value })
            }
          />
          <Input
            label={t("admin.organisations.fieldContactEmail")}
            type="email"
            value={form.contactEmail}
            onChange={(event) =>
              setForm({ ...form, contactEmail: event.target.value })
            }
          />
          <Input
            label={t("admin.organisations.fieldContactPhone")}
            value={form.contactPhone}
            onChange={(event) =>
              setForm({ ...form, contactPhone: event.target.value })
            }
          />
          <Input
            label={t("admin.organisations.fieldAddress")}
            value={form.address}
            onChange={(event) => setForm({ ...form, address: event.target.value })}
          />
          <div className="actions">
            <Button onClick={() => void save()} disabled={saving}>
              {saving ? t("admin.organisations.saving") : t("admin.organisations.save")}
            </Button>
            <Button variant="secondary" onClick={() => setModalOpen(false)}>
              {t("admin.organisations.cancel")}
            </Button>
          </div>
        </Modal>
      ) : null}

      {statusTarget ? (
        <ConfirmDialog
          title={
            statusTarget.isActive
              ? t("admin.organisations.confirmDeactivateTitle")
              : t("admin.organisations.confirmActivateTitle")
          }
          message={
            statusTarget.isActive
              ? t("admin.organisations.confirmDeactivateMessage", { name: statusTarget.name })
              : t("admin.organisations.confirmActivateMessage", { name: statusTarget.name })
          }
          confirmLabel={statusTarget.isActive
            ? t("admin.organisations.deactivate")
            : t("admin.organisations.activate")}
          onConfirm={() => void toggleStatus(statusTarget)}
          onCancel={() => setStatusTarget(null)}
        />
      ) : null}

      <Toast message={notice?.message ?? null} tone={notice?.tone} onDismiss={() => setNotice(null)} />
    </AuthGuard>
  );
}
