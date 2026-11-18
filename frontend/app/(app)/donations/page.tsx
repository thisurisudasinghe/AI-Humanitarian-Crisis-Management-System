"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { Button, Input, Select, Skeleton, Toast } from "@/components/ui";
import { AuthGuard } from "@/components/AuthGuard";
import { api, apiJson, errorMessage } from "@/lib/api";
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

const donationRoles = [
  RoleNames.SystemAdmin,
  RoleNames.DonorOfficer,
  RoleNames.EmergencyCoordinator,
  RoleNames.PartnerOfficer
];

const managerRoles = [RoleNames.SystemAdmin, RoleNames.DonorOfficer];

type Notice = { message: string; tone: "error" | "success" } | null;

function isoDate(date: Date): string {
  return date.toISOString().slice(0, 10);
}

function parsePositive(value: string): number | null {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

export default function DonationsPage() {
  const { user } = useAuth();

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
    const now = new Date();
    setFrom(isoDate(new Date(now.getFullYear(), now.getMonth(), 1)));
    setTo(isoDate(now));
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
          setNotice({ message: errorMessage(caught), tone: "error" });
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
      setNotice({ message: errorMessage(caught), tone: "error" });
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
    }, "Donor registered.");
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
    }, "Campaign created in Draft state.");
  }

  function onActivateCampaign(campaign: CampaignDto) {
    void run(async () => {
      await apiJson<CampaignDto>(
        `/api/v1/donations/campaigns/${campaign.id}`,
        "PATCH",
        { status: "Active" satisfies CampaignStatus }
      );
    }, "Campaign activated.");
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
    }, "Donation recorded.");
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
    }, "Donation allocated.");
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
    }, donation.isSuspicious ? "Flag cleared." : "Donation flagged.");
  }

  function onCreateExpense() {
    void run(async () => {
      await apiJson(`/api/v1/donations/campaigns/${expenseCampaignId}/expenses`, "POST", {
        description: expenseDescription,
        amount: parsePositive(expenseAmount)
      });

      setExpenseDescription("");
      setExpenseAmount("");
    }, "Expense recorded.");
  }

  return (
    <AuthGuard allowedRoles={donationRoles}>
      <div className="page">
        <header className="page__header">
          <div>
            <h1>Donations</h1>
            <p>
              Record donations, allocate them to campaigns and review the
              transparency report.
            </p>
          </div>
          <Button onClick={() => void loadAll()} disabled={loading || busy}>
            Refresh
          </Button>
        </header>

        {!allowed ? null : (
          <>
            <section className="card">
              <div className="filters">
                <Input
                  label="From"
                  type="date"
                  value={from}
                  onChange={(event) => setFrom(event.target.value)}
                />
                <Input
                  label="To"
                  type="date"
                  value={to}
                  onChange={(event) => setTo(event.target.value)}
                />
              </div>
            </section>

            <section className="card-grid" aria-label="Donation summary">
              {loading ? (
                <Skeleton rows={4} />
              ) : (
                <>
                  <div className="card">
                    <div className="metric__label">Donations</div>
                    <div className="metric__value">
                      {report?.totalDonationCount ?? 0}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">Donors</div>
                    <div className="metric__value">{report?.totalDonorCount ?? 0}</div>
                  </div>
                  <div className="card">
                    <div className="metric__label">Flagged donations</div>
                    <div className="metric__value">
                      {report?.suspiciousDonationCount ?? 0}
                    </div>
                  </div>
                  <div className="card">
                    <div className="metric__label">Flagged allocations</div>
                    <div className="metric__value">
                      {report?.suspiciousAllocationCount ?? 0}
                    </div>
                  </div>
                </>
              )}
            </section>

            <section className="card">
              <h2>Transparency report</h2>
              <p className="metric__label">
                Aggregated by currency. Donor names are never included.
              </p>
              {loading ? (
                <Skeleton rows={3} />
              ) : report && report.currencyTotals.length > 0 ? (
                <div className="table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th scope="col">Currency</th>
                        <th scope="col">Monetary</th>
                        <th scope="col">In-kind value</th>
                        <th scope="col">Allocated</th>
                        <th scope="col">Spent</th>
                        <th scope="col">Donations</th>
                        <th scope="col">Donors</th>
                        <th scope="col">Flagged</th>
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
                <p className="metric__label">No donations in this period yet.</p>
              )}

              {report && report.campaigns.length > 0 ? (
                <div className="table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th scope="col">Campaign</th>
                        <th scope="col">Status</th>
                        <th scope="col">Goal</th>
                        <th scope="col">Raised</th>
                        <th scope="col">Spent</th>
                        <th scope="col">Remaining</th>
                        <th scope="col">Progress</th>
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
                Generated at {formatDate(report?.generatedAt)}
              </p>
            </section>

            <section className="card">
              <h2>Donations</h2>
              {loading ? (
                <Skeleton rows={4} />
              ) : donations.length === 0 ? (
                <p className="metric__label">No donations recorded yet.</p>
              ) : (
                <div className="table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th scope="col">Reference</th>
                        <th scope="col">Donor</th>
                        <th scope="col">Type</th>
                        <th scope="col">Value</th>
                        <th scope="col">Allocated</th>
                        <th scope="col">Status</th>
                        <th scope="col">Review</th>
                        <th scope="col">Received</th>
                        {canManage ? <th scope="col">Actions</th> : null}
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
                                {donation.isSuspicious ? "Flagged" : "Clear"}
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
                                  {donation.isSuspicious ? "Clear flag" : "Flag"}
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
                    label="Donor"
                    value={donationDonorId}
                    onChange={(event) => setDonationDonorId(event.target.value)}
                  >
                    <option value="">Select a donor</option>
                    {donors
                      .filter((donor) => donor.isActive)
                      .map((donor) => (
                        <option key={donor.id} value={donor.id}>
                          {donor.name}
                        </option>
                      ))}
                  </Select>
                  <Select
                    label="Type"
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
                    label="Currency"
                    value={donationCurrency}
                    maxLength={3}
                    onChange={(event) =>
                      setDonationCurrency(event.target.value.toUpperCase())
                    }
                  />
                  {donationType === "Monetary" ? (
                    <Input
                      label="Amount"
                      type="number"
                      min="0.01"
                      step="0.01"
                      value={donationAmount}
                      onChange={(event) => setDonationAmount(event.target.value)}
                    />
                  ) : (
                    <>
                      <Input
                        label="Item"
                        value={donationItem}
                        onChange={(event) => setDonationItem(event.target.value)}
                      />
                      <Input
                        label="Quantity"
                        type="number"
                        min="1"
                        value={donationQuantity}
                        onChange={(event) => setDonationQuantity(event.target.value)}
                      />
                      <Input
                        label="Estimated value"
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
                    Record donation
                  </Button>
                </div>
              ) : null}
            </section>

            <section className="card">
              <h2>Allocate a donation</h2>
              {canManage ? (
                <div className="filters">
                  <Select
                    label="Donation"
                    value={allocationDonationId}
                    onChange={(event) => setAllocationDonationId(event.target.value)}
                  >
                    <option value="">Select a donation</option>
                    {allocatableDonations.map((donation) => (
                      <option key={donation.id} value={donation.id}>
                        {donation.reference} —{" "}
                        {formatMoney(donation.unallocatedAmount, donation.currency)}{" "}
                        available
                      </option>
                    ))}
                  </Select>
                  <Select
                    label="Campaign"
                    value={allocationCampaignId}
                    onChange={(event) => setAllocationCampaignId(event.target.value)}
                  >
                    <option value="">Select a campaign</option>
                    {activeCampaigns.map((campaign) => (
                      <option key={campaign.id} value={campaign.id}>
                        {campaign.name} ({campaign.currency})
                      </option>
                    ))}
                  </Select>
                  <Input
                    label="Amount"
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
                    Allocate
                  </Button>
                </div>
              ) : (
                <p className="metric__label">
                  Only Donor Officers and System Administrators can allocate
                  donations.
                </p>
              )}
            </section>

            <section className="card">
              <h2>Donors</h2>
              {loading ? (
                <Skeleton rows={3} />
              ) : donors.length === 0 ? (
                <p className="metric__label">No donors registered yet.</p>
              ) : (
                <div className="table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th scope="col">Name</th>
                        <th scope="col">Type</th>
                        <th scope="col">Anonymous</th>
                        <th scope="col">Donations</th>
                        <th scope="col">Contributed (all currencies)</th>
                        <th scope="col">Status</th>
                      </tr>
                    </thead>
                    <tbody>
                      {donors.map((donor) => (
                        <tr key={donor.id}>
                          <td>{donor.name}</td>
                          <td>{donor.donorType}</td>
                          <td>{donor.isAnonymous ? "Yes" : "No"}</td>
                          <td>{donor.donationCount}</td>
                          <td>{donor.totalContributed.toFixed(2)}</td>
                          <td>
                            <span
                              className={`badge ${
                                donor.isActive ? "badge--ok" : "badge--muted"
                              }`}
                            >
                              {donor.isActive ? "Active" : "Inactive"}
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
                    label="Name"
                    value={donorName}
                    onChange={(event) => setDonorName(event.target.value)}
                  />
                  <Select
                    label="Donor type"
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
                    label="Contact email"
                    type="email"
                    value={donorEmail}
                    onChange={(event) => setDonorEmail(event.target.value)}
                  />
                  <Button
                    onClick={onCreateDonor}
                    disabled={busy || donorName.trim().length === 0}
                  >
                    Register donor
                  </Button>
                </div>
              ) : null}
            </section>

            <section className="card">
              <h2>Campaigns</h2>
              {loading ? (
                <Skeleton rows={3} />
              ) : campaigns.length === 0 ? (
                <p className="metric__label">No campaigns created yet.</p>
              ) : (
                <div className="table-wrap">
                  <table className="table">
                    <thead>
                      <tr>
                        <th scope="col">Name</th>
                        <th scope="col">Status</th>
                        <th scope="col">Goal</th>
                        <th scope="col">Raised</th>
                        <th scope="col">Spent</th>
                        <th scope="col">Progress</th>
                        {canManage ? <th scope="col">Actions</th> : null}
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
                                  Activate
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
                      label="Campaign name"
                      value={campaignName}
                      onChange={(event) => setCampaignName(event.target.value)}
                    />
                    <Input
                      label="Goal amount"
                      type="number"
                      min="0"
                      step="0.01"
                      value={campaignGoal}
                      onChange={(event) => setCampaignGoal(event.target.value)}
                    />
                    <Input
                      label="Currency"
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
                      Create campaign
                    </Button>
                  </div>

                  <h3>Record an expense</h3>
                  <div className="filters">
                    <Select
                      label="Campaign"
                      value={expenseCampaignId}
                      onChange={(event) => setExpenseCampaignId(event.target.value)}
                    >
                      <option value="">Select a campaign</option>
                      {campaigns
                        .filter((campaign) => campaign.status !== "Cancelled")
                        .map((campaign) => (
                          <option key={campaign.id} value={campaign.id}>
                            {campaign.name} ({campaign.currency})
                          </option>
                        ))}
                    </Select>
                    <Input
                      label="Description"
                      value={expenseDescription}
                      onChange={(event) => setExpenseDescription(event.target.value)}
                    />
                    <Input
                      label="Amount"
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
                      Record expense
                    </Button>
                  </div>
                </>
              ) : null}
            </section>

            <section className="card">
              <h2>Status reference</h2>
              <p className="metric__label">
                Donation statuses: {DonationStatuses.join(", ")}. Campaign
                statuses: {CampaignStatuses.join(", ")}.
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
