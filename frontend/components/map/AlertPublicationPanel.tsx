"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import { api } from "@/lib/api";
import { useT } from "@/lib/i18n";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { markerLabelKey } from "./mapTypes";
import styles from "./AlertPublicationPanel.module.css";

interface StaffAlert {
  id: string;
  crisisId?: string | null;
  title: string;
  body: string;
  status: string;
  publishedAt?: string | null;
  publicVisible: boolean;
  publicTitle?: string | null;
  publicSummary?: string | null;
  crisisType?: string | null;
  severity?: string | null;
  district?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  expiresAt?: string | null;
  publishedByUserId?: string | null;
}

interface PublicationForm {
  publicTitle: string;
  publicSummary: string;
  crisisType: string;
  severity: string;
  district: string;
  latitude: string;
  longitude: string;
  expiresAt: string;
}

interface Notice {
  tone: "success" | "error";
  text: string;
}

type PendingAction = "publish" | "hide" | null;

const CRISIS_TYPES = [
  "flood",
  "landslide",
  "fire",
  "severe-weather",
  "drought",
  "other-crisis"
] as const;

const EMPTY_FORM: PublicationForm = {
  publicTitle: "",
  publicSummary: "",
  crisisType: "",
  severity: "",
  district: "",
  latitude: "",
  longitude: "",
  expiresAt: ""
};

function normaliseType(value?: string | null): string {
  const normalised = (value ?? "").trim().toLowerCase().replace(/\s+/g, "-");
  return CRISIS_TYPES.find((type) => type === normalised) ?? "";
}

function toDateTimeLocal(value?: string | null): string {
  if (!value) return "";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "" : date.toISOString().slice(0, 16);
}

function toUtcIso(value: string): string | null {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}

function formFromAlert(alert: StaffAlert): PublicationForm {
  return {
    publicTitle: alert.publicTitle ?? "",
    publicSummary: alert.publicSummary ?? "",
    crisisType: normaliseType(alert.crisisType),
    severity: alert.severity ?? "",
    district: alert.district ?? "",
    latitude: alert.latitude != null ? String(alert.latitude) : "",
    longitude: alert.longitude != null ? String(alert.longitude) : "",
    expiresAt: toDateTimeLocal(alert.expiresAt)
  };
}

/**
 * Coordinator-only publication controls. The private alert title/body are shown
 * only to an authorized coordinator; only the public fields are ever sent to
 * the publication endpoint and therefore to the anonymous map feed.
 */
