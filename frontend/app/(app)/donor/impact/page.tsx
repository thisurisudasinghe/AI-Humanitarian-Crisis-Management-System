"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { DonorPage } from "@/components/safeway/DonorPage";
import { Icon } from "@/components/safeway/Icon";
import { PortalNotice, PortalSection, PortalState } from "@/components/safeway/PortalPrimitives";
import { currentMonthRange } from "@/lib/dates";
import { groupDonationsByCurrency } from "@/lib/donor";
import { formatMoney, type DonationReportDto, type DonationDto } from "@/lib/donations";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useT } from "@/lib/i18n";
import { getDonorDonations, getDonorTransparency } from "@/lib/portal";

type LoadState = "loading" | "ready" | "error";

/** The API caps page size, so personal totals are assembled from every page. */
async function loadAllDonations(): Promise<DonationDto[]> {
  const pageSize = 200;
  const first = await getDonorDonations(1, pageSize);
  const donations = [...first.items];
  const pages = first.totalPages;
  for (let page = 2; page <= pages; page += 1) {
    const next = await getDonorDonations(page, pageSize);
    donations.push(...next.items);
  }
  return donations;
}

export default function DonorImpactPage() {
  const t = useT();
  const initialRange = useMemo(() => currentMonthRange(), []);
  const [donations, setDonations] = useState<DonationDto[]>([]);
  const [personalState, setPersonalState] = useState<LoadState>("loading");
  const [personalError, setPersonalError] = useState<string | null>(null);
  const [report, setReport] = useState<DonationReportDto | null>(null);
  const [reportState, setReportState] = useState<LoadState>("loading");
  const [reportError, setReportError] = useState<string | null>(null);
  const [from, setFrom] = useState(initialRange.from);
  const [to, setTo] = useState(initialRange.to);
  const [draftFrom, setDraftFrom] = useState(initialRange.from);
  const [draftTo, setDraftTo] = useState(initialRange.to);
  const [rangeError, setRangeError] = useState<string | null>(null);
  const [reportVersion, setReportVersion] = useState(0);

  useEffect(() => {
    let active = true;
    setPersonalState("loading");
    loadAllDonations()
      .then((loaded) => {
        if (!active) return;
        setDonations(loaded);
        setPersonalState("ready");
        setPersonalError(null);
      })
      .catch((cause) => {
        if (!active) return;
        setPersonalState("error");
        setPersonalError(localizedErrorMessage(cause, t));
      });
    return () => {
      active = false;
    };
  }, [t]);

  useEffect(() => {
    let active = true;
    setReportState("loading");
    getDonorTransparency(from, to)
      .then((result) => {
        if (!active) return;
        setReport(result);
        setReportState("ready");
        setReportError(null);
      })
      .catch((cause) => {
        if (!active) return;
        setReportState("error");
        setReportError(localizedErrorMessage(cause, t));
      });
    return () => {
      active = false;
    };
  }, [from, to, reportVersion, t]);

  const buckets = useMemo(() => groupDonationsByCurrency(donations), [donations]);
  const receivedCount = donations.filter((donation) => donation.status === "Received").length;
  const allocatedCount = donations.filter((donation) => donation.status === "Allocated").length;
  const awaitingReview = donations.filter((donation) => donation.isSuspicious).length;
  const campaignsSupported = useMemo(() => {
    const ids = new Set<string>();
    for (const donation of donations) {
      if (donation.campaignId) ids.add(donation.campaignId);
      for (const allocation of donation.allocations) ids.add(allocation.campaignId);
    }
    return ids.size;
  }, [donations]);

  function updateReport() {
    if (!draftFrom || !draftTo) {
      setRangeError(t("donorX.validation.rangeDates"));
      return;
    }
    if (draftTo < draftFrom) {
      setRangeError(t("donorX.validation.endAfterStart"));
      return;
    }
    setRangeError(null);
    setFrom(draftFrom);
    setTo(draftTo);
    setReportVersion((value) => value + 1);
  }

  return (
    <DonorPage
      eyebrowKey="donorX.impact.eyebrow"
      titleKey="donorX.impact.title"
      descriptionKey="donorX.impact.description"
      action={
        <Link className="btn btn-outline" href="/donor/donate">
          <Icon name="plus" size={16} /> {t("donorX.landing.cardDonateCta")}
        </Link>
      }
    >
      {personalState === "loading" ? (
        <PortalState kind="loading" title={t("donorX.impact.loadingTitle")}>
          {t("donorX.impact.loadingBody")}
        </PortalState>
      ) : personalState === "error" ? (
        <PortalState kind="error" title={t("donorX.impact.errorTitle")}>
          {personalError}
        </PortalState>
      ) : donations.length === 0 ? (
        <PortalState kind="empty" title={t("donorX.impact.emptyTitle")} href="/donor/donate" linkLabel={t("donorX.landing.cardDonateCta")}>
          {t("donorX.impact.emptyBody")}
        </PortalState>
      ) : (
        <PortalSection
          title={t("donorX.impact.personalTitle")}
          description={t("donorX.impact.personalDescription")}
        >
          <div className="donor-impact-stats">
            <div>
              <Icon name="clipboard" size={18} />
              <span>{t("donorX.impact.recordedCount")}</span>
              <b>{donations.length}</b>
            </div>
            <div>
              <Icon name="checkCircle" size={18} />
              <span>{t("donorX.impact.receivedCount")}</span>
              <b>{receivedCount}</b>
            </div>
            <div>
              <Icon name="handHeart" size={18} />
              <span>{t("donorX.impact.allocatedCount")}</span>
              <b>{allocatedCount}</b>
            </div>
            <div>
              <Icon name="clock" size={18} />
              <span>{t("donorX.impact.awaitingReview")}</span>
              <b>{awaitingReview}</b>
            </div>
            <div>
              <Icon name="megaphone" size={18} />
              <span>{t("donorX.impact.campaignsSupported")}</span>
              <b>{campaignsSupported}</b>
            </div>
          </div>

          <h3>{t("donorX.impact.byCurrency")}</h3>
          <div className="donor-impact-currency-grid">
            {buckets.map((bucket) => (
              <article className="donor-impact-currency" key={bucket.currency}>
                <h4>{bucket.currency}</h4>
                <dl>
                  <div>
                    <dt>{t("donorX.impact.monetary")}</dt>
                    <dd>{formatMoney(bucket.monetary, bucket.currency)}</dd>
                  </div>
                  <div>
                    <dt>{t("donorX.impact.materials")}</dt>
                    <dd>{formatMoney(bucket.materialEstimated, bucket.currency)}</dd>
                  </div>
                  <div>
                    <dt>{t("donorX.impact.allocated")}</dt>
                    <dd>{formatMoney(bucket.allocated, bucket.currency)}</dd>
                  </div>
                  <div>
                    <dt>{t("donorX.impact.recordedCount")}</dt>
                    <dd>{bucket.count}</dd>
                  </div>
                </dl>
              </article>
            ))}
          </div>
        </PortalSection>
      )}

      <PortalSection
        title={t("donorX.impact.publicTitle")}
        description={t("donorX.impact.publicDescription")}
      >
        <div className="donor-report-range">
          <div className="donor-field">
            <label htmlFor="donor-impact-from">{t("donorX.impact.from")}</label>
            <input
              id="donor-impact-from"
              type="date"
              value={draftFrom}
              onChange={(event) => setDraftFrom(event.target.value)}
            />
          </div>
          <div className="donor-field">
            <label htmlFor="donor-impact-to">{t("donorX.impact.to")}</label>
            <input
              id="donor-impact-to"
              type="date"
              value={draftTo}
              onChange={(event) => setDraftTo(event.target.value)}
            />
          </div>
          <button className="btn btn-primary" type="button" onClick={updateReport}>
            <Icon name="clock" size={16} /> {t("donorX.impact.update")}
          </button>
        </div>
        {rangeError ? (
          <PortalNotice tone="error">{rangeError}</PortalNotice>
        ) : null}

        {reportState === "loading" ? (
          <PortalState kind="loading" title={t("donorX.impact.loadingTitle")}>
            {t("donorX.impact.loadingBody")}
          </PortalState>
        ) : reportState === "error" ? (
          <PortalState kind="error" title={t("donorX.impact.errorTitle")}>
            {reportError}
          </PortalState>
        ) : !report || report.currencyTotals.length === 0 ? (
          <PortalState kind="empty" title={t("donorX.impact.noTotals")}>
            {t("donorX.impact.publicDescription")}
          </PortalState>
        ) : (
          <>
            <h3>{t("donorX.impact.totalsByCurrency")}</h3>
            <div className="donor-report-table-wrap">
              <table className="donor-report-table">
                <thead>
                  <tr>
                    <th scope="col">{t("donorX.impact.totalsByCurrency")}</th>
                    <th scope="col">{t("donorX.impact.monetary")}</th>
                    <th scope="col">{t("donorX.impact.materials")}</th>
                    <th scope="col">{t("donorX.impact.allocated")}</th>
                    <th scope="col">{t("donorX.impact.spent")}</th>
                    <th scope="col">{t("donorX.impact.recordedCount")}</th>
                  </tr>
                </thead>
                <tbody>
                  {report.currencyTotals.map((total) => (
                    <tr key={total.currency}>
                      <th scope="row">{total.currency}</th>
                      <td>{formatMoney(total.monetaryTotal, total.currency)}</td>
                      <td>{formatMoney(total.materialEstimatedTotal, total.currency)}</td>
                      <td>{formatMoney(total.allocatedTotal, total.currency)}</td>
                      <td>{formatMoney(total.spentTotal, total.currency)}</td>
                      <td>{total.donationCount}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <h3>{t("donorX.impact.campaigns")}</h3>
            {report.campaigns.length === 0 ? (
              <PortalState kind="empty" title={t("donorX.impact.noCampaigns")}>
                {t("donorX.impact.publicDescription")}
              </PortalState>
            ) : (
              <div className="donor-impact-campaigns">
                {report.campaigns.map((campaign) => {
                  const progress = Math.max(0, Math.min(100, Math.round(campaign.progressPercent)));
                  return (
                    <article key={campaign.campaignId}>
                      <div className="donor-campaign-card__progress-top">
                        <b>{campaign.name}</b>
                        <span>{t("donorX.needs.percent", { percent: progress })}</span>
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
                      <p>
                        {formatMoney(campaign.raisedAmount, campaign.currency)} ·{" "}
                        {t("donorX.needs.remaining")}: {formatMoney(campaign.remainingAmount, campaign.currency)}
                      </p>
                    </article>
                  );
                })}
              </div>
            )}
          </>
        )}

        <PortalNotice tone="success">{t("donorX.impact.noImpactClaim")}</PortalNotice>
      </PortalSection>
    </DonorPage>
  );
}
