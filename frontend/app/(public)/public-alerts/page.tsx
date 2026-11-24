"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Icon } from "@/components/safeway/Icon";
import { PortalState } from "@/components/safeway/PortalPrimitives";
import { api } from "@/lib/api";
import { useI18n, useT } from "@/lib/i18n";
import { formatDate } from "@/lib/i18n/core";
import { localizedErrorMessage } from "@/lib/i18n/errors";

interface PublicAlert {
  id: string;
  title: string;
  summary: string;
  type: string;
  severity?: string | null;
  status: string;
  district?: string | null;
  publishedAt?: string | null;
  expiresAt?: string | null;
  latitude?: number | null;
  longitude?: number | null;
}

function dictionaryLabel(t: (key: string) => string, namespace: string, value: string): string {
  const key = `${namespace}.${value}`;
  const label = t(key);
  return label === key ? t("pw.common.notProvided") : label;
}

export default function PublicAlertsPage() {
  const t = useT();
  const { locale } = useI18n();
  const [alerts, setAlerts] = useState<PublicAlert[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [refreshToken, setRefreshToken] = useState(0);

  useEffect(() => {
    let active = true;
    if (refreshToken === 0) setLoading(true);
    else setRefreshing(true);

    api<PublicAlert[]>("/api/v1/public/alerts")
      .then((rows) => {
        if (!active) return;
        setAlerts(rows);
        setError(null);
      })
      .catch((cause) => {
        if (active) setError(localizedErrorMessage(cause, t));
      })
      .finally(() => {
        if (!active) return;
        setLoading(false);
        setRefreshing(false);
      });

    return () => {
      active = false;
    };
  }, [t, refreshToken]);

  return (
    <main className="public-info pw-page">
      <header className="page-hero">
        <div className="page-hero-lines" />
        <div className="eyebrow"><span /> {t("pw.alerts.eyebrow")}</div>
        <h1>{t("pw.alerts.title")}</h1>
        <p>{t("pw.alerts.intro")}</p>
      </header>

      <div className="page-body">
        <aside className="pw-callout pw-callout--blue" role="note">
          <span className="pw-callout__icon" aria-hidden="true">
            <Icon name="megaphone" size={22} />
          </span>
          <div>
            <b>{t("pw.alerts.scopeTitle")}</b>
            <p>{t("pw.alerts.scopeBody")}</p>
          </div>
          <Link href="/support">{t("pw.alerts.supportLink")} <Icon name="arrow" size={14} /></Link>
        </aside>

        <section className="pw-section" aria-labelledby="pw-alerts-list">
          <div className="pw-section__head">
            <h2 id="pw-alerts-list">{t("pw.alerts.listTitle")}</h2>
            <p>{t("pw.alerts.listBody")}</p>
            <button
              className="btn btn-outline"
              type="button"
              onClick={() => setRefreshToken((value) => value + 1)}
              disabled={loading || refreshing}
              aria-busy={refreshing}
            >
              <Icon name="clock" size={16} />
              {refreshing ? t("pw.common.refreshing") : t("pw.common.refresh")}
            </button>
          </div>

          {error ? <p className="pw-form-error" role="alert">{error}</p> : null}

          {loading ? (
            <PortalState kind="loading" title={t("pw.alerts.loadingTitle")}>{t("pw.alerts.loadingBody")}</PortalState>
          ) : alerts.length === 0 ? (
            <PortalState kind="empty" title={t("pw.alerts.emptyTitle")}>{t("pw.alerts.emptyBody")}</PortalState>
          ) : (
            <div className="pw-list">
              {alerts.map((alert) => (
                <article className="pw-card pw-card--amber pw-alert-card" key={alert.id}>
                  <div className="pw-card__topline">
                    <span className="pw-card__icon" aria-hidden="true">
                      <Icon name="bell" size={20} />
                    </span>
                    <div className="pw-alert-card__heading">
                      <h3>{alert.title}</h3>
                      <p className="pw-muted">
                        {dictionaryLabel(t, "pw.alerts.type", alert.type)}
                        {alert.severity ? ` · ${dictionaryLabel(t, "pw.alerts.severity", alert.severity)}` : ""}
                        {alert.district ? ` · ${alert.district}` : ""}
                      </p>
                    </div>
                    <span className="pw-pill pw-pill--amber">{dictionaryLabel(t, "pw.alerts.status", alert.status)}</span>
                  </div>
                  <p>{alert.summary}</p>
                  <dl className="pw-facts pw-facts--compact">
                    <div>
                      <dt>{t("pw.alerts.published")}</dt>
                      <dd>{formatDate(alert.publishedAt, locale)}</dd>
                    </div>
                    <div>
                      <dt>{t("pw.alerts.expires")}</dt>
                      <dd>{formatDate(alert.expiresAt, locale)}</dd>
                    </div>
                  </dl>
                </article>
              ))}
            </div>
          )}
        </section>

        <aside className="pw-callout pw-callout--coral" role="note">
          <span className="pw-callout__icon" aria-hidden="true">
            <Icon name="phone" size={20} />
          </span>
          <div>
            <b>{t("pw.alerts.emergencyTitle")}</b>
            <p>{t("pw.alerts.emergencyBody")}</p>
          </div>
          <Link href="/emergency-services">{t("pw.alerts.emergencyLink")} <Icon name="arrow" size={14} /></Link>
        </aside>
      </div>
    </main>
  );
}
