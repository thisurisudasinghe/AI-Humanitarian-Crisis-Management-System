"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { CampaignCard } from "@/components/safeway/CampaignCard";
import { DonationCard } from "@/components/safeway/DonationCard";
import { DonorPage } from "@/components/safeway/DonorPage";
import { Icon } from "@/components/safeway/Icon";
import { PortalSection, PortalState } from "@/components/safeway/PortalPrimitives";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useT } from "@/lib/i18n";
import {
  getDonorCampaigns,
  getDonorDonations,
  getDonorProfile,
  type CampaignDto,
  type DonationDto,
  type DonorDto
} from "@/lib/portal";

const ACTION_CARDS = [
  {
    href: "/donor/donate",
    icon: "handHeart",
    className: "donor-action-card--green",
    titleKey: "donorX.landing.cardDonateTitle",
    copyKey: "donorX.landing.cardDonateCopy",
    ctaKey: "donorX.landing.cardDonateCta"
  },
  {
    href: "/donor/needs",
    icon: "box",
    className: "donor-action-card--blue",
    titleKey: "donorX.landing.cardNeedsTitle",
    copyKey: "donorX.landing.cardNeedsCopy",
    ctaKey: "donorX.landing.cardNeedsCta"
  },
  {
    href: "/donor/donations",
    icon: "clipboard",
    className: "donor-action-card--blue",
    titleKey: "donorX.landing.cardHistoryTitle",
    copyKey: "donorX.landing.cardHistoryCopy",
    ctaKey: "donorX.landing.cardHistoryCta"
  },
  {
    href: "/donor/impact",
    icon: "chart",
    className: "donor-action-card--green",
    titleKey: "donorX.landing.cardImpactTitle",
    copyKey: "donorX.landing.cardImpactCopy",
    ctaKey: "donorX.landing.cardImpactCta"
  }
] as const;

type ProfileState = "loading" | "ready" | "missing" | "error";
type ListState = "loading" | "ready" | "error";

