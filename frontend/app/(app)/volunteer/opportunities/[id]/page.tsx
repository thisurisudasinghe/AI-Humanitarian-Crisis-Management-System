"use client";

import dynamic from "next/dynamic";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import type { MapMarker } from "@/components/map/MapView";
import { Icon } from "@/components/safeway/Icon";
import { PortalSection, PortalState } from "@/components/safeway/PortalPrimitives";
import { VolunteerPage } from "@/components/safeway/VolunteerPage";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useI18n } from "@/lib/i18n";
import { formatDate } from "@/lib/i18n/core";
import { getVolunteerPortal, type VolunteerTask } from "@/lib/portal";
import {
  backendTimestamp,
  hasValidPoint,
  mapLink,
  parseBackendArray,
  skillLabel,
  taskIcon,
  taskTone
} from "@/lib/volunteer";

const MapView = dynamic(() => import("@/components/map/MapView"), { ssr: false });

export default function VolunteerOpportunityDetailPage() {
  const params = useParams<{ id: string }>();
  const id = typeof params.id === "string" ? params.id : "";
  const { t, locale } = useI18n();
  const [task, setTask] = useState<VolunteerTask | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [refreshToken, setRefreshToken] = useState(0);

  useEffect(() => {
    let active = true;
    setLoading(true);
    getVolunteerPortal()
      .then((result) => {
        if (!active) return;
        setTask(result.opportunities.find((item) => item.id === id) ?? null);
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
  }, [id, refreshToken, t]);

  const skills = task ? parseBackendArray(task.requiredSkillsJson) : [];
  const validLocation = task ? hasValidPoint(task.lat, task.lng) : false;
  const markers: MapMarker[] =
    task && validLocation
      ? [{ id: task.id, lat: task.lat as number, lng: task.lng as number, label: task.title, type: "opportunity" }]
      : [];

  return (
    <VolunteerPage
      eyebrowKey="volunteerX.opportunity.detailEyebrow"
      titleKey="volunteerX.opportunity.detailTitle"
      descriptionKey="volunteerX.opportunity.description"
      action={
        <Link className="btn btn-outline" href="/volunteer/opportunities">
          <Icon name="arrowLeft" size={16} /> {t("volunteerX.opportunity.back")}
        </Link>
      }
    >
      {loading ? (
        <PortalState kind="loading" title={t("volunteerX.opportunity.loadingTitle")}>
          {t("volunteerX.opportunity.loadingBody")}
        </PortalState>
      ) : error ? (
        <div className="volunteer-error-bar" role="alert">
          <Icon name="info" size={17} />
          <span>{error}</span>
          <button className="btn btn-primary" type="button" onClick={() => setRefreshToken((value) => value + 1)}>
            {t("volunteerX.shared.retry")}
          </button>
        </div>
      ) : !task ? (
        <PortalState kind="empty" title={t("volunteerX.opportunity.notFoundTitle")}>
          {t("volunteerX.opportunity.notFoundBody")}
        </PortalState>
      ) : (
        <>
          <section className="volunteer-detail-hero">
            <span className="volunteer-detail-hero__icon" aria-hidden="true">
              <Icon name={taskIcon(task)} size={28} />
            </span>
            <div>
              <h2>{task.title}</h2>
              <span className={`badge ${taskTone("Open")}`}>
                <i aria-hidden="true" /> {t("volunteerX.shared.status.Open")}
              </span>
            </div>
          </section>

          <div className="volunteer-detail-grid">
            <PortalSection title={t("volunteerX.opportunity.descriptionLabel")}>
              <p>{t("volunteerX.shared.noDescription")}</p>
              <dl className="volunteer-facts">
                <div>
                  <dt><Icon name="clock" size={16} /> {t("volunteerX.opportunity.scheduledLabel")}</dt>
                  <dd>{task.scheduledAt ? formatDate(backendTimestamp(task.scheduledAt), locale) : t("volunteerX.shared.scheduleTbc")}</dd>
                </div>
                <div>
                  <dt><Icon name="pin" size={16} /> {t("volunteerX.opportunity.availabilityLabel")}</dt>
                  <dd>
                    {validLocation ? (
                      <a href={mapLink(task.lat as number, task.lng as number)} target="_blank" rel="noreferrer">
                        {t("volunteerX.shared.locationOnMap")} <Icon name="arrow" size={13} />
                      </a>
                    ) : (
                      t("volunteerX.shared.locationNotSpecified")
                    )}
                  </dd>
                </div>
              </dl>
            </PortalSection>

            <PortalSection title={t("volunteerX.opportunity.requestedSkills")}>
              {skills.length > 0 ? (
                <div className="volunteer-chip-list">
                  {skills.map((skill) => (
                    <span className="volunteer-chip is-static" key={skill}>
                      <Icon name="checkCircle" size={15} /> {skillLabel(t, skill)}
                    </span>
                  ))}
                </div>
              ) : (
                <p>{t("volunteerX.opportunity.noSkills")}</p>
              )}
            </PortalSection>
          </div>

          {markers.length > 0 ? (
            <PortalSection title={t("volunteerX.shared.openMap")}>
              <div className="volunteer-map-frame">
                <MapView markers={markers} height={260} />
              </div>
            </PortalSection>
          ) : null}

          <div className="volunteer-info-panel" role="note">
            <Icon name="clipboard" size={18} />
            <div>
              <b>{t("volunteerX.opportunity.coordinatorNoteTitle")}</b>
              <p>{t("volunteerX.shared.coordinatorControlled")}</p>
            </div>
          </div>
        </>
      )}
    </VolunteerPage>
  );
}
