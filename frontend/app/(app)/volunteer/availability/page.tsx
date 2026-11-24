"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Icon } from "@/components/safeway/Icon";
import { PortalNotice, PortalSection, PortalState } from "@/components/safeway/PortalPrimitives";
import { VolunteerPage } from "@/components/safeway/VolunteerPage";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useI18n } from "@/lib/i18n";
import { formatDate } from "@/lib/i18n/core";
import { addVolunteerAvailability, getVolunteerPortal, type VolunteerAvailability } from "@/lib/portal";
import {
  backendTimestamp,
  fromDateTimeLocal,
  toDateTimeLocal,
  windowsOverlap
} from "@/lib/volunteer";

export default function VolunteerAvailabilityPage() {
  const { t, locale } = useI18n();
  const [windows, setWindows] = useState<VolunteerAvailability[]>([]);
  const [start, setStart] = useState("");
  const [end, setEnd] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [refreshToken, setRefreshToken] = useState(0);

  useEffect(() => {
    let active = true;
    setLoading(true);
    getVolunteerPortal()
      .then((result) => {
        if (!active) return;
        setWindows(result.availability);
        setError(null);
      })
      .catch((cause) => {
        if (active) setError(localizedErrorMessage(cause, t));
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [refreshToken, t]);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving) return;
    setNotice(null);
    setFormError(null);

    if (!start || !end) {
      setFormError(t("volunteerX.availability.validationRequired"));
      return;
    }
    const startsAt = fromDateTimeLocal(start);
    const endsAt = fromDateTimeLocal(end);
    if (!startsAt || !endsAt) {
      setFormError(t("volunteerX.availability.validationRequired"));
      return;
    }
    const startMs = new Date(startsAt).getTime();
    const endMs = new Date(endsAt).getTime();
    if (endMs <= startMs) {
      setFormError(t("volunteerX.availability.validationEndAfterStart"));
      return;
    }
    if (startMs < Date.now() - 60_000) {
      setFormError(t("volunteerX.availability.validationPast"));
      return;
    }
    if (windowsOverlap(startsAt, endsAt, windows)) {
      setFormError(t("volunteerX.availability.validationOverlap"));
      return;
    }

    setSaving(true);
    try {
      await addVolunteerAvailability({ startsAt, endsAt });
      setStart("");
      setEnd("");
      setNotice(t("volunteerX.availability.success"));
      setRefreshToken((value) => value + 1);
    } catch (cause) {
      setFormError(localizedErrorMessage(cause, t));
    } finally {
      setSaving(false);
    }
  }

  return (
    <VolunteerPage
      eyebrowKey="volunteerX.availability.title"
      titleKey="volunteerX.availability.title"
      descriptionKey="volunteerX.availability.description"
      action={
        <button
          className="btn btn-outline"
          type="button"
          onClick={() => setRefreshToken((value) => value + 1)}
          disabled={loading}
          aria-busy={loading}
        >
          <Icon name="clock" size={16} /> {t("volunteerX.shared.refresh")}
        </button>
      }
    >
      {notice ? <PortalNotice>{notice}</PortalNotice> : null}
      {formError ? <PortalNotice tone="error">{formError}</PortalNotice> : null}
      {error ? (
        <div className="volunteer-error-bar" role="alert">
          <Icon name="info" size={17} />
          <span>{error}</span>
          <button className="btn btn-primary" type="button" onClick={() => setRefreshToken((value) => value + 1)}>
            {t("volunteerX.shared.retry")}
          </button>
        </div>
      ) : null}

      <div className="volunteer-availability-grid">
        <PortalSection title={t("volunteerX.availability.addTitle")} description={t("volunteerX.availability.localTimeNote")}>
          <form className="volunteer-availability-form" onSubmit={(event) => void onSubmit(event)} noValidate>
            <div className="volunteer-time-fields">
              <div>
                <label htmlFor="volunteer-availability-start">
                  <Icon name="calendar" size={16} /> {t("volunteerX.availability.startsLabel")}
                </label>
                <input
                  id="volunteer-availability-start"
                  type="datetime-local"
                  value={start}
                  min={toDateTimeLocal(new Date().toISOString())}
                  onChange={(event) => setStart(event.target.value)}
                />
              </div>
              <div>
                <label htmlFor="volunteer-availability-end">
                  <Icon name="clock" size={16} /> {t("volunteerX.availability.endsLabel")}
                </label>
                <input
                  id="volunteer-availability-end"
                  type="datetime-local"
                  value={end}
                  min={start || toDateTimeLocal(new Date().toISOString())}
                  onChange={(event) => setEnd(event.target.value)}
                />
              </div>
            </div>
            <button className="btn btn-primary" type="submit" disabled={saving} aria-busy={saving}>
              <Icon name="plus" size={16} />
              {saving ? t("volunteerX.availability.saving") : t("volunteerX.availability.addButton")}
            </button>
          </form>
        </PortalSection>

        <PortalSection
          title={t("volunteerX.availability.savedTitle")}
          description={t("volunteerX.availability.count", { count: windows.length })}
        >
          {loading ? (
            <PortalState kind="loading" title={t("volunteerX.availability.loadingTitle")}>
              {t("volunteerX.availability.loadingBody")}
            </PortalState>
          ) : error ? (
            <PortalState kind="error" title={t("volunteerX.availability.errorTitle")}>
              {error}
            </PortalState>
          ) : windows.length === 0 ? (
            <PortalState kind="empty" title={t("volunteerX.availability.emptyTitle")}>
              {t("volunteerX.availability.emptyBody")}
            </PortalState>
          ) : (
            <div className="volunteer-availability-list">
              {windows.map((window) => (
                <article className="volunteer-availability-item" key={window.id}>
                  <span aria-hidden="true"><Icon name="calendar" size={18} /></span>
                  <div>
                    <b>{formatDate(backendTimestamp(window.startsAt), locale)}</b>
                    <small>{t("volunteerX.availability.endsLabel")}: {formatDate(backendTimestamp(window.endsAt), locale)}</small>
                  </div>
                </article>
              ))}
            </div>
          )}
          <p className="volunteer-field-help">
            <Icon name="info" size={14} /> {t("volunteerX.availability.noEditNote")}
          </p>
        </PortalSection>
      </div>
    </VolunteerPage>
  );
}