export default function DonorPortalPage() {
  const t = useT();
  const { enroll } = useAuth();
  const [profile, setProfile] = useState<DonorDto | null>(null);
  const [profileState, setProfileState] = useState<ProfileState>("loading");
  const [profileError, setProfileError] = useState<string | null>(null);
  const [activating, setActivating] = useState(false);
  const [donations, setDonations] = useState<DonationDto[]>([]);
  const [donationState, setDonationState] = useState<ListState>("loading");
  const [donationError, setDonationError] = useState<string | null>(null);
  const [campaigns, setCampaigns] = useState<CampaignDto[]>([]);
  const [campaignState, setCampaignState] = useState<ListState>("loading");
  const [campaignError, setCampaignError] = useState<string | null>(null);
  const [refresh, setRefresh] = useState(0);

  const loadProfile = useCallback(() => {
    setProfileState("loading");
    getDonorProfile()
      .then((result) => {
        setProfile(result);
        setProfileState("ready");
        setProfileError(null);
      })
      .catch((cause) => {
        setProfile(null);
        setProfileState(
          cause instanceof ApiError && cause.code === "DONOR_PROFILE_NOT_FOUND" ? "missing" : "error"
        );
        setProfileError(localizedErrorMessage(cause, t));
      });
  }, [t]);

  useEffect(() => {
    loadProfile();
  }, [loadProfile, refresh]);

  useEffect(() => {
    let active = true;
    setDonationState("loading");
    getDonorDonations(1, 3)
      .then((result) => {
        if (!active) return;
        setDonations(result.items);
        setDonationState("ready");
        setDonationError(null);
      })
      .catch((cause) => {
        if (!active) return;
        setDonationState("error");
        setDonationError(localizedErrorMessage(cause, t));
      });
    return () => {
      active = false;
    };
  }, [t, refresh]);

  useEffect(() => {
    let active = true;
    setCampaignState("loading");
    getDonorCampaigns(1, 3)
      .then((result) => {
        if (!active) return;
        setCampaigns(result.items);
        setCampaignState("ready");
        setCampaignError(null);
      })
      .catch((cause) => {
        if (!active) return;
        setCampaignState("error");
        setCampaignError(localizedErrorMessage(cause, t));
      });
    return () => {
      active = false;
    };
  }, [t, refresh]);

  async function completeProfile() {
    setActivating(true);
    setProfileError(null);
    try {
      await enroll("donor");
      setRefresh((value) => value + 1);
    } catch (cause) {
      setProfileError(localizedErrorMessage(cause, t));
    } finally {
      setActivating(false);
    }
  }

  return (
    <DonorPage
      eyebrowKey="donorX.landing.eyebrow"
      titleKey="donorX.landing.title"
      descriptionKey="donorX.landing.support"
      showBack={false}
    >
      <nav className="donor-action-grid" aria-label={t("donorX.landing.actionsAria")}>
        {ACTION_CARDS.map((card) => (
          <Link className={`donor-action-card ${card.className}`} href={card.href} key={card.href}>
            <span className="donor-action-card__icon" aria-hidden="true">
              <Icon name={card.icon} size={23} />
            </span>
            <span className="donor-action-card__body">
              <b>{t(card.titleKey)}</b>
              <span>{t(card.copyKey)}</span>
            </span>
            <span className="donor-action-card__cta">
              {t(card.ctaKey)} <Icon name="arrow" size={16} />
            </span>
          </Link>
        ))}
      </nav>

      <div className="donor-landing-columns">
        <PortalSection
          title={t("donorX.landing.recentTitle")}
          description={t("donorX.landing.recentDescription")}
          action={
            <Link className="donor-inline-link" href="/donor/donations">
              {t("donorX.landing.recentViewAll")} <Icon name="arrow" size={15} />
            </Link>
          }
        >
          {donationState === "loading" ? (
            <PortalState kind="loading" title={t("donorX.landing.recentLoadingTitle")}>
              {t("donorX.landing.recentLoadingBody")}
            </PortalState>
          ) : donationState === "error" ? (
            <PortalState kind="error" title={t("donorX.landing.recentErrorTitle")}>
              {donationError}
            </PortalState>
          ) : donations.length === 0 ? (
            <PortalState
              kind="empty"
              title={t("donorX.landing.recentEmptyTitle")}
              href="/donor/donate"
              linkLabel={t("donorX.landing.cardDonateCta")}
            >
              {t("donorX.landing.recentEmptyBody")}
            </PortalState>
          ) : (
            <div className="donor-recent-list">
              {donations.map((donation) => (
                <DonationCard compact donation={donation} key={donation.id} />
              ))}
            </div>
          )}
        </PortalSection>

        <div className="donor-landing-side">
          <PortalSection
            title={t("donorX.landing.needsPreviewTitle")}
            description={t("donorX.landing.needsPreviewDescription")}
          >
            {campaignState === "loading" ? (
              <PortalState kind="loading" title={t("donorX.needs.loadingTitle")}>
                {t("donorX.needs.loadingBody")}
              </PortalState>
            ) : campaignState === "error" ? (
              <PortalState kind="error" title={t("donorX.needs.errorTitle")}>
                {campaignError}
              </PortalState>
            ) : campaigns.length === 0 ? (
              <PortalState kind="empty" title={t("donorX.landing.needsPreviewEmpty")}>
                {t("donorX.needs.emptyBody")}
              </PortalState>
            ) : (
              <div className="donor-needs-preview">
                {campaigns.map((campaign) => (
                  <CampaignCard compact campaign={campaign} key={campaign.id} />
                ))}
              </div>
            )}
            <Link className="donor-inline-link donor-landing-side__link" href="/donor/needs">
              {t("donorX.landing.needsPreviewCta")} <Icon name="arrow" size={15} />
            </Link>
          </PortalSection>

          <section className="donor-profile-card" aria-labelledby="donor-profile-heading">
            <span className="donor-profile-card__icon" aria-hidden="true">
              <Icon name="user" size={19} />
            </span>
            <div>
              <h2 id="donor-profile-heading">{t("donorX.profile.title")}</h2>
              {profileState === "loading" ? (
                <p>{t("authGuard.checking")}</p>
              ) : profileState === "ready" && profile ? (
                <>
                  <p>{t("donorX.landing.profileReady")}</p>
                  <dl className="donor-profile-card__facts">
                    <div>
                      <dt>{t("donorX.profile.name")}</dt>
                      <dd>{profile.name}</dd>
                    </div>
                    <div>
                      <dt>{t("donorX.profile.email")}</dt>
                      <dd>{profile.contactEmail || t("donorX.profile.noEmail")}</dd>
                    </div>
                    <div>
                      <dt>{t("donorX.profile.type")}</dt>
                      <dd>{profile.donorType}</dd>
                    </div>
                  </dl>
                </>
              ) : profileState === "missing" ? (
                <>
                  <h3>{t("donorX.landing.profileMissingTitle")}</h3>
                  <p>{t("donorX.landing.profileMissingBody")}</p>
                  <button
                    className="btn btn-primary"
                    type="button"
                    onClick={() => void completeProfile()}
                    disabled={activating}
                  >
                    {activating ? t("donorX.profile.completing") : t("donorX.landing.profileComplete")}
                  </button>
                </>
              ) : (
                <>
                  <h3>{t("donorX.landing.profileErrorTitle")}</h3>
                  <p>{profileError || t("donorX.landing.profileErrorBody")}</p>
                  <button
                    className="btn btn-outline"
                    type="button"
                    onClick={() => setRefresh((value) => value + 1)}
                  >
                    <Icon name="arrow" size={15} /> {t("donorX.shared.retry")}
                  </button>
                </>
              )}
            </div>
          </section>
        </div>
      </div>

      <p className="donor-landing-note">
        <Icon name="info" size={15} /> {t("donorX.landing.note")}
      </p>
    </DonorPage>
  );
}
