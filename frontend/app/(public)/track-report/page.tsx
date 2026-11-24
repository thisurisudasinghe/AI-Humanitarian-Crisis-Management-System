"use client";

import Link from "next/link";
import { useEffect, useState, type FormEvent } from "react";
import { ApiError, apiJson } from "@/lib/api";
import { Icon } from "@/components/safeway/Icon";
import { useI18n, useT } from "@/lib/i18n";
import { formatDate } from "@/lib/i18n/core";
import { localizedErrorMessage } from "@/lib/i18n/errors";

const TRACK_TOKEN_KEY = "safeway.guestTrackingToken";

interface TimelineEntry {
  status: string;
  at: string;
}

interface GuestTrackingResult {
  reference: string;
  incidentType: string;
  location: string;
  status: string;
  submittedAt: string;
  updatedAt: string;
  timeline: TimelineEntry[];
}

function guestStatusLabel(t: (key: string) => string, status: string): string {
  const key = `pw.guestTrack.status.${status}`;
  const label = t(key);
  return label === key ? t("pw.common.statusUnknown") : label;
}

function incidentLabel(t: (key: string) => string, type: string): string {
  const key = `pw.guestReport.category${type}`;
  const label = t(key);
  return label === key ? t("pw.common.notProvided") : label;
}

export default function TrackReportPage() {
  const t = useT();
  const { locale } = useI18n();
  const [token, setToken] = useState("");
  const [showToken, setShowToken] = useState(false);
  const [ready, setReady] = useState(false);
  const [result, setResult] = useState<GuestTrackingResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    try {
      const stored = window.sessionStorage.getItem(TRACK_TOKEN_KEY);
      if (stored) setToken(stored);
    } catch {
      // The token can always be pasted from the submission confirmation.
    }
    setReady(true);
  }, []);

  async function track(event?: FormEvent<HTMLFormElement>) {
    event?.preventDefault();
    const trimmed = token.trim();
    if (!trimmed) {
      setError(t("pw.guestTrack.tokenRequired"));
      setResult(null);
      return;
    }

    setBusy(true);
    setError(null);
    try {
      const found = await apiJson<GuestTrackingResult>("/api/v1/public/guest-reports/track", "POST", {
        token: trimmed
      });
      setResult(found);
      try {
        window.sessionStorage.setItem(TRACK_TOKEN_KEY, trimmed);
      } catch {
        // Session storage is a convenience, not a requirement for tracking.
      }
    } catch (cause) {
      setResult(null);
      if (cause instanceof ApiError && (cause.status === 400 || cause.status === 404)) {
        setError(t("pw.guestTrack.invalidToken"));
      } else {
        setError(localizedErrorMessage(cause, t));
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="public-info pw-page">
      <header className="page-hero">
        <div className="page-hero-lines" />
        <div className="eyebrow">
          <span /> {t("pw.guestTrack.eyebrow")}
        </div>
        <h1>{t("pw.guestTrack.title")}</h1>
        <p>{t("pw.guestTrack.intro")}</p>
      </header>

      <div className="page-body">
        <form className="pw-card pw-form pw-track-form" onSubmit={(event) => void track(event)} noValidate>
          <h2>{t("pw.guestTrack.formTitle")}</h2>
          <p className="pw-form__intro">{t("pw.guestTrack.formIntro")}</p>

          <label className="pw-field">
            <span className="pw-field__label">{t("pw.guestTrack.tokenLabel")}</span>
            <span className="pw-token-field">
              <input
                type={showToken ? "text" : "password"}
                value={token}
                onChange={(event) => setToken(event.target.value)}
                placeholder={t("pw.guestTrack.tokenPlaceholder")}
                autoComplete="off"
                disabled={!ready}
              />
              <button
                className="btn btn-outline"
                type="button"
                aria-pressed={showToken}
                onClick={() => setShowToken((value) => !value)}
              >
                {showToken ? t("pw.guestTrack.hideToken") : t("pw.guestTrack.showToken")}
              </button>
            </span>
            <small>{t("pw.guestTrack.tokenHint")}</small>
          </label>

          {error ? <p className="pw-form-error" role="alert">{error}</p> : null}

          <button className="btn btn-primary pw-submit" type="submit" disabled={busy || !ready}>
            {busy ? t("pw.guestTrack.tracking") : t("pw.guestTrack.track")}
            <Icon name="arrow" size={16} />
          </button>
        </form>

        {!result && !error ? (
          <section className="pw-card pw-card--blue pw-state" aria-labelledby="pw-track-empty">
            <span className="pw-card__icon" aria-hidden="true">
              <Icon name="search" size={22} />
            </span>
            <h2 id="pw-track-empty">{t("pw.guestTrack.emptyTitle")}</h2>
            <p>{t("pw.guestTrack.emptyBody")}</p>
          </section>
        ) : null}

        {result ? (
          <section className="pw-section" aria-labelledby="pw-track-result">
            <div className="pw-section__head">
              <h2 id="pw-track-result">{t("pw.guestTrack.resultTitle")}</h2>
              <p>{t("pw.guestTrack.resultBody")}</p>
            </div>

            <article className="pw-card pw-result-card">
              <dl className="pw-facts">
                <div>
                  <dt>{t("pw.common.reference")}</dt>
                  <dd className="pw-facts__reference">{result.reference}</dd>
                </div>
                <div>
                  <dt>{t("pw.guestTrack.incidentType")}</dt>
                  <dd>{incidentLabel(t, result.incidentType)}</dd>
                </div>
                <div>
                  <dt>{t("pw.common.location")}</dt>
                  <dd>{result.location}</dd>
                </div>
                <div>
                  <dt>{t("pw.common.status")}</dt>
                  <dd>{guestStatusLabel(t, result.status)}</dd>
                </div>
                <div>
                  <dt>{t("pw.guestTrack.submitted")}</dt>
                  <dd>{formatDate(result.submittedAt, locale)}</dd>
                </div>
                <div>
                  <dt>{t("pw.guestTrack.updated")}</dt>
                  <dd>{formatDate(result.updatedAt, locale)}</dd>
                </div>
              </dl>
            </article>

            <article className="pw-card pw-card--blue" aria-labelledby="pw-track-timeline">
              <h3 id="pw-track-timeline">{t("pw.guestTrack.timelineTitle")}</h3>
              {result.timeline.length === 0 ? (
                <p className="pw-muted">{t("pw.guestTrack.timelineEmpty")}</p>
              ) : (
                <ol className="pw-timeline">
                  {result.timeline.map((entry, index) => (
                    <li key={`${entry.status}-${entry.at}-${index}`}>
                      <span className="pw-timeline__dot" aria-hidden="true" />
                      <b>{guestStatusLabel(t, entry.status)}</b>
                      <time>{formatDate(entry.at, locale)}</time>
                    </li>
                  ))}
                </ol>
              )}
              <p className="pw-muted pw-timeline__note">{t("pw.guestTrack.timelineNote")}</p>
            </article>
          </section>
        ) : null}

        <aside className="pw-callout pw-callout--blue" role="note">
          <span className="pw-callout__icon" aria-hidden="true">
            <Icon name="info" size={20} />
          </span>
          <div>
            <b>{t("pw.guestTrack.privacyTitle")}</b>
            <p>{t("pw.guestTrack.privacyBody")}</p>
          </div>
          <Link href="/guest-report">{t("pw.guestTrack.newReportLink")} <Icon name="arrow" size={14} /></Link>
        </aside>
      </div>
    </main>
  );
}
