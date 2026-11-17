"use client";

import { useEffect, useState } from "react";
import { Button, Input, Skeleton, Toast } from "@/components/ui";
import { AuthGuard } from "@/components/AuthGuard";
import { api, apiJson, errorMessage } from "@/lib/api";
import { ConfigKeys, type SystemSettingDto } from "@/lib/admin";
import { RoleNames } from "@/lib/roles";

type Notice = { message: string; tone: "error" | "success" } | null;

const keyLabels: Record<string, string> = {
  SlaHours: "SLA hours",
  ReorderThreshold: "Resource reorder threshold",
  ServiceTypes: "Service types",
  RequestCategories: "Request categories",
  UrgencyThresholds: "Urgency thresholds",
  SuspiciousDonationAmount: "Suspicious donation amount"
};

export default function ConfigPage() {
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
      setNotice({ message: "Enter at least one setting value.", tone: "error" });
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
      setNotice({ message: "Configuration saved.", tone: "success" });
    } catch (caught) {
      setNotice({ message: errorMessage(caught), tone: "error" });
    } finally {
      setSaving(false);
    }
  }

  return (
    <AuthGuard allowedRoles={[RoleNames.SystemAdmin]}>
      <div className="page">
        <header className="page__header">
          <div>
            <h1>Configuration</h1>
            <p>Runtime thresholds and catalogue values used across modules.</p>
          </div>
          <Button onClick={() => void save()} disabled={saving || loading}>
            {saving ? "Saving…" : "Save changes"}
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
