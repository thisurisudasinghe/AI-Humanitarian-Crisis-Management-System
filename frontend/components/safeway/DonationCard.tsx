"use client";

import Link from "next/link";
import { donationIcon, donationStatusLabel, donationStatusTone, donationSummary, formatDonationDate } from "@/lib/donor";
import type { DonationDto } from "@/lib/donations";
import { useI18n } from "@/lib/i18n";
import { Icon } from "./Icon";

/** A single whole-card link. Never place another control inside this card. */
export function DonationCard({
  donation,
  compact = false
}: {
  donation: DonationDto;
  compact?: boolean;
}) {
  const { t, locale } = useI18n();
  const label = donationStatusLabel(t, donation.status);

  return (
    <Link
      className={`donor-donation-card${compact ? " donor-donation-card--compact" : ""}`}
      href={`/donor/donations/${encodeURIComponent(donation.id)}`}
      aria-label={`${donation.reference} — ${label}`}
    >
      <span className="donor-donation-card__icon" aria-hidden="true">
        <Icon name={donationIcon(donation)} size={compact ? 18 : 22} />
      </span>
      <span className="donor-donation-card__body">
        <span className="donor-donation-card__top">
          <b>{donation.reference}</b>
          <span className={`donor-badge donor-badge--${donationStatusTone(donation.status)}`}>{label}</span>
        </span>
        <span className="donor-donation-card__summary">{donationSummary(t, donation)}</span>
        <span className="donor-donation-card__meta">
          <Icon name="clock" size={13} />
          {formatDonationDate(donation.createdAt || donation.receivedAt, locale)}
        </span>
      </span>
      <span className="donor-donation-card__arrow" aria-hidden="true">
        <Icon name="chevron" size={17} />
      </span>
    </Link>
  );
}