export function AlertPublicationPanel() {
  const t = useT();
  const [alerts, setAlerts] = useState<StaffAlert[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [form, setForm] = useState<PublicationForm>(EMPTY_FORM);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [loadError, setLoadError] = useState("");
  const [notice, setNotice] = useState<Notice | null>(null);
  const [pendingAction, setPendingAction] = useState<PendingAction>(null);

  const applyAlert = useCallback((alert: StaffAlert) => {
    setSelectedId(alert.id);
    setForm(formFromAlert(alert));
    setNotice(null);
    setPendingAction(null);
  }, []);

  const loadAlerts = useCallback(async (preferredId?: string | null) => {
    setLoading(true);
    setLoadError("");
    try {
      const data = await api<StaffAlert[]>("/api/v1/alerts");
      setAlerts(data);
      const next = data.find((alert) => alert.id === preferredId) ?? data[0] ?? null;
      if (next) applyAlert(next);
      else {
        setSelectedId(null);
        setForm(EMPTY_FORM);
      }
    } catch (error) {
      setLoadError(localizedErrorMessage(error, t));
    } finally {
      setLoading(false);
    }
  }, [applyAlert, t]);

  useEffect(() => {
    void loadAlerts();
  }, [loadAlerts]);

  const selected = alerts.find((alert) => alert.id === selectedId) ?? null;

  const update = (field: keyof PublicationForm, value: string) => {
    setForm((current) => ({ ...current, [field]: value }));
    setPendingAction(null);
  };

  async function publish() {
    if (!selected) return;

    const latitude = Number(form.latitude);
    const longitude = Number(form.longitude);
    if (
      !form.publicTitle.trim() ||
      !form.publicSummary.trim() ||
      !form.crisisType ||
      !Number.isFinite(latitude) ||
      latitude < -90 ||
      latitude > 90 ||
      !Number.isFinite(longitude) ||
      longitude < -180 ||
      longitude > 180
    ) {
      setNotice({ tone: "error", text: t("operations.mapPublication.requiredVisible") });
      return;
    }

    setSaving(true);
    setNotice(null);
    try {
      await api<void>(`/api/v1/alerts/${selected.id}/map-publication`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          visible: true,
          publicTitle: form.publicTitle.trim(),
          publicSummary: form.publicSummary.trim(),
          crisisType: form.crisisType,
          severity: form.severity.trim() || null,
          district: form.district.trim() || null,
          latitude,
          longitude,
          expiresAt: toUtcIso(form.expiresAt)
        })
      });
      setPendingAction(null);
      setNotice({ tone: "success", text: t("operations.mapPublication.published") });
      await loadAlerts(selected.id);
    } catch (error) {
      setNotice({ tone: "error", text: `${t("operations.mapPublication.saveError")} ${localizedErrorMessage(error, t)}` });
    } finally {
      setSaving(false);
    }
  }

  async function hide() {
    if (!selected) return;

    setSaving(true);
    setNotice(null);
    try {
      await api<void>(`/api/v1/alerts/${selected.id}/map-publication`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ visible: false })
      });
      setPendingAction(null);
      setNotice({ tone: "success", text: t("operations.mapPublication.hiddenDone") });
      await loadAlerts(selected.id);
    } catch (error) {
      setNotice({ tone: "error", text: `${t("operations.mapPublication.saveError")} ${localizedErrorMessage(error, t)}` });
    } finally {
      setSaving(false);
    }
  }

  async function confirmPending() {
    if (pendingAction === "publish") await publish();
    if (pendingAction === "hide") await hide();
  }

  const submitForm = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setPendingAction("publish");
  };

  return (
    <section className="panel" aria-labelledby="alert-publication-heading">
      <div className={styles.header}>
        <h2 id="alert-publication-heading">{t("operations.mapPublication.title")}</h2>
        <button type="button" onClick={() => void loadAlerts(selectedId)} disabled={loading || saving}>
          {t("operations.mapPublication.reload")}
        </button>
      </div>
      <p className="muted small">{t("operations.mapPublication.intro")}</p>

      <datalist id="publication-crisis-types">
        {CRISIS_TYPES.map((type) => (
          <option key={type} value={type}>{t(markerLabelKey(type))}</option>
        ))}
      </datalist>

      {loading ? (
        <p className="muted" role="status">{t("operations.mapPublication.loading")}</p>
      ) : loadError ? (
        <p className="error" role="alert">{loadError}</p>
      ) : alerts.length === 0 ? (
        <p className="muted" role="status">{t("operations.mapPublication.empty")}</p>
      ) : (
        <div className={styles.layout}>
          <div className={styles.alertList} role="group" aria-label={t("operations.mapPublication.alertTitle")}>
            {alerts.map((alert) => (
              <button
                type="button"
                key={alert.id}
                className={`${styles.alertItem} ${alert.id === selectedId ? styles.alertItemActive : ""}`}
                aria-pressed={alert.id === selectedId}
                onClick={() => applyAlert(alert)}
              >
                <span className={styles.alertName}>{alert.title}</span>
                <span className={`${styles.publicFlag} ${alert.publicVisible ? styles.isPublic : styles.isHidden}`}>
                  {t(alert.publicVisible ? "operations.mapPublication.current" : "operations.mapPublication.hidden")}
                </span>
              </button>
            ))}
          </div>

          <form className={`form-grid ${styles.form}`} onSubmit={submitForm}>
            <p className={styles.privateTitle}>
              <span className="muted">{t("operations.mapPublication.alertTitle")}</span>
              <span>{selected?.title}</span>
            </p>

            <label htmlFor="publication-title">{t("operations.mapPublication.publicTitle")}</label>
            <input
              id="publication-title"
              value={form.publicTitle}
              maxLength={200}
              placeholder={t("operations.mapPublication.publicTitlePlaceholder")}
              onChange={(event) => update("publicTitle", event.target.value)}
              required
            />

            <label htmlFor="publication-summary">{t("operations.mapPublication.publicSummary")}</label>
            <textarea
              id="publication-summary"
              rows={3}
              value={form.publicSummary}
              maxLength={1000}
              placeholder={t("operations.mapPublication.publicSummaryPlaceholder")}
              onChange={(event) => update("publicSummary", event.target.value)}
              required
            />

            <label htmlFor="publication-type">{t("operations.mapPublication.crisisType")}</label>
            <input
              id="publication-type"
              list="publication-crisis-types"
              value={form.crisisType}
              maxLength={40}
              placeholder={t("operations.mapPublication.crisisTypePlaceholder")}
              onChange={(event) => update("crisisType", event.target.value)}
              required
            />

            <label htmlFor="publication-severity">{t("operations.mapPublication.severity")}</label>
            <input
              id="publication-severity"
              value={form.severity}
              maxLength={30}
              placeholder={t("operations.mapPublication.severityPlaceholder")}
              onChange={(event) => update("severity", event.target.value)}
            />

            <label htmlFor="publication-district">{t("operations.mapPublication.district")}</label>
            <input
              id="publication-district"
              value={form.district}
              maxLength={120}
              placeholder={t("operations.mapPublication.districtPlaceholder")}
              onChange={(event) => update("district", event.target.value)}
            />

            <label htmlFor="publication-latitude">{t("operations.mapPublication.latitude")}</label>
            <input
              id="publication-latitude"
              type="number"
              step="any"
              min={-90}
              max={90}
              value={form.latitude}
              onChange={(event) => update("latitude", event.target.value)}
              required
            />

            <label htmlFor="publication-longitude">{t("operations.mapPublication.longitude")}</label>
            <input
              id="publication-longitude"
              type="number"
              step="any"
              min={-180}
              max={180}
              value={form.longitude}
              onChange={(event) => update("longitude", event.target.value)}
              required
            />

            <label htmlFor="publication-expires">{t("operations.mapPublication.expiresAt")}</label>
            <input
              id="publication-expires"
              type="datetime-local"
              value={form.expiresAt}
              onChange={(event) => update("expiresAt", event.target.value)}
            />

            <p className={`muted small ${styles.privacy}`}>{t("operations.mapPublication.privacyNote")}</p>

            {pendingAction ? (
              <div className={styles.confirmRow} role="group" aria-label={t("operations.mapPublication.title")}>
                <span className={styles.confirmText}>
                  {t(pendingAction === "publish" ? "operations.mapPublication.confirmPublish" : "operations.mapPublication.confirmHide")}
                </span>
                <button type="button" onClick={() => void confirmPending()} disabled={saving}>
                  {t(pendingAction === "publish" ? "operations.mapPublication.publish" : "operations.mapPublication.hide")}
                </button>
                <button type="button" onClick={() => setPendingAction(null)} disabled={saving}>
                  {t("operations.mapPublication.cancel")}
                </button>
              </div>
            ) : (
              <div className="inline">
                <button type="submit" disabled={saving}>
                  {t("operations.mapPublication.publish")}
                </button>
                {selected?.publicVisible && (
                  <button type="button" onClick={() => setPendingAction("hide")} disabled={saving}>
                    {t("operations.mapPublication.hide")}
                  </button>
                )}
              </div>
            )}

            {notice && (
              <p
                className={notice.tone === "success" ? "success" : "error"}
                role={notice.tone === "success" ? "status" : "alert"}
              >
                {notice.text}
              </p>
            )}
          </form>
        </div>
      )}
    </section>
  );
}
