"use client";

import Link from "next/link";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useEffect, useState, type FormEvent } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import {
  PortalHeader,
  PortalNotice,
  PortalSection,
  PortalState,
  PortalStats
} from "@/components/safeway/PortalPrimitives";

import { toDateInputValue } from "@/lib/dates";
import { formatMoney } from "@/lib/donations";
import { formatShortDate } from "@/lib/format";
import { formatNumber, useI18n } from "@/lib/i18n";
import { getInbox, type NotificationInboxDto } from "@/lib/notifications";
import {
  getDonorCampaigns,
  getDonorDonations,
  getDonorProfile,
  getDonorTransparency,
  recordDonation,
  type CampaignDto,
  type DonationDto,
  type DonationReportDto,
  type DonorDto
} from "@/lib/portal";
import { RoleNames } from "@/lib/roles";

type DonationType = "Monetary" | "Material";

function numberOrNull(value: string, message: string): number | null {
  if (!value.trim()) {
    return null;
  }
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 0) {
    throw new Error(message);
  }
  return parsed;
}

function donationValue(donation: DonationDto): number | null {
  const value =
    donation.donationType === "Monetary"
      ? donation.amount
      : donation.estimatedValue;
  return value ?? null;
}

export default function DonorPortalPage() {
  const { t, locale } = useI18n();
  const [profile, setProfile] = useState<DonorDto | null>(null);
  const [campaigns, setCampaigns] = useState<CampaignDto[]>([]);
  const [donations, setDonations] = useState<DonationDto[]>([]);
  const [inbox, setInbox] = useState<NotificationInboxDto | null>(null);
  const [report, setReport] = useState<DonationReportDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [reportLoading, setReportLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [reportError, setReportError] = useState<string | null>(null);

  const [range, setRange] = useState({ from: "", to: "" });
  const [draftRange, setDraftRange] = useState({ from: "", to: "" });

  const [donationType, setDonationType] = useState<DonationType>("Monetary");
  const [currency, setCurrency] = useState("USD");
  const [amount, setAmount] = useState("");
  const [itemDescription, setItemDescription] = useState("");
  const [quantity, setQuantity] = useState("");
  const [estimatedValue, setEstimatedValue] = useState("");
  const [notes, setNotes] = useState("");
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const now = new Date();
    const from = new Date(now);
    from.setDate(from.getDate() - 90);
    const initial = {
      from: toDateInputValue(from),
      to: toDateInputValue(now)
    };
    setRange(initial);
    setDraftRange(initial);
  }, []);

  async function load() {
    const profileRequest = getDonorProfile()
      .then((data) => {
        setProfile(data);
        setLoadError(null);
      })
      .catch((cause) => {
        setLoadError(localizedErrorMessage(cause, t));
      });

    const campaignRequest = getDonorCampaigns()
      .then((result) => setCampaigns(result.items))
      .catch(() => setCampaigns([]));

    const donationRequest = getDonorDonations(1, 10)
      .then((result) => setDonations(result.items))
      .catch(() => setDonations([]));

    const inboxRequest = getInbox({ limit: 5 })
      .then((result) => setInbox(result))
      .catch(() => setInbox(null));

    await Promise.all([profileRequest, campaignRequest, donationRequest, inboxRequest]);
  }

  useEffect(() => {
    let active = true;
    setLoading(true);
    void load().finally(() => {
      if (active) setLoading(false);
    });
    return () => {
      active = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!range.from || !range.to) {
      return;
    }

    let active = true;
    setReportLoading(true);
    void getDonorTransparency(range.from, range.to)
      .then((data) => {
        if (active) {
          setReport(data);
          setReportError(null);
        }
      })
      .catch((cause) => {
        if (active) setReportError(localizedErrorMessage(cause, t));
      })
      .finally(() => {
        if (active) setReportLoading(false);
      });

    return () => {
      active = false;
    };
  }, [range.from, range.to]);

  function onRangeSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!draftRange.from || !draftRange.to) {
      setReportError(t("portal.donor.validation.rangeDates"));
      return;
    }
    if (Date.parse(draftRange.to) < Date.parse(draftRange.from)) {
      setReportError(t("portal.donor.validation.endAfterStart"));
      return;
    }
    setReportError(null);
    setRange({ ...draftRange });
  }

  async function onDonationSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setNotice(null);
    setError(null);

    try {
      const code = currency.trim().toUpperCase();
      if (!/^[A-Z]{3}$/.test(code)) {
        throw new Error(t("portal.donor.validation.currencyCode"));
      }

      const common = {
        donationType,
        currency: code,
        notes: notes.trim() || undefined
      };

      if (donationType === "Monetary") {
        const value = numberOrNull(amount, t("portal.donor.validation.nonNegative"));
        if (value === null || value <= 0) {
          throw new Error(t("portal.donor.validation.positiveAmount"));
        }
        await recordDonation({ ...common, amount: value });
      } else {
        if (!itemDescription.trim()) {
          throw new Error(t("portal.donor.validation.describeGoods"));
        }
        const value = numberOrNull(estimatedValue, t("portal.donor.validation.nonNegative"));
        const count = numberOrNull(quantity, t("portal.donor.validation.nonNegative"));
        if (count !== null && !Number.isInteger(count)) {
          throw new Error(t("portal.donor.validation.wholeNumber"));
        }
        await recordDonation({
          ...common,
          itemDescription: itemDescription.trim(),
          quantity: count ?? undefined,
          estimatedValue: value ?? undefined
        });
      }

      setAmount("");
      setItemDescription("");
      setQuantity("");
      setEstimatedValue("");
      setNotes("");
      await load();
      setNotice(t("portal.donor.validation.saved"));
    } catch (cause) {
      setError(localizedErrorMessage(cause, t));
    } finally {
      setBusy(false);
    }
  }

  const totalCurrency = profile
    ? formatNumber(profile.totalContributed, locale, { maximumFractionDigits: 2 })
    : "—";

  return (
    <AuthGuard allowedRoles={[RoleNames.Donor]} activationPurpose="donor">
      <PortalHeader
        eyebrow={t("portal.donor.eyebrow")}
        title={t("portal.donor.title")}
        description={t("portal.donor.description")}
        action={
          <Link className="btn btn-outline" href="/notifications">
            <Icon name="bell" /> {t("portal.donor.notifications")}
          </Link>
        }
      />

      <PortalStats
        items={[
          {
            label: t("portal.donor.stats.recordedContributions"),
            value: loading ? "—" : (profile?.donationCount ?? 0),
            hint: t("portal.donor.stats.notCancelled"),
            icon: "heart",
            tone: "green"
          },
          {
            label: t("portal.donor.stats.recordedValue"),
            value: loading ? "—" : totalCurrency,
            hint: t("portal.donor.stats.acrossGifts"),
            icon: "chart",
            tone: "blue"
          },
          {
            label: t("portal.donor.stats.activeAppeals"),
            value: loading ? "—" : campaigns.length,
            hint: t("portal.donor.stats.openForSupport"),
            icon: "box",
            tone: "blue"
          },
          {
            label: t("portal.donor.stats.unreadMessages"),
            value: loading ? "—" : (inbox?.unreadCount ?? 0),
            icon: "bell",
            tone: "green"
          }
        ]}
      />

      {loadError ? <PortalNotice tone="error">{loadError}</PortalNotice> : null}
      {notice ? <PortalNotice>{notice}</PortalNotice> : null}
      {error ? <PortalNotice tone="error">{error}</PortalNotice> : null}

      <PortalSection
        title={t("portal.donor.profile.title")}
        description={t("portal.donor.profile.description")}
      >
        {loading ? (
          <PortalState kind="loading" title={t("portal.donor.profile.loadingTitle")}>
            {t("portal.donor.profile.loadingBody")}
          </PortalState>
        ) : !profile ? (
          <PortalState kind="error" title={t("portal.donor.profile.errorTitle")}>
            {t("portal.donor.profile.errorBody")}
          </PortalState>
        ) : (
          <div className="portal-donor-profile">
            <div className="portal-identity">
              <span className="portal-avatar">
                <Icon name="heart" size={22} />
              </span>
              <div>
                <small>{t("portal.donor.profile.registered")}</small>
                <h3>{profile.name}</h3>
                <p>{profile.contactEmail || t("portal.donor.profile.noContactEmail")}</p>
              </div>
            </div>
            <dl>
              <div>
                <dt>{t("portal.donor.profile.donorType")}</dt>
                <dd>{profile.donorType}</dd>
              </div>
              <div>
                <dt>{t("portal.donor.profile.status")}</dt>
                <dd>
                  <span className={`badge ${profile.isActive ? "green" : "amber"}`}>
                    <i /> {profile.isActive ? t("portal.donor.profile.active") : t("portal.donor.profile.inactive")}
                  </span>
                </dd>
              </div>
              <div>
                <dt>{t("portal.donor.profile.visibility")}</dt>
                <dd>{profile.isAnonymous ? t("portal.donor.profile.anonymous") : t("portal.donor.profile.named")}</dd>
              </div>
              <div>
                <dt>{t("portal.donor.profile.reference")}</dt>
                <dd>{profile.id.slice(0, 8).toUpperCase()}</dd>
              </div>
            </dl>
          </div>
        )}
      </PortalSection>

      <div className="portal-split-grid">
        <PortalSection
          title={t("portal.donor.record.title")}
          description={t("portal.donor.record.description")}
        >
          <form className="portal-form" onSubmit={(event) => void onDonationSubmit(event)}>
            <label>
              <span>{t("portal.donor.record.type")}</span>
              <select
                value={donationType}
                onChange={(event) => {
                  setDonationType(event.target.value as DonationType);
                  setError(null);
                }}
              >
                <option value="Monetary">{t("portal.donor.record.monetary")}</option>
                <option value="Material">{t("portal.donor.record.goods")}</option>
              </select>
            </label>

            {donationType === "Monetary" ? (
              <div className="portal-form-row">
                <label>
                  <span>{t("portal.donor.record.amount")}</span>
                  <input
                    type="number"
                    min="0.01"
                    step="0.01"
                    value={amount}
                    onChange={(event) => setAmount(event.target.value)}
                    placeholder="0.00"
                    required
                  />
                </label>
                <label>
                  <span>{t("portal.donor.record.currency")}</span>
                  <input
                    value={currency}
                    onChange={(event) => setCurrency(event.target.value)}
                    maxLength={3}
                    placeholder="USD"
                    required
                  />
                </label>
              </div>
            ) : (
              <>
                <label>
                  <span>{t("portal.donor.record.itemDescription")}</span>
                  <input
                    value={itemDescription}
                    onChange={(event) => setItemDescription(event.target.value)}
                    placeholder={t("portal.donor.record.itemPlaceholder")}
                    maxLength={500}
                    required
                  />
                </label>
                <div className="portal-form-row">
                  <label>
                    <span>{t("portal.donor.record.quantity")}</span>
                    <input
                      type="number"
                      min="1"
                      step="1"
                      value={quantity}
                      onChange={(event) => setQuantity(event.target.value)}
                      placeholder={t("portal.donor.record.optional")}
                    />
                  </label>
                  <label>
                    <span>{t("portal.donor.record.estimatedValue", { currency: currency || t("portal.donor.record.currencyFallback") })}</span>
                    <input
                      type="number"
                      min="0"
                      step="0.01"
                      value={estimatedValue}
                      onChange={(event) => setEstimatedValue(event.target.value)}
                      placeholder={t("portal.donor.record.optional")}
                    />
                  </label>
                </div>
                <label>
                  <span>{t("portal.donor.record.currencyForEstimated")}</span>
                  <input
                    value={currency}
                    onChange={(event) => setCurrency(event.target.value)}
                    maxLength={3}
                    placeholder="USD"
                    required
                  />
                </label>
              </>
            )}

            <label>
              <span>{t("portal.donor.record.notes")}</span>
              <textarea
                value={notes}
                onChange={(event) => setNotes(event.target.value)}
                maxLength={1000}
                placeholder={t("portal.donor.record.notesPlaceholder")}
              />
            </label>

            <p className="portal-form-help">
              <Icon name="info" size={15} /> {t("portal.donor.record.help")}
            </p>

            <button className="btn btn-primary" type="submit" disabled={busy || !profile?.isActive}>
              {busy ? t("portal.donor.record.recording") : t("portal.donor.record.submit")}
              {busy ? null : <Icon name="arrow" size={16} />}
            </button>
          </form>
        </PortalSection>

        <PortalSection
          title={t("portal.donor.appeals.title")}
          description={t("portal.donor.appeals.description")}
        >
          {loading ? (
            <PortalState kind="loading" title={t("portal.donor.appeals.loadingTitle")}>
              {t("portal.donor.appeals.loadingBody")}
            </PortalState>
          ) : campaigns.length === 0 ? (
            <PortalState kind="empty" title={t("portal.donor.appeals.emptyTitle")}>
              {t("portal.donor.appeals.emptyBody")}
            </PortalState>
          ) : (
            <div className="portal-campaign-grid">
              {campaigns.map((campaign) => (
                <article className="portal-campaign" key={campaign.id}>
                  <div className="portal-campaign-head">
                    <b>{campaign.name}</b>
                    <span className="badge green">
                      <i /> {campaign.status}
                    </span>
                  </div>
                  {campaign.description ? <p>{campaign.description}</p> : null}
                  <div className="portal-campaign-amounts">
                    <span>
                      <b>{formatMoney(campaign.raisedAmount, campaign.currency)}</b>
                      <small>{t("portal.donor.appeals.raised")}</small>
                    </span>
                    <span>
                      <b>{formatMoney(campaign.goalAmount, campaign.currency)}</b>
                      <small>{t("portal.donor.appeals.goal")}</small>
                    </span>
                    <span>
                      <b>{campaign.donationCount}</b>
                      <small>{t("portal.donor.appeals.contributions")}</small>
                    </span>
                  </div>
                  <div
                    className="portal-progress"
                    role="progressbar"
                    aria-label={t("portal.donor.appeals.progressAria", { name: campaign.name })}
                    aria-valuenow={Math.round(campaign.progressPercent)}
                    aria-valuemin={0}
                    aria-valuemax={100}
                  >
                    <span
                      style={{
                        width: `${Math.min(100, Math.max(0, campaign.progressPercent))}%`
                      }}
                    />
                  </div>
                  <small>
                    {t("portal.donor.appeals.percentOfGoal", { percent: Math.round(campaign.progressPercent) })}
                    {campaign.endsAt ? ` · ${t("portal.donor.appeals.ends", { date: formatShortDate(campaign.endsAt) })}` : ""}
                  </small>
                </article>
              ))}
            </div>
          )}
        </PortalSection>
      </div>

      <PortalSection
        title={t("portal.donor.history.title")}
        description={t("portal.donor.history.description")}
      >
        {loading ? (
          <PortalState kind="loading" title={t("portal.donor.history.loadingTitle")}>
            {t("portal.donor.history.loadingBody")}
          </PortalState>
        ) : donations.length === 0 ? (
          <PortalState kind="empty" title={t("portal.donor.history.emptyTitle")}>
            {t("portal.donor.history.emptyBody")}
          </PortalState>
        ) : (
          <div className="portal-list">
            {donations.map((donation) => (
              <article className="portal-row portal-row--stacked" key={donation.id}>
                <i>
                  <Icon name={donation.donationType === "Monetary" ? "chart" : "box"} size={18} />
                </i>
                <div>
                  <b>
                    {donation.donationType === "Monetary"
                      ? formatMoney(donation.amount, donation.currency)
                      : donation.itemDescription ?? t("portal.donor.history.materialContribution")}
                  </b>
                  <p>
                    {donation.reference} · {donation.donationType} · {formatShortDate(donation.receivedAt)}
                  </p>
                  {donation.allocations.length > 0 ? (
                    <small>
                      {t("portal.donor.history.allocatedTo")}{" "}
                      {donation.allocations
                        .map((allocation) => allocation.campaignName)
                        .join(", ")}
                    </small>
                  ) : (
                    <small>{t("portal.donor.history.notAllocated")}</small>
                  )}
                </div>
                <div className="portal-row-actions">
                  <span className={`badge ${donation.isSuspicious ? "amber" : "green"}`}>
                    <i /> {donation.isSuspicious ? t("portal.donor.history.underReview") : donation.status}
                  </span>
                  <small className="portal-recorded-value">
                    {donationValue(donation) === null
                      ? "—"
                      : formatMoney(donationValue(donation), donation.currency)}
                  </small>
                </div>
              </article>
            ))}
          </div>
        )}
      </PortalSection>

      <PortalSection
        title={t("portal.donor.transparency.title")}
        description={t("portal.donor.transparency.description")}
      >
        <form className="portal-range-form" onSubmit={onRangeSubmit}>
          <label>
            <span>{t("portal.donor.transparency.from")}</span>
            <input
              type="date"
              value={draftRange.from}
              onChange={(event) => setDraftRange({ ...draftRange, from: event.target.value })}
              required
            />
          </label>
          <label>
            <span>{t("portal.donor.transparency.to")}</span>
            <input
              type="date"
              value={draftRange.to}
              onChange={(event) => setDraftRange({ ...draftRange, to: event.target.value })}
              required
            />
          </label>
          <button className="btn btn-outline" type="submit" disabled={reportLoading}>
            {reportLoading ? t("portal.donor.transparency.updating") : t("portal.donor.transparency.update")}
          </button>
        </form>

        {reportError ? <PortalNotice tone="error">{reportError}</PortalNotice> : null}
        {reportLoading ? (
          <PortalState kind="loading" title={t("portal.donor.transparency.loadingTitle")}>
            {t("portal.donor.transparency.loadingBody")}
          </PortalState>
        ) : !report ? (
          <PortalState kind="empty" title={t("portal.donor.transparency.emptyTitle")}>
            {t("portal.donor.transparency.emptyBody")}
          </PortalState>
        ) : (
          <>
            <div className="portal-transparency-totals">
              <article>
                <Icon name="heart" />
                <span>
                  <b>{report.totalDonationCount}</b>
                  <small>{t("portal.donor.transparency.contributions")}</small>
                </span>
              </article>
              <article>
                <Icon name="users" />
                <span>
                  <b>{report.totalDonorCount}</b>
                  <small>{t("portal.donor.transparency.donors")}</small>
                </span>
              </article>
              <article>
                <Icon name="shield" />
                <span>
                  <b>{report.suspiciousDonationCount + report.suspiciousAllocationCount}</b>
                  <small>{t("portal.donor.transparency.awaitingReview")}</small>
                </span>
              </article>
            </div>

            {report.currencyTotals.length === 0 ? (
              <PortalState kind="empty" title={t("portal.donor.transparency.noAllocationsTitle")}>
                {t("portal.donor.transparency.noAllocationsBody")}
              </PortalState>
            ) : (
              <div className="portal-list">
                {report.currencyTotals.map((total) => (
                  <article className="portal-row" key={total.currency}>
                    <i>
                      <Icon name="chart" size={18} />
                    </i>
                    <div>
                      <b>{total.currency}</b>
                      <p>
                        {t("portal.donor.transparency.receivedMaterials", {
                          received: formatMoney(total.monetaryTotal, total.currency),
                          materials: formatMoney(total.materialEstimatedTotal, total.currency)
                        })}
                      </p>
                      <small>
                        {t("portal.donor.transparency.allocatedSpent", {
                          allocated: formatMoney(total.allocatedTotal, total.currency),
                          spent: formatMoney(total.spentTotal, total.currency)
                        })}
                      </small>
                    </div>
                  </article>
                ))}
              </div>
            )}

            {report.campaigns.length > 0 ? (
              <div className="portal-list portal-list--tight">
                {report.campaigns.map((campaign) => (
                  <article className="portal-row" key={campaign.campaignId}>
                    <i className="green">
                      <Icon name="box" size={18} />
                    </i>
                    <div>
                      <b>{campaign.name}</b>
                      <p>
                        {t("portal.donor.transparency.raisedOf", {
                          raised: formatMoney(campaign.raisedAmount, campaign.currency),
                          goal: formatMoney(campaign.goalAmount, campaign.currency)
                        })}
                      </p>
                      <small>
                        {t("portal.donor.transparency.spentOfGoal", {
                          spent: formatMoney(campaign.spentAmount, campaign.currency),
                          percent: Math.round(campaign.progressPercent)
                        })}
                      </small>
                    </div>
                    <span className={`badge ${campaign.status === "Active" ? "green" : "muted"}`}>
                      <i /> {campaign.status}
                    </span>
                  </article>
                ))}
              </div>
            ) : null}
          </>
        )}
      </PortalSection>

      <PortalSection
        title={t("portal.donor.messages.title")}
        description={t("portal.donor.messages.description")}
        action={
          <Link className="portal-text-link" href="/notifications">
            {t("portal.donor.messages.openInbox")}
          </Link>
        }
      >
        {!inbox || inbox.items.length === 0 ? (
          <PortalState kind="empty" title={t("portal.donor.messages.emptyTitle")}>
            {t("portal.donor.messages.emptyBody")}
          </PortalState>
        ) : (
          <div className="portal-list">
            {inbox.items.map((notification) => (
              <article className="portal-row" key={notification.id}>
                <i className={notification.isRead ? "muted" : ""}>
                  <Icon name={notification.isRead ? "check" : "bell"} size={18} />
                </i>
                <div>
                  <b>{notification.subject ?? notification.templateKey}</b>
                  <p>{notification.body ?? t("portal.donor.messages.inboxDetails")}</p>
                  <small>{formatShortDate(notification.createdAt)}</small>
                </div>
              </article>
            ))}
          </div>
        )}
      </PortalSection>
    </AuthGuard>
  );
}
