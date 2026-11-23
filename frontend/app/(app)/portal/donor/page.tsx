"use client";

import Link from "next/link";
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
import { errorMessage } from "@/lib/api";
import { toDateInputValue } from "@/lib/dates";
import { formatMoney } from "@/lib/donations";
import { formatShortDate } from "@/lib/format";
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

function numberOrNull(value: string): number | null {
  if (!value.trim()) {
    return null;
  }
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 0) {
    throw new Error("Enter a valid non-negative number.");
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
        setLoadError(errorMessage(cause));
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
        if (active) setReportError(errorMessage(cause));
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
      setReportError("Select both a start and end date.");
      return;
    }
    if (Date.parse(draftRange.to) < Date.parse(draftRange.from)) {
      setReportError("The end date must be on or after the start date.");
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
        throw new Error("Currency must be a three-letter code, such as USD.");
      }

      const common = {
        donationType,
        currency: code,
        notes: notes.trim() || undefined
      };

      if (donationType === "Monetary") {
        const value = numberOrNull(amount);
        if (value === null || value <= 0) {
          throw new Error("Enter a monetary amount greater than zero.");
        }
        await recordDonation({ ...common, amount: value });
      } else {
        if (!itemDescription.trim()) {
          throw new Error("Describe the goods being contributed.");
        }
        const value = numberOrNull(estimatedValue);
        const count = numberOrNull(quantity);
        if (count !== null && !Number.isInteger(count)) {
          throw new Error("Quantity must be a whole number.");
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
      setNotice("Your contribution was recorded. Its reference appears in your history.");
    } catch (cause) {
      setError(errorMessage(cause));
    } finally {
      setBusy(false);
    }
  }

  const totalCurrency = profile
    ? new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(
        profile.totalContributed
      )
    : "—";

  return (
    <AuthGuard allowedRoles={[RoleNames.Donor]}>
      <PortalHeader
        eyebrow="Donor portal"
        title="Your contributions and their impact"
        description="Review active appeals, keep a reliable giving history and see aggregated allocation data."
        action={
          <Link className="btn btn-outline" href="/notifications">
            <Icon name="bell" /> Notifications
          </Link>
        }
      />

      <PortalStats
        items={[
          {
            label: "Recorded contributions",
            value: loading ? "—" : (profile?.donationCount ?? 0),
            hint: "Not cancelled",
            icon: "heart",
            tone: "green"
          },
          {
            label: "Recorded value",
            value: loading ? "—" : totalCurrency,
            hint: "Across recorded gifts",
            icon: "chart",
            tone: "blue"
          },
          {
            label: "Active appeals",
            value: loading ? "—" : campaigns.length,
            hint: "Open for support",
            icon: "box",
            tone: "blue"
          },
          {
            label: "Unread messages",
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
        title="Donor profile"
        description="This record links your account to the contributions you submit."
      >
        {loading ? (
          <PortalState kind="loading" title="Loading donor profile">
            Reading the profile linked to your account.
          </PortalState>
        ) : !profile ? (
          <PortalState kind="error" title="Donor profile unavailable">
            A donor profile is not linked to this account. Contact support to have it connected.
          </PortalState>
        ) : (
          <div className="portal-donor-profile">
            <div className="portal-identity">
              <span className="portal-avatar">
                <Icon name="heart" size={22} />
              </span>
              <div>
                <small>REGISTERED DONOR</small>
                <h3>{profile.name}</h3>
                <p>{profile.contactEmail || "No contact email provided"}</p>
              </div>
            </div>
            <dl>
              <div>
                <dt>Donor type</dt>
                <dd>{profile.donorType}</dd>
              </div>
              <div>
                <dt>Profile status</dt>
                <dd>
                  <span className={`badge ${profile.isActive ? "green" : "amber"}`}>
                    <i /> {profile.isActive ? "Active" : "Inactive"}
                  </span>
                </dd>
              </div>
              <div>
                <dt>Visibility</dt>
                <dd>{profile.isAnonymous ? "Anonymous" : "Named"}</dd>
              </div>
              <div>
                <dt>Profile reference</dt>
                <dd>{profile.id.slice(0, 8).toUpperCase()}</dd>
              </div>
            </dl>
          </div>
        )}
      </PortalSection>

      <div className="portal-split-grid">
        <PortalSection
          title="Record a contribution"
          description="Add a gift or material donation that has already been received."
        >
          <form className="portal-form" onSubmit={(event) => void onDonationSubmit(event)}>
            <label>
              <span>Contribution type</span>
              <select
                value={donationType}
                onChange={(event) => {
                  setDonationType(event.target.value as DonationType);
                  setError(null);
                }}
              >
                <option value="Monetary">Monetary</option>
                <option value="Material">Goods or materials</option>
              </select>
            </label>

            {donationType === "Monetary" ? (
              <div className="portal-form-row">
                <label>
                  <span>Amount</span>
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
                  <span>Currency</span>
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
                  <span>Item description</span>
                  <input
                    value={itemDescription}
                    onChange={(event) => setItemDescription(event.target.value)}
                    placeholder="Blankets, food parcels, medical supplies"
                    maxLength={500}
                    required
                  />
                </label>
                <div className="portal-form-row">
                  <label>
                    <span>Quantity</span>
                    <input
                      type="number"
                      min="1"
                      step="1"
                      value={quantity}
                      onChange={(event) => setQuantity(event.target.value)}
                      placeholder="Optional"
                    />
                  </label>
                  <label>
                    <span>Estimated value ({currency || "currency"})</span>
                    <input
                      type="number"
                      min="0"
                      step="0.01"
                      value={estimatedValue}
                      onChange={(event) => setEstimatedValue(event.target.value)}
                      placeholder="Optional"
                    />
                  </label>
                </div>
                <label>
                  <span>Currency for estimated value</span>
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
              <span>Notes</span>
              <textarea
                value={notes}
                onChange={(event) => setNotes(event.target.value)}
                maxLength={1000}
                placeholder="Delivery or receipt details for your records"
              />
            </label>

            <p className="portal-form-help">
              <Icon name="info" size={15} /> Recording only. Confirm the funds or goods were
              received before submitting; no payment is taken here.
            </p>

            <button className="btn btn-primary" type="submit" disabled={busy || !profile?.isActive}>
              {busy ? "Recording…" : "Record contribution"}
              {busy ? null : <Icon name="arrow" size={16} />}
            </button>
          </form>
        </PortalSection>

        <PortalSection
          title="Active appeals"
          description="Campaigns currently open for support, with progress published by the response team."
        >
          {loading ? (
            <PortalState kind="loading" title="Loading appeals">
              Checking active campaigns.
            </PortalState>
          ) : campaigns.length === 0 ? (
            <PortalState kind="empty" title="No active appeals">
              New appeals will appear here when a response team publishes them.
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
                      <small>raised</small>
                    </span>
                    <span>
                      <b>{formatMoney(campaign.goalAmount, campaign.currency)}</b>
                      <small>goal</small>
                    </span>
                    <span>
                      <b>{campaign.donationCount}</b>
                      <small>contributions</small>
                    </span>
                  </div>
                  <div
                    className="portal-progress"
                    role="progressbar"
                    aria-label={`${campaign.name} progress`}
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
                    {Math.round(campaign.progressPercent)}% of goal
                    {campaign.endsAt ? ` · Ends ${formatShortDate(campaign.endsAt)}` : ""}
                  </small>
                </article>
              ))}
            </div>
          )}
        </PortalSection>
      </div>

      <PortalSection
        title="Contribution history"
        description="Only contributions linked to your donor profile are shown."
      >
        {loading ? (
          <PortalState kind="loading" title="Loading history">
            Reading your recorded contributions.
          </PortalState>
        ) : donations.length === 0 ? (
          <PortalState kind="empty" title="No contributions recorded yet">
            Once a contribution is recorded, its reference and allocation status appear here.
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
                      : donation.itemDescription ?? "Material contribution"}
                  </b>
                  <p>
                    {donation.reference} · {donation.donationType} · {formatShortDate(donation.receivedAt)}
                  </p>
                  {donation.allocations.length > 0 ? (
                    <small>
                      Allocated to:{" "}
                      {donation.allocations
                        .map((allocation) => allocation.campaignName)
                        .join(", ")}
                    </small>
                  ) : (
                    <small>Not yet allocated to an appeal</small>
                  )}
                </div>
                <div className="portal-row-actions">
                  <span className={`badge ${donation.isSuspicious ? "amber" : "green"}`}>
                    <i /> {donation.isSuspicious ? "Under review" : donation.status}
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
        title="Transparency and allocation"
        description="Aggregated campaign and expenditure data. Individual donor identities are never published."
      >
        <form className="portal-range-form" onSubmit={onRangeSubmit}>
          <label>
            <span>From</span>
            <input
              type="date"
              value={draftRange.from}
              onChange={(event) => setDraftRange({ ...draftRange, from: event.target.value })}
              required
            />
          </label>
          <label>
            <span>To</span>
            <input
              type="date"
              value={draftRange.to}
              onChange={(event) => setDraftRange({ ...draftRange, to: event.target.value })}
              required
            />
          </label>
          <button className="btn btn-outline" type="submit" disabled={reportLoading}>
            {reportLoading ? "Updating…" : "Update report"}
          </button>
        </form>

        {reportError ? <PortalNotice tone="error">{reportError}</PortalNotice> : null}
        {reportLoading ? (
          <PortalState kind="loading" title="Preparing transparency report">
            Summarising allocation data for the selected period.
          </PortalState>
        ) : !report ? (
          <PortalState kind="empty" title="No report data">
            Select a date range to view aggregated figures.
          </PortalState>
        ) : (
          <>
            <div className="portal-transparency-totals">
              <article>
                <Icon name="heart" />
                <span>
                  <b>{report.totalDonationCount}</b>
                  <small>Contributions</small>
                </span>
              </article>
              <article>
                <Icon name="users" />
                <span>
                  <b>{report.totalDonorCount}</b>
                  <small>Supporting donors</small>
                </span>
              </article>
              <article>
                <Icon name="shield" />
                <span>
                  <b>{report.suspiciousDonationCount + report.suspiciousAllocationCount}</b>
                  <small>Records awaiting review</small>
                </span>
              </article>
            </div>

            {report.currencyTotals.length === 0 ? (
              <PortalState kind="empty" title="No allocations in this period">
                Contributions outside this date range are not included in the figures above.
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
                        Received {formatMoney(total.monetaryTotal, total.currency)} · Materials{" "}
                        {formatMoney(total.materialEstimatedTotal, total.currency)}
                      </p>
                      <small>
                        Allocated {formatMoney(total.allocatedTotal, total.currency)} · Spent{" "}
                        {formatMoney(total.spentTotal, total.currency)}
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
                        Raised {formatMoney(campaign.raisedAmount, campaign.currency)} of{" "}
                        {formatMoney(campaign.goalAmount, campaign.currency)}
                      </p>
                      <small>
                        Spent {formatMoney(campaign.spentAmount, campaign.currency)} ·{" "}
                        {Math.round(campaign.progressPercent)}% of goal
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
        title="Messages"
        description="Updates about campaigns and recorded contributions."
        action={
          <Link className="portal-text-link" href="/notifications">
            Open inbox →
          </Link>
        }
      >
        {!inbox || inbox.items.length === 0 ? (
          <PortalState kind="empty" title="No messages yet">
            Donation and campaign updates will appear here.
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
                  <p>{notification.body ?? "Open the inbox for details."}</p>
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
