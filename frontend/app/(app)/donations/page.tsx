"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { Button, Input, Select, Skeleton, Toast } from "@/components/ui";
import { AuthGuard } from "@/components/AuthGuard";
import { api, apiJson } from "@/lib/api";
import { formatDate } from "@/lib/admin";
import {
  CampaignStatuses,
  DonationStatuses,
  DonationTypes,
  DonorTypes,
  formatMoney,
  type CampaignDto,
  type CampaignStatus,
  type DonationDto,
  type DonationReportDto,
  type DonationType,
  type DonorDto,
  type DonorType,
  type PagedResult
} from "@/lib/donations";
import { RoleNames, hasAnyRole } from "@/lib/roles";
import { useAuth } from "@/lib/auth";
import { useT } from "@/lib/i18n";
import { currentMonthRange } from "@/lib/dates";

const donationRoles = [
  RoleNames.SystemAdmin,
  RoleNames.DonorOfficer,
  RoleNames.EmergencyCoordinator,
  RoleNames.PartnerOfficer
];

const managerRoles = [RoleNames.SystemAdmin, RoleNames.DonorOfficer];

type Notice = { message: string; tone: "error" | "success" } | null;

function parsePositive(value: string): number | null {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

export default function DonationsPage() {
  const { user } = useAuth();
  const t = useT();

  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [report, setReport] = useState<DonationReportDto | null>(null);
  const [donors, setDonors] = useState<DonorDto[]>([]);
  const [campaigns, setCampaigns] = useState<CampaignDto[]>([]);
  const [donations, setDonations] = useState<DonationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<Notice>(null);

  const [donorName, setDonorName] = useState("");
  const [donorType, setDonorType] = useState<DonorType>("Individual");
  const [donorEmail, setDonorEmail] = useState("");

  const [campaignName, setCampaignName] = useState("");
  const [campaignGoal, setCampaignGoal] = useState("");
  const [campaignCurrency, setCampaignCurrency] = useState("USD");

  const [donationDonorId, setDonationDonorId] = useState("");
  const [donationType, setDonationType] = useState<DonationType>("Monetary");
  const [donationAmount, setDonationAmount] = useState("");
  const [donationCurrency, setDonationCurrency] = useState("USD");
  const [donationItem, setDonationItem] = useState("");
  const [donationQuantity, setDonationQuantity] = useState("");
  const [donationEstimatedValue, setDonationEstimatedValue] = useState("");

  const [allocationDonationId, setAllocationDonationId] = useState("");
  const [allocationCampaignId, setAllocationCampaignId] = useState("");
  const [allocationAmount, setAllocationAmount] = useState("");

  const [expenseCampaignId, setExpenseCampaignId] = useState("");
  const [expenseDescription, setExpenseDescription] = useState("");
  const [expenseAmount, setExpenseAmount] = useState("");

  const canManage = user ? hasAnyRole(user.roles, managerRoles) : false;
  const allowed = user ? hasAnyRole(user.roles, donationRoles) : false;

  useEffect(() => {
    const range = currentMonthRange();
    setFrom(range.from);
    setTo(range.to);
  }, []);

  const loadAll = useCallback(async () => {
    if (!from || !to) {
      return;
    }

    const params = new URLSearchParams({
      from: `${from}T00:00:00Z`,
      to: `${to}T23:59:59Z`
    });

    const [donorPage, campaignPage, donationPage, reportData] = await Promise.all([
      api<PagedResult<DonorDto>>("/api/v1/donations/donors?page=1&pageSize=100"),
      api<PagedResult<CampaignDto>>("/api/v1/donations/campaigns?page=1&pageSize=100"),
      api<PagedResult<DonationDto>>("/api/v1/donations?page=1&pageSize=100"),
      api<DonationReportDto>(`/api/v1/donations/report?${params.toString()}`)
    ]);

    setDonors(donorPage.items);
    setCampaigns(campaignPage.items);
    setDonations(donationPage.items);
    setReport(reportData);
  }, [from, to]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);

    void loadAll()
      .catch((caught: unknown) => {
        if (!cancelled) {
          setNotice({ message: localizedErrorMessage(caught, t), tone: "error" });
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [loadAll]);

  const activeCampaigns = useMemo(
    () => campaigns.filter((campaign) => campaign.status === "Active"),
    [campaigns]
  );

  const allocatableDonations = useMemo(
    () =>
      donations.filter(
        (donation) =>
          donation.status !== "Cancelled" &&
          (donation.unallocatedAmount ?? 0) > 0
      ),
    [donations]
  );

  async function run(action: () => Promise<void>, success?: string) {
    setBusy(true);
    setNotice(null);

    try {
      await action();
      await loadAll();
      if (success) {
        setNotice({ message: success, tone: "success" });
      }
    } catch (caught) {
      setNotice({ message: localizedErrorMessage(caught, t), tone: "error" });
    } finally {
      setBusy(false);
    }
  }

  function onCreateDonor() {
    void run(async () => {
      await apiJson<DonorDto>("/api/v1/donations/donors", "POST", {
        name: donorName,
        donorType,
        contactEmail: donorEmail || null
      });

      setDonorName("");
      setDonorEmail("");
      setDonorType("Individual");
    }, t("donations.successDonorRegistered"));
  }

  function onCreateCampaign() {
    void run(async () => {
      const goal = Number(campaignGoal);

      await apiJson<CampaignDto>("/api/v1/donations/campaigns", "POST", {
        name: campaignName,
        goalAmount: Number.isFinite(goal) ? goal : 0,
        currency: campaignCurrency
      });

      setCampaignName("");
      setCampaignGoal("");
    }, t("donations.successCampaignCreated"));
  }

  function onActivateCampaign(campaign: CampaignDto) {
    void run(async () => {
      await apiJson<CampaignDto>(
        `/api/v1/donations/campaigns/${campaign.id}`,
        "PATCH",
        { status: "Active" satisfies CampaignStatus }
      );
    }, t("donations.successCampaignActivated"));
  }

  function onCreateDonation() {
    void run(async () => {
      const payload =
        donationType === "Monetary"
          ? {
              donorId: donationDonorId,
              donationType,
              amount: parsePositive(donationAmount),
              currency: donationCurrency
            }
          : {
              donorId: donationDonorId,
              donationType,
              itemDescription: donationItem,
              quantity: parsePositive(donationQuantity),
              estimatedValue:
                donationEstimatedValue.trim().length > 0
                  ? Number(donationEstimatedValue)
                  : null,
              currency: donationCurrency
            };

      await apiJson<DonationDto>("/api/v1/donations", "POST", payload);

      setDonationAmount("");
      setDonationItem("");
      setDonationQuantity("");
      setDonationEstimatedValue("");
    }, t("donations.successDonationRecorded"));
  }

  function onAllocate() {
    void run(async () => {
      await apiJson<DonationDto>(
        `/api/v1/donations/${allocationDonationId}/allocations`,
        "POST",
        {
          campaignId: allocationCampaignId,
          amount: parsePositive(allocationAmount)
        }
      );

      setAllocationAmount("");
    }, t("donations.successDonationAllocated"));
  }

  function onToggleFlag(donation: DonationDto) {
    void run(async () => {
      await apiJson<DonationDto>(
        `/api/v1/donations/${donation.id}/flag`,
        "PATCH",
        {
          isSuspicious: !donation.isSuspicious,
          reason: donation.isSuspicious ? null : "Flagged for review"
        }
      );
    }, donation.isSuspicious ? t("donations.successFlagCleared") : t("donations.successDonationFlagged"));
  }

  function onCreateExpense() {
    void run(async () => {
      await apiJson(`/api/v1/donations/campaigns/${expenseCampaignId}/expenses`, "POST", {
        description: expenseDescription,
        amount: parsePositive(expenseAmount)
      });

      setExpenseDescription("");
      setExpenseAmount("");
    }, t("donations.successExpenseRecorded"));
  }

  return (
    <AuthGuard allowedRoles={donationRoles}>
      <div className="page">
        <header className="page__header">
          <div>
            <h1>{t("donations.title")}</h1>
            <p>
              {t("donations.subtitle")}
            </p>
          </div>
          <Button onClick={() => void loadAll()} disabled={loading || busy}>
            {t("donations.refresh")}
          </Button>
        </header>

        {!allowed ? null : (
          <>
            <section className="card">
              <div className="filters">
                <Input
                  label={t("donations.from")}
                  type="date"
                  value={from}
                  onChange={(event) => setFrom(event.target.value)}
                />
                <Input
                  label={t("donations.to")}
                  type="date"
                  value={to}
                  onChange={(event) => setTo(event.target.value)}
                />
              </div>
            </section>

            <section className="card-grid" aria-label={t("donations.summaryAriaLabel")}>
              {loading ? (
                <Skeleton rows={4} />
              ) : (
                <>
                  <div className="card">
                    <div className="metric__label">{t("donations.title")}</div>
                    <div className="metric__value">
                      {report?.totalDonationCount ?? 0}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">{t("donations.donors")}</div>
                    <div className="metric__value">{report?.totalDonorCount ?? 0}</div>
                  </div>
                  <div className="card">
                    <div className="metric__label">{t("donations.flaggedDonations")}</div>
                    <div className="metric__value">
                      {report?.suspiciousDonationCount ?? 0}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">{t("donations.flaggedAllocations")}</div>
                    <div className="metric__value">
                      {report?.suspiciousAllocationCount ?? 0}
                    </div>
                  </div>
                </>
              )}
            </section>

            <section className="card">
              <h2>{t("donations.transparencyReport")}</h2>
              <p className="metric__label">
                {t("donations.reportHint")}
              </p>
              {loading ? (
                <Skeleton rows={3} />
              ) : report && report.currencyTotals.length > 0 ? (
                <div className="table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th scope="col">{t("donations.currency")}</th>
                        <th scope="col">{t("donations.monetary")}</th>
                        <th scope="col">{t("donations.inKindValue")}</th>
                        <th scope="col">{t("donations.allocated")}</th>
                        <th scope="col">{t("donations.spent")}</th>
                        <th scope="col">{t("donations.title")}</th>
                        <th scope="col">{t("donations.donors")}</th>
                        <th scope="col">{t("donations.flagged")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {report.currencyTotals.map((total) => (
                        <tr key={total.currency}>
                          <td>{total.currency}</td>
                          <td>{formatMoney(total.monetaryTotal, total.currency)}</td>
                          <td>
                            {formatMoney(total.materialEstimatedTotal, total.currency)}
                          </td>
                          <td>{formatMoney(total.allocatedTotal, total.currency)}</td>
                          <td>{formatMoney(total.spentTotal, total.currency)}</td>
                          <td>{total.donationCount}</td>
                          <td>{total.donorCount}</td>
                          <td>{total.suspiciousCount}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <p className="metric__label">{t("donations.noDonationsInPeriod")}</p>
              )}

              {report && report.campaigns.length > 0 ? (
                <div className="table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th scope="col">{t("donations.campaign")}</th>
                        <th scope="col">{t("donations.status")}</th>
                        <th scope="col">{t("donations.goal")}</th>
                        <th scope="col">{t("donations.raised")}</th>
                        <th scope="col">{t("donations.spent")}</th>
                        <th scope="col">{t("donations.remaining")}</th>
                        <th scope="col">{t("donations.progress")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {report.campaigns.map((line) => (
                        <tr key={line.campaignId}>
                          <td>{line.name}</td>
                          <td>{line.status}</td>
                          <td>{formatMoney(line.goalAmount, line.currency)}</td>
                          <td>{formatMoney(line.raisedAmount, line.currency)}</td>
                          <td>{formatMoney(line.spentAmount, line.currency)}</td>
                          <td>{formatMoney(line.remainingAmount, line.currency)}</td>
                          <td>{line.progressPercent}%</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : null}

              <p className="metric__label">
                {t("donations.generatedAt", { date: formatDate(report?.generatedAt) })}
              </p>
            </section>

            <section className="card">
              <h2>{t("donations.title")}</h2>
              {loading ? (
                <Skeleton rows={4} />
              ) : donations.length === 0 ? (
                <p className="metric__label">{t("donations.noDonations")}</p>
              ) : (
                <div className="table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th scope="col">{t("donations.reference")}</th>
                        <th scope="col">{t("donations.donor")}</th>
                        <th scope="col">{t("donations.type")}</th>
                        <th scope="col">{t("donations.value")}</th>
                        <th scope="col">{t("donations.allocated")}</th>
                        <th scope="col">{t("donations.status")}</th>
                        <th scope="col">{t("donations.review")}</th>
                        <th scope="col">{t("donations.received")}</th>
                        {canManage ? <th scope="col">{t("donations.actions")}</th> : null}
                      </tr>
                    </thead>
                    <tbody>
                      {donations.map((donation) => {
                        const value =
                          donation.donationType === "Monetary"
                            ? donation.amount
                            : donation.estimatedValue;

                        return (
                          <tr key={donation.id}>
                            <td>
                              <code>{donation.reference}</code>
                            </td>
                            <td>{donation.donorName}</td>
                            <td>{donation.donationType}</td>
                            <td>{formatMoney(value, donation.currency)}</td>
                            <td>
                              {formatMoney(donation.allocatedAmount, donation.currency)}
                            </td>
                            <td>{donation.status}</td>
                            <td>
                              <span
                                className={`badge ${
                                  donation.isSuspicious
                                    ? "badge--danger"
                                    : "badge--ok"
                                }`}
                                title={donation.suspiciousReason ?? undefined}
                              >
                                {donation.isSuspicious ? t("donations.flagged") : t("donations.clear")}
                              </span>
                            </td>
                            <td>{formatDate(donation.receivedAt)}</td>
                            {canManage ? (
                              <td>
                                <Button
                                  variant="secondary"
                                  onClick={() => onToggleFlag(donation)}
                                  disabled={busy}
                                >
                                  {donation.isSuspicious ? t("donations.clearFlag") : t("donations.flag")}
                                </Button>
                              </td>
                            ) : null}
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}

              {canManage ? (
                <div className="filters">
                  <Select
                    label={t("donations.donor")}
                    id="donation-donor"
                    value={donationDonorId}
                    onChange={(event) => setDonationDonorId(event.target.value)}
                  >
                    <option value="">{t("donations.selectDonor")}</option>
                    {donors
                      .filter((donor) => donor.isActive)
                      .map((donor) => (
                        <option key={donor.id} value={donor.id}>
                          {donor.name}
                        </option>
                      ))}
                  </Select>
                  <Select
                    label={t("donations.type")}
                    id="donation-type"
                    value={donationType}
                    onChange={(event) =>
                      setDonationType(event.target.value as DonationType)
                    }
                  >
                    {DonationTypes.map((type) => (
                      <option key={type} value={type}>
                        {type}
                      </option>
                    ))}
                  </Select>
                  <Input
                    label={t("donations.currency")}
                    id="donation-currency"
                    value={donationCurrency}
                    maxLength={3}
                    onChange={(event) =>
                      setDonationCurrency(event.target.value.toUpperCase())
                    }
                  />
                  {donationType === "Monetary" ? (
                    <Input
                      label={t("donations.amount")}
                      id="donation-amount"
                      type="number"
                      min="0.01"
                      step="0.01"
                      value={donationAmount}
                      onChange={(event) => setDonationAmount(event.target.value)}
                    />
                  ) : (
                    <>
                      <Input
                        label={t("donations.item")}
                        value={donationItem}
                        onChange={(event) => setDonationItem(event.target.value)}
                      />
                      <Input
                        label={t("donations.quantity")}
                        type="number"
                        min="1"
                        value={donationQuantity}
                        onChange={(event) => setDonationQuantity(event.target.value)}
                      />
                      <Input
                        label={t("donations.estimatedValue")}
                        type="number"
                        min="0"
                        step="0.01"
                        value={donationEstimatedValue}
                        onChange={(event) =>
                          setDonationEstimatedValue(event.target.value)
                        }
                      />
                    </>
                  )}
                  <Button
                    onClick={onCreateDonation}
                    disabled={
                      busy ||
                      !donationDonorId ||
                      (donationType === "Monetary"
                        ? parsePositive(donationAmount) === null
                        : donationItem.trim().length === 0)
                    }
                  >
                    {t("donations.recordDonation")}
                  </Button>
                </div>
              ) : null}
            </section>

            <section className="card">
              <h2>{t("donations.allocateTitle")}</h2>
              {canManage ? (
                <div className="filters">
                  <Select
                    label={t("donations.donation")}
                    id="allocation-donation"
                    value={allocationDonationId}
                    onChange={(event) => setAllocationDonationId(event.target.value)}
                  >
                    <option value="">{t("donations.selectDonation")}</option>
                    {allocatableDonations.map((donation) => (
                      <option key={donation.id} value={donation.id}>
                        {donation.reference} —{" "}
                        {formatMoney(donation.unallocatedAmount, donation.currency)}{" "}
                        {t("donations.available")}
                      </option>
                    ))}
                  </Select>
                  <Select
                    label={t("donations.campaign")}
                    id="allocation-campaign"
                    value={allocationCampaignId}
                    onChange={(event) => setAllocationCampaignId(event.target.value)}
                  >
                    <option value="">{t("donations.selectCampaign")}</option>
                    {activeCampaigns.map((campaign) => (
                      <option key={campaign.id} value={campaign.id}>
                        {campaign.name} ({campaign.currency})
                      </option>
                    ))}
                  </Select>
                  <Input
                    label={t("donations.amount")}
                    id="allocation-amount"
                    type="number"
                    min="0.01"
                    step="0.01"
                    value={allocationAmount}
                    onChange={(event) => setAllocationAmount(event.target.value)}
                  />
                  <Button
                    onClick={onAllocate}
                    disabled={
                      busy ||
                      !allocationDonationId ||
                      !allocationCampaignId ||
                      parsePositive(allocationAmount) === null
                    }
                  >
                    {t("donations.allocate")}
                  </Button>
                </div>
              ) : (
                <p className="metric__label">
                  {t("donations.allocateRestricted")}
                </p>
              )}
            </section>

            <section className="card">
              <h2>{t("donations.donors")}</h2>
              {loading ? (
                <Skeleton rows={3} />
              ) : donors.length === 0 ? (
                <p className="metric__label">{t("donations.noDonors")}</p>
              ) : (
                <div className="table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th scope="col">{t("donations.name")}</th>
                        <th scope="col">{t("donations.type")}</th>
                        <th scope="col">{t("donations.anonymous")}</th>
                        <th scope="col">{t("donations.title")}</th>
                        <th scope="col">{t("donations.contributed")}</th>
                        <th scope="col">{t("donations.status")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {donors.map((donor) => (
                        <tr key={donor.id}>
                          <td>{donor.name}</td>
                          <td>{donor.donorType}</td>
                          <td>{donor.isAnonymous ? t("donations.yes") : t("donations.no")}</td>
                          <td>{donor.donationCount}</td>
                          <td>{donor.totalContributed.toFixed(2)}</td>
                          <td>
                            <span
                              className={`badge ${
                                donor.isActive ? "badge--ok" : "badge--muted"
                              }`}
                            >
                              {donor.isActive ? t("donations.active") : t("donations.inactive")}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              {canManage ? (
                <div className="filters">
                  <Input
                    label={t("donations.name")}
                    value={donorName}
                    onChange={(event) => setDonorName(event.target.value)}
                  />
                  <Select
                    label={t("donations.donorType")}
                    value={donorType}
                    onChange={(event) =>
                      setDonorType(event.target.value as DonorType)
                    }
                  >
                    {DonorTypes.map((type) => (
                      <option key={type} value={type}>
                        {type}
                      </option>
                    ))}
                  </Select>
                  <Input
                    label={t("donations.contactEmail")}
                    type="email"
                    value={donorEmail}
                    onChange={(event) => setDonorEmail(event.target.value)}
                  />
                  <Button
                    onClick={onCreateDonor}
                    disabled={busy || donorName.trim().length === 0}
                  >
                    {t("donations.registerDonor")}
                  </Button>
                </div>
              ) : null}
            </section>

            <section className="card">
              <h2>{t("donations.campaigns")}</h2>
              {loading ? (
                <Skeleton rows={3} />
              ) : campaigns.length === 0 ? (
                <p className="metric__label">{t("donations.noCampaigns")}</p>
              ) : (
                <div className="table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th scope="col">{t("donations.name")}</th>
                        <th scope="col">{t("donations.status")}</th>
                        <th scope="col">{t("donations.goal")}</th>
                        <th scope="col">{t("donations.raised")}</th>
                        <th scope="col">{t("donations.spent")}</th>
                        <th scope="col">{t("donations.progress")}</th>
                        {canManage ? <th scope="col">{t("donations.actions")}</th> : null}
                      </tr>
                    </thead>
                    <tbody>
                      {campaigns.map((campaign) => (
                        <tr key={campaign.id}>
                          <td>{campaign.name}</td>
                          <td>{campaign.status}</td>
                          <td>{formatMoney(campaign.goalAmount, campaign.currency)}</td>
                          <td>{formatMoney(campaign.raisedAmount, campaign.currency)}</td>
                          <td>{formatMoney(campaign.spentAmount, campaign.currency)}</td>
                          <td>{campaign.progressPercent}%</td>
                          {canManage ? (
                            <td>
                              {campaign.status === "Draft" ? (
                                <Button
                                  variant="secondary"
                                  onClick={() => onActivateCampaign(campaign)}
                                  disabled={busy}
                                >
                                  {t("donations.activate")}
                                </Button>
                              ) : null}
                            </td>
                          ) : null}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              {canManage ? (
                <>
                  <div className="filters">
                    <Input
                      label={t("donations.campaignName")}
                      value={campaignName}
                      onChange={(event) => setCampaignName(event.target.value)}
                    />
                    <Input
                      label={t("donations.goalAmount")}
                      type="number"
                      min="0"
                      step="0.01"
                      value={campaignGoal}
                      onChange={(event) => setCampaignGoal(event.target.value)}
                    />
                    <Input
                      label={t("donations.currency")}
                      id="campaign-currency"
                      value={campaignCurrency}
                      maxLength={3}
                      onChange={(event) =>
                        setCampaignCurrency(event.target.value.toUpperCase())
                      }
                    />
                    <Button
                      onClick={onCreateCampaign}
                      disabled={busy || campaignName.trim().length === 0}
                    >
                      {t("donations.createCampaign")}
                    </Button>
                  </div>

                  <h3>{t("donations.expenseTitle")}</h3>
                  <div className="filters">
                    <Select
                      label={t("donations.campaign")}
                      id="expense-campaign"
                      value={expenseCampaignId}
                      onChange={(event) => setExpenseCampaignId(event.target.value)}
                    >
                      <option value="">{t("donations.selectCampaign")}</option>
                      {campaigns
                        .filter((campaign) => campaign.status !== "Cancelled")
                        .map((campaign) => (
                          <option key={campaign.id} value={campaign.id}>
                            {campaign.name} ({campaign.currency})
                          </option>
                        ))}
                    </Select>
                    <Input
                      label={t("donations.description")}
                      value={expenseDescription}
                      onChange={(event) => setExpenseDescription(event.target.value)}
                    />
                    <Input
                      label={t("donations.amount")}
                      id="expense-amount"
                      type="number"
                      min="0.01"
                      step="0.01"
                      value={expenseAmount}
                      onChange={(event) => setExpenseAmount(event.target.value)}
                    />
                    <Button
                      onClick={onCreateExpense}
                      disabled={
                        busy ||
                        !expenseCampaignId ||
                        expenseDescription.trim().length === 0 ||
                        parsePositive(expenseAmount) === null
                      }
                    >
                      {t("donations.recordExpense")}
                    </Button>
                  </div>
                </>
              ) : null}
            </section>

            <section className="card">
              <h2>{t("donations.statusReferenceTitle")}</h2>
              <p className="metric__label">
                {t("donations.statusReference", { donations: DonationStatuses.join(", "), campaigns: CampaignStatuses.join(", ") })}
              </p>
            </section>
          </>
        )}
      </div>

      <Toast
        message={notice?.message ?? null}
        tone={notice?.tone}
        onDismiss={() => setNotice(null)}
      />
    </AuthGuard>
  );
}
