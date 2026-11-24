"use client";

import { useEffect, useState } from "react";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { Button, Input, Skeleton, Toast } from "@/components/ui";
import { AuthGuard } from "@/components/AuthGuard";
import { api, apiJson } from "@/lib/api";
import { ConfigKeys, type SystemSettingDto } from "@/lib/admin";
import { useT } from "@/lib/i18n";
import { RoleNames } from "@/lib/roles";

type Notice = { message: string; tone: "error" | "success" } | null;

export default function ConfigPage() {
  const t = useT();
  const keyLabels: Record<string, string> = {
    SlaHours: t("admin.config.keySlaHours"),
    ReorderThreshold: t("admin.config.keyReorderThreshold"),
    ServiceTypes: t("admin.config.keyServiceTypes"),
    RequestCategories: t("admin.config.keyRequestCategories"),
    UrgencyThresholds: t("admin.config.keyUrgencyThresholds"),
    SuspiciousDonationAmount: t("admin.config.keySuspiciousDonationAmount")
  };
  const [settings, setSettings] = useState<SystemSettingDto[]>([]);
  const [values, setValues] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState<Notice>(null);

  useEffect(() => {
    let cancelled = false;

    void api<SystemSettingDto[]>("/api/v1/admin/config")
      .then((items) => {
        if (cancelled) {
          return;
        }

        setSettings(items);
        setValues(
          Object.fromEntries(
            ConfigKeys.map((key) => [
              key,
              items.find((item) => item.key === key)?.value ?? ""
            ])
          )
        );
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
  }, []);

  async function save() {
    const entries = ConfigKeys.filter(
      (key) => (values[key] ?? "").trim().length > 0
    ).map((key) => ({
      key,
      value: values[key],
      description: settings.find((item) => item.key === key)?.description ?? null
    }));

    if (entries.length === 0) {
      setNotice({ message: t("admin.config.validationNoValues"), tone: "error" });
      return;
    }

    setSaving(true);
    setNotice(null);

    try {
      const updated = await apiJson<SystemSettingDto[]>(
        "/api/v1/admin/config",
        "PATCH",
        { settings: entries }
      );
      setSettings(updated);
      setNotice({ message: t("admin.config.saved"), tone: "success" });
    } catch (caught) {
      setNotice({ message: localizedErrorMessage(caught, t), tone: "error" });
    } finally {
      setSaving(false);
    }
  }

  return (
    <AuthGuard allowedRoles={[RoleNames.SystemAdmin]}>
      <div className="page">
        <header className="page__header">
          <div>
            <h1>{t("admin.config.title")}</h1>
            <p>{t("admin.config.subtitle")}</p>
          </div>
          <Button onClick={() => void save()} disabled={saving || loading}>
            {saving ? t("admin.config.saving") : t("admin.config.saveChanges")}
          </Button>
        </header>

        <section className="card">
          {loading ? (
            <Skeleton rows={6} />
          ) : (
            <div className="config-grid">
              {ConfigKeys.map((key) => {
                const setting = settings.find((item) => item.key === key);

                return (
                  <div className="config-item" key={key}>
                    <Input
                      label={keyLabels[key] ?? key}
                      value={values[key] ?? ""}
                      onChange={(event) =>
                        setValues({ ...values, [key]: event.target.value })
                      }
                    />
                    <div className="metric__label">
                      {setting?.description ?? key}
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </section>
      </div>

      <Toast message={notice?.message ?? null} tone={notice?.tone} onDismiss={() => setNotice(null)} />
    </AuthGuard>
  );
}
