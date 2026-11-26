"use client";

import Link from "next/link";
import { campaignIcon, formatDonationDate } from "@/lib/donor";
import { formatMoney, type CampaignDto } from "@/lib/donations";
import { useI18n } from "@/lib/i18n";
import { Icon } from "./Icon";

/** Shows only campaign fields returned by the API. Never invents category, area or urgency. */
export function CampaignCard({
  campaign,
  compact = false,
  supportHref,
  onSupport
}: {
  campaign: CampaignDto;
  compact?: boolean;
  supportHref?: string;
  onSupport?: (campaign: CampaignDto) => void;
}) {
  const { t, locale } = useI18n();
  const progress = Math.max(0, Math.min(100, Math.round(Number(campaign.progressPercent) || 0)));
  const goalReached = progress >= 100;

  return (
    <article className={`donor-campaign-card${compact ? " donor-campaign-card--compact" : ""}`}>
      <div className="donor-campaign-card__head">
        <span className="donor-campaign-card__icon" aria-hidden="true">
          <Icon name={campaignIcon(campaign)} size={compact ? 17 : 21} />
        </span>
        <h3>{campaign.name}</h3>
      </div>

      {campaign.description && !compact ? (
        <p className="donor-campaign-card__description">{campaign.description}</p>
      ) : null}

      <div className="donor-campaign-card__progress">
        <div className="donor-campaign-card__progress-top">
          <span>{goalReached ? t("donorX.needs.complete") : t("donorX.needs.percent", { percent: progress })}</span>
          <span>{formatMoney(campaign.raisedAmount, campaign.currency)}</span>
        </div>
        <div
          className="donor-progress"
          role="progressbar"
          aria-valuenow={progress}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-label={t("donorX.needs.percent", { percent: progress })}
        >
          <span style={{ width: `${progress}%` }} />
        </div>
      </div>

      <dl className="donor-campaign-card__facts">
        <div>
          <dt>{t("donorX.needs.goal")}</dt>
          <dd>{formatMoney(campaign.goalAmount, campaign.currency)}</dd>
        </div>
        <div>
          <dt>{t("donorX.needs.remaining")}</dt>
          <dd>{formatMoney(campaign.remainingAmount, campaign.currency)}</dd>
        </div>
      </dl>

      {campaign.endsAt ? (
        <p className="donor-campaign-card__deadline">
          <Icon name="calendar" size={14} />
          {t("donorX.needs.ends", { date: formatDonationDate(campaign.endsAt, locale) })}
        </p>
      ) : null}

      {supportHref ? (
        <Link className="btn btn-primary donor-campaign-card__cta" href={supportHref}>
          {t("donorX.needs.support")} <Icon name="arrow" size={15} />
        </Link>
      ) : onSupport ? (
        <button
          className="btn btn-primary donor-campaign-card__cta"
          type="button"
          onClick={() => onSupport(campaign)}
        >
          {t("donorX.needs.support")} <Icon name="arrow" size={15} />
        </button>
      ) : null}
    </article>
  );
}
