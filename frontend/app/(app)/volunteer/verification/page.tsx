"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { Icon } from "@/components/safeway/Icon";
import { PortalNotice, PortalSection, PortalState } from "@/components/safeway/PortalPrimitives";
import { VolunteerPage } from "@/components/safeway/VolunteerPage";
import { VolunteerTimeline } from "@/components/safeway/VolunteerTimeline";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useI18n } from "@/lib/i18n";
import { getVolunteerPortal, type VolunteerPortalData } from "@/lib/portal";
import { buildVerificationTimeline, normalizeVerificationStatus } from "@/lib/volunteer";

const NEXT_STEP_KEYS: Record<string, string> = {
  None: "volunteerX.verification.nextStepsMissing",
  Pending: "volunteerX.verification.nextStepsPending",
  Verified: "volunteerX.verification.nextStepsVerifiedActive",
  Rejected: "volunteerX.verification.nextStepsRejected",
  Unknown: "volunteerX.verification.nextStepsUnknown"
};

export default function VolunteerVerificationPage() {
  const { t } = useI18n();
  const [portal, setPortal] = useState<VolunteerPortalData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [refreshToken, setRefreshToken] = useState(0);

  useEffect(() => {
    let active = true;
    setLoading(true);
    getVolunteerPortal()
      .then((result) => {
        if (!active) return;
        setPortal(result);
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

  const status = normalizeVerificationStatus(
    portal?.profile?.verificationStatus,
    Boolean(portal?.profile)
  );
  const steps = buildVerificationTimeline(status);
  const noteKey = useCallback(() => {
    if (status === "Verified" && portal?.profile?.active === false) {
      return "volunteerX.verifyNote.verifiedInactive";
    }
    return NEXT_STEP_KEYS[status] ?? NEXT_STEP_KEYS.Unknown;
  }, [portal?.profile?.active, status])();

  return (
    <VolunteerPage
      eyebrowKey="volunteerX.verification.eyebrow"
      titleKey="volunteerX.verification.title"
      descriptionKey="volunteerX.verification.description"
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
      {loading ? (
        <PortalState kind="loading" title={t("volunteerX.verification.loadingTitle")}>
          {t("volunteerX.verification.loadingBody")}
        </PortalState>
      ) : error ? (
        <div className="volunteer-error-bar" role="alert">
          <Icon name="info" size={17} />
          <span>{error}</span>
          <button className="btn btn-primary" type="button" onClick={() => setRefreshToken((value) => value + 1)}>
            {t("volunteerX.shared.retry")}
          </button>
        </div>
      ) : status === "None" ? (
        <PortalState
          kind="empty"
          title={t("volunteerX.verification.noProfileTitle")}
          href="/volunteer/profile"
          linkLabel={t("volunteerX.verification.editProfile")}
        >
          {t("volunteerX.verification.noProfileBody")}
        </PortalState>
      ) : (
        <>
          <PortalSection title={t("volunteerX.verification.timelineTitle")}>
            <VolunteerTimeline steps={steps} ariaLabelKey="volunteerX.verification.timelineTitle" />
          </PortalSection>

          <section className="volunteer-next-steps" aria-labelledby="volunteer-next-heading">
            <span className="volunteer-next-steps__icon" aria-hidden="true">
              <Icon name={status === "Rejected" ? "info" : "checkCircle"} size={22} />
            </span>
            <div>
              <h2 id="volunteer-next-heading">{t("volunteerX.verification.nextStepsTitle")}</h2>
              <p>{t(noteKey)}</p>
              <p className="volunteer-next-steps__rule">
                <Icon name="shield" size={14} /> {t("volunteerX.verifyNote.coordinatorOnly")}
              </p>
            </div>
          </section>

          {(status === "Rejected" || status === "Unknown") && portal?.profile ? (
            <PortalNotice tone="error">
              {t(`volunteerX.shared.statusNote.${status}`)}
            </PortalNotice>
          ) : null}

          <div className="volunteer-form-actions">
            <Link className="btn btn-primary" href="/volunteer/profile">
              <Icon name="user" size={16} /> {t("volunteerX.verification.editProfile")}
            </Link>
            <Link className="btn btn-outline" href="/volunteer/availability">
              {t("volunteerX.landing.cardAvailabilityCta")} <Icon name="arrow" size={16} />
            </Link>
          </div>
        </>
      )}
    </VolunteerPage>
  );
}
