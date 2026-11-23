"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { useI18n } from "@/lib/i18n";
import { Icon } from "./Icon";

interface PublicAlert {
  id: string;
  crisisId?: string | null;
  title: string;
  body: string;
  status: string;
  publishedAt?: string | null;
  createdAt: string;
}

interface PublicServiceCentre {
  id: string;
  crisisId?: string | null;
  name: string;
  address: string;
  lat: number;
  lng: number;
  services: string;
  openingHours?: string | null;
}

export function PublicCaseUpdates() {
  const { t, locale } = useI18n();
  const [alerts, setAlerts] = useState<PublicAlert[]>([]);
  const [centres, setCentres] = useState<PublicServiceCentre[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const loc = locale === "si" ? "si-LK" : locale === "ta" ? "ta-LK" : "en-LK";
  const formatDate = (value?: string | null) => {
    if (!value) return "—";
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? "—" : date.toLocaleString(loc);
  };

  useEffect(() => {
    let active = true;
    Promise.all([
      api<PublicAlert[]>("/api/v1/public/alerts"),
      api<PublicServiceCentre[]>("/api/v1/public/service-centres")
    ])
      .then(([alertRows, centreRows]) => {
        if (!active) return;
        setAlerts(alertRows);
        setCentres(centreRows);
        setError(null);
      })
      .catch(() => {
        if (!active) return;
        setError(t("common.alertsError"));
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => { active = false; };
  }, [t]);

  return (
    <>
      <section className="public-live-updates" aria-label={t("common.publicAlertsTitle")}>
        <div className="section-head">
          <div>
            <div className="eyebrow dark"><span /> {t("common.publicAlertsEyebrow")}</div>
            <h2>{t("common.publicAlertsTitle")}</h2>
          </div>
        </div>
        {loading ? <p className="muted">{t("common.loadingAlerts")}</p> : null}
        {error ? <p className="form-error" role="alert">{error}</p> : null}
        {!loading && !error && alerts.length === 0 ? (
          <p className="muted">{t("common.noAlerts")}</p>
        ) : null}
        <div className="value-grid">
          {alerts.slice(0, 6).map((alert) => (
            <article key={alert.id}>
              <i><Icon name="alert" /></i>
              <h3>{alert.title}</h3>
              <p>{alert.body}</p>
              <p className="muted"><small>{t("common.published")}: {formatDate(alert.publishedAt ?? alert.createdAt)}</small></p>
            </article>
          ))}
        </div>
      </section>

      <section className="public-live-updates" aria-label={t("common.serviceCentresTitle")}>
        <div className="section-head">
          <div>
            <div className="eyebrow dark"><span /> {t("common.serviceCentresEyebrow")}</div>
            <h2>{t("common.serviceCentresTitle")}</h2>
          </div>
        </div>
        {!loading && !error && centres.length === 0 ? (
          <p className="muted">{t("common.noCentres")}</p>
        ) : null}
        <div className="value-grid">
          {centres.slice(0, 6).map((centre) => (
            <article key={centre.id}>
              <i><Icon name="pin" /></i>
              <h3>{centre.name}</h3>
              <p>{centre.address}</p>
              <p className="muted"><small>{t("common.servicesLabel")}: {centre.services || "—"}</small></p>
              <p className="muted"><small>{t("common.hoursLabel")}: {centre.openingHours || "—"}</small></p>
            </article>
          ))}
        </div>
      </section>
    </>
  );
}
