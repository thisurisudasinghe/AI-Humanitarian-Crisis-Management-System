"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { ApiError } from "@/lib/api";
import { DonorPage } from "@/components/safeway/DonorPage";
import { DonorTimeline } from "@/components/safeway/DonorTimeline";
import { Icon } from "@/components/safeway/Icon";
import { PortalNotice, PortalSection, PortalState } from "@/components/safeway/PortalPrimitives";
import {
  buildDonationTimeline,
  categoryIcon,
  categoryLabel,
  deliveryLabel,
  donationStatusLabel,
  donationStatusNote,
  donationStatusTone,
  donationSummary,
  formatDonationCalendarDate,
  formatDonationDate,
  hasValidPoint,
  mapLink,
  unitLabel
} from "@/lib/donor";
import { formatMoney, type DonationDto } from "@/lib/donations";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useI18n } from "@/lib/i18n";
import { getDonorDonation } from "@/lib/portal";

type LoadState = "loading" | "ready" | "notFound" | "error";

export default function DonorDonationDetailPage() {
  const params = useParams<{ id: string }>();
  const id = typeof params.id === "string" ? params.id : "";
  const { t, locale } = useI18n();
  const [donation, setDonation] = useState<DonationDto | null>(null);
  const [state, setState] = useState<LoadState>("loading");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) {
      setState("notFound");
      return;
    }
    let active = true;
    setState("loading");
    getDonorDonation(id)
      .then((result) => {
        if (!active) return;
        setDonation(result);
        setState("ready");
        setError(null);
      })
      .catch((cause) => {
        if (!active) return;
        setState(
          cause instanceof ApiError && (cause.status === 404 || cause.code === "DONATION_NOT_FOUND")
            ? "notFound"
            : "error"
        );
        setError(localizedErrorMessage(cause, t));
      });
    return () => {
      active = false;
    };
  }, [id, t]);

  return (
    <DonorPage
      eyebrowKey="donorX.detail.eyebrow"
      titleKey="donorX.detail.title"
      descriptionKey="donorX.detail.description"
    >
      {state === "loading" ? (
        <PortalState kind="loading" title={t("donorX.detail.loadingTitle")}>
          {t("donorX.detail.loadingBody")}
        </PortalState>
      ) : state === "notFound" ? (
        <PortalState
          kind="empty"
          title={t("donorX.detail.notFoundTitle")}
          href="/donor/donations"
          linkLabel={t("donorX.detail.back")}
        >
          {t("donorX.detail.notFoundBody")}
        </PortalState>
      ) : state === "error" || !donation ? (
        <PortalState kind="error" title={t("donorX.detail.errorTitle")}>
          {error}
        </PortalState>
      ) : (
        <>
          <section className="donor-detail-head" aria-labelledby="donor-detail-reference">
            <div>
              <p className="donor-detail-head__eyebrow">{t("donorX.shared.reference")}</p>
              <h2 id="donor-detail-reference">{donation.reference}</h2>
              <p className="donor-detail-head__summary">{donationSummary(t, donation)}</p>
            </div>
            <span className={`donor-badge donor-badge--${donationStatusTone(donation.status)}`}>
              {donationStatusLabel(t, donation.status)}
            </span>
          </section>

          <PortalNotice>{donationStatusNote(t, donation.status)}</PortalNotice>

          {donation.isSuspicious ? (
            <PortalNotice tone="error">
              {t("donorX.detail.suspiciousTitle")} — {t("donorX.detail.suspiciousBody")}
            </PortalNotice>
          ) : null}
          {donation.status === "Cancelled" ? (
            <PortalNotice tone="error">{t("donorX.detail.cancelledBody")}</PortalNotice>
          ) : null}

          <PortalSection title={t("donorX.detail.summaryTitle")}>
            <dl className="donor-detail-list">
              <div>
                <dt>{t("donorX.detail.recordedLabel")}</dt>
                <dd>{formatDonationDate(donation.createdAt || donation.receivedAt, locale)}</dd>
              </div>
              <div>
                <dt>{t("donorX.detail.typeLabel")}</dt>
                <dd>
                  {donation.donationType === "Monetary"
                    ? t("donorX.donate.reviewFinancial")
                    : t("donorX.donate.reviewSupplies")}
                </dd>
              </div>
              {donation.donationType === "Monetary" ? (
                <div>
                  <dt>{t("donorX.detail.amountLabel")}</dt>
                  <dd>{formatMoney(donation.amount, donation.currency)}</dd>
                </div>
              ) : (
                <>
                  {donation.estimatedValue ? (
                    <div>
                      <dt>{t("donorX.donate.estimatedLabel", { currency: donation.currency })}</dt>
                      <dd>{formatMoney(donation.estimatedValue, donation.currency)}</dd>
                    </div>
                  ) : null}
                  {donation.category ? (
                    <div>
                      <dt>{t("donorX.donate.categoryLabel")}</dt>
                      <dd>{categoryLabel(t, donation.category)}</dd>
                    </div>
                  ) : null}
                </>
              )}
              <div>
                <dt>{t("donorX.detail.campaignLabel")}</dt>
                <dd>{donation.campaignName ?? t("donorX.detail.noCampaign")}</dd>
              </div>
              <div>
                <dt>{t("donorX.detail.locationLabel")}</dt>
                <dd>
                  {hasValidPoint(donation.latitude, donation.longitude) ? (
                    <span className="donor-detail-location">
                      {donation.locationText ?? t("donorX.shared.locationOnMap")}
                      <a
                        href={mapLink(donation.latitude as number, donation.longitude as number)}
                        target="_blank"
                        rel="noreferrer"
                      >
                        {t("donorX.shared.openMap")} <Icon name="arrow" size={13} />
                      </a>
                    </span>
                  ) : (
                    donation.locationText ?? t("donorX.shared.locationNotSpecified")
                  )}
                </dd>
              </div>
              {donation.deliveryMethod ? (
                <div>
                  <dt>{t("donorX.detail.deliveryLabel")}</dt>
                  <dd>{deliveryLabel(t, donation.deliveryMethod)}</dd>
                </div>
              ) : null}
              {donation.notes ? (
                <div>
                  <dt>{t("donorX.detail.notesLabel")}</dt>
                  <dd>{donation.notes}</dd>
                </div>
              ) : null}
            </dl>
          </PortalSection>

          {(donation.items?.length ?? 0) > 0 ? (
            <PortalSection title={t("donorX.detail.itemsTitle")}>
              <ul className="donor-detail-items">
                {(donation.items ?? []).map((item) => (
                  <li key={item.id}>
                    <span className="donor-detail-items__icon" aria-hidden="true">
                      <Icon name={categoryIcon(item.category)} size={18} />
                    </span>
                    <div>
                      <b>{item.name}</b>
                      <p>
                        {item.quantity} {unitLabel(t, item.unit)} · {categoryLabel(t, item.category)}
                      </p>
                      {item.condition ? (
                        <small>{t("donorX.donate.conditionLabel")}: {item.condition}</small>
                      ) : null}
                      {item.packaging ? (
                        <small>{t("donorX.donate.packagingLabel")}: {item.packaging}</small>
                      ) : null}
                      {item.expiryDate ? (
                        <small>{t("donorX.donate.expiryLabel")}: {formatDonationCalendarDate(item.expiryDate, locale)}</small>
                      ) : null}
                      {item.notes ? (
                        <small>{t("donorX.donate.itemNotesLabel")}: {item.notes}</small>
                      ) : null}
                    </div>
                  </li>
                ))}
              </ul>
            </PortalSection>
          ) : null}

          <PortalSection title={t("donorX.detail.timelineTitle")}>
            <DonorTimeline steps={buildDonationTimeline(donation)} noteKey="donorX.detail.distributedPending" />
          </PortalSection>

          {donation.allocations.length > 0 ? (
            <PortalSection title={t("donorX.detail.stageAllocatedTitle")}>
              <ul className="donor-allocation-list">
                {donation.allocations.map((allocation) => (
                  <li key={allocation.id}>
                    <span className="donor-allocation-list__icon" aria-hidden="true">
                      <Icon name="handHeart" size={17} />
                    </span>
                    <div>
                      <b>{allocation.campaignName}</b>
                      <p>{formatMoney(allocation.amount, donation.currency)}</p>
                      <small>{formatDonationDate(allocation.createdAt, locale)}</small>
                    </div>
                  </li>
                ))}
              </ul>
            </PortalSection>
          ) : null}

          <div className="donor-detail-actions">
            <Link className="btn btn-outline" href="/donor/donations">
              <Icon name="arrowLeft" size={16} /> {t("donorX.detail.back")}
            </Link>
          </div>
        </>
      )}
    </DonorPage>
  );
}
