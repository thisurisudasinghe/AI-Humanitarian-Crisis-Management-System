"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Icon } from "@/components/safeway/Icon";
import { PortalHeader, PortalState } from "@/components/safeway/PortalPrimitives";
import { api } from "@/lib/api";
import { useI18n } from "@/lib/i18n";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { formatDate } from "@/lib/i18n/core";

interface PublicAlert {
  id: string;
  title: string;
  body: string;
  status: string;
  publishedAt?: string | null;
  createdAt: string;
}

export default function PublicAlertsPage() {
  const { t, locale } = useI18n();
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
    <div className="citizen-alerts-page">
      <Link className="back-link citizen-detail-back" href="/portal/citizen">
        <Icon name="arrowLeft" size={16} /> {t("citizenX.report.backPortal")}
      </Link>

      <PortalHeader
        eyebrow={t("citizenX.alertsPage.eyebrow")}
        title={t("citizenX.alertsPage.title")}
        description={t("citizenX.alertsPage.description")}
        action={
          <button
            className="btn btn-outline"
            type="button"
            onClick={() => setRefreshToken((value) => value + 1)}
            disabled={loading || refreshing}
            aria-busy={refreshing}
          >
            <Icon name="clock" size={16} />
            {refreshing ? t("citizenX.alertsPage.refreshing") : t("citizenX.alertsPage.refresh")}
          </button>
        }
      />

      <aside className="citizen-alerts__distinction" role="note">
        <i aria-hidden="true"><Icon name="bell" size={20} /></i>
        <div>
          <b>{t("citizenX.alertsPage.notificationsNoteTitle")}</b>
          <p>{t("citizenX.landing.personalNote")}</p>
        </div>
        <Link className="btn btn-outline" href="/notifications">
          {t("citizenX.alertsPage.notificationsLink")} <Icon name="arrow" size={16} />
        </Link>
      </aside>

      {error ? (
        <div className="citizen-error-bar" role="alert">
          <Icon name="info" size={18} />
          <span>{error}</span>
          <button className="btn btn-outline" type="button" onClick={() => setRefreshToken((value) => value + 1)}>
            {t("citizenX.alertsPage.refresh")}
          </button>
        </div>
      ) : null}

      {loading ? (
        <PortalState kind="loading" title={t("citizenX.alertsPage.loadingTitle")}>
          {t("citizenX.alertsPage.loadingBody")}
        </PortalState>
      ) : alerts.length === 0 ? (
        <PortalState kind="empty" title={t("citizenX.alertsPage.emptyTitle")}>
          {t("citizenX.alertsPage.emptyBody")}
        </PortalState>
      ) : (
        <section className="citizen-alerts-list" aria-label={t("citizenX.alertsPage.title")}>
          {alerts.map((alert) => (
            <article className="citizen-alert-card" key={alert.id}>
              <div className="citizen-alert-card__head">
                <i aria-hidden="true"><Icon name="megaphone" size={22} /></i>
                <div>
                  <h2>{alert.title}</h2>
                  <p>
                    <Icon name="clock" size={14} /> {t("citizenX.alertsPage.published")}:{" "}
                    {formatDate(alert.publishedAt ?? alert.createdAt, locale)}
                  </p>
                </div>
              </div>
              <p className="citizen-alert-card__body">{alert.body}</p>
            </article>
          ))}
        </section>
      )}
    </div>
  );
}
