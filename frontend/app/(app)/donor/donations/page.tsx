"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { DonationCard } from "@/components/safeway/DonationCard";
import { DonorPage } from "@/components/safeway/DonorPage";
import { Icon } from "@/components/safeway/Icon";
import { PortalSection, PortalState } from "@/components/safeway/PortalPrimitives";
import { donationStatusLabel, donationSummary } from "@/lib/donor";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useT } from "@/lib/i18n";
import { getDonorDonations, type DonationDto } from "@/lib/portal";

type LoadState = "loading" | "ready" | "error";

const STATUS_OPTIONS = ["Received", "Allocated", "Cancelled"] as const;

export default function DonorDonationsPage() {
  const t = useT();
  const [search, setSearch] = useState("");
  const [type, setType] = useState("");
  const [status, setStatus] = useState("");
  const [donations, setDonations] = useState<DonationDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [state, setState] = useState<LoadState>("loading");
  const [error, setError] = useState<string | null>(null);
  const [loadingMore, setLoadingMore] = useState(false);
  const [moreError, setMoreError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    setState("loading");
    setMoreError(null);
    getDonorDonations(1, 20, { type, status })
      .then((result) => {
        if (!active) return;
        setDonations(result.items);
        setTotal(result.totalCount);
        setPage(1);
        setState("ready");
        setError(null);
      })
      .catch((cause) => {
        if (!active) return;
        setState("error");
        setError(localizedErrorMessage(cause, t));
      });
    return () => {
      active = false;
    };
  }, [type, status, t]);

  const visible = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return donations;
    return donations.filter((donation) =>
      [donation.reference, donationSummary(t, donation), donation.itemDescription ?? ""]
        .join(" ")
        .toLowerCase()
        .includes(term)
    );
  }, [donations, search, t]);

  async function loadMore() {
    if (loadingMore) return;
    setLoadingMore(true);
    setMoreError(null);
    try {
      const nextPage = page + 1;
      const result = await getDonorDonations(nextPage, 20, { type, status });
      setDonations((current) => [...current, ...result.items]);
      setPage(nextPage);
      setTotal(result.totalCount);
    } catch (cause) {
      setMoreError(localizedErrorMessage(cause, t));
    } finally {
      setLoadingMore(false);
    }
  }

  return (
    <DonorPage
      eyebrowKey="donorX.donations.eyebrow"
      titleKey="donorX.donations.title"
      descriptionKey="donorX.donations.description"
      action={
        <Link className="btn btn-primary" href="/donor/donate">
          <Icon name="plus" size={16} /> {t("donorX.landing.cardDonateCta")}
        </Link>
      }
    >
      <PortalSection title={t("donorX.donations.title")}>
        <div className="donor-filter-bar">
          <div className="donor-field donor-filter-bar__search">
            <label htmlFor="donor-donations-search">{t("donorX.donations.searchLabel")}</label>
            <div className="donor-input-icon">
              <Icon name="search" size={16} />
              <input
                id="donor-donations-search"
                type="search"
                value={search}
                placeholder={t("donorX.donations.searchPlaceholder")}
                onChange={(event) => setSearch(event.target.value)}
              />
            </div>
          </div>
          <div className="donor-field">
            <label htmlFor="donor-donations-type">{t("donorX.donations.filterTypeLabel")}</label>
            <select id="donor-donations-type" value={type} onChange={(event) => setType(event.target.value)}>
              <option value="">{t("donorX.donations.filterAll")}</option>
              <option value="Monetary">{t("donorX.donations.filterMonetary")}</option>
              <option value="Material">{t("donorX.donations.filterMaterial")}</option>
            </select>
          </div>
          <div className="donor-field">
            <label htmlFor="donor-donations-status">{t("donorX.donations.filterStatusLabel")}</label>
            <select id="donor-donations-status" value={status} onChange={(event) => setStatus(event.target.value)}>
              <option value="">{t("donorX.donations.filterAll")}</option>
              {STATUS_OPTIONS.map((option) => (
                <option value={option} key={option}>{donationStatusLabel(t, option)}</option>
              ))}
            </select>
          </div>
        </div>

        {state === "loading" ? (
          <PortalState kind="loading" title={t("donorX.donations.loadingTitle")}>
            {t("donorX.donations.loadingBody")}
          </PortalState>
        ) : state === "error" ? (
          <PortalState kind="error" title={t("donorX.donations.errorTitle")}>
            {error}
          </PortalState>
        ) : visible.length === 0 ? (
          <PortalState
            kind="empty"
            title={t("donorX.donations.emptyTitle")}
            href="/donor/donate"
            linkLabel={t("donorX.landing.cardDonateCta")}
          >
            {t("donorX.donations.emptyBody")}
          </PortalState>
        ) : (
          <>
            <p className="donor-results-count">
              {t("donorX.donations.results", { count: search.trim() ? visible.length : total })}
            </p>
            <div className="donor-donation-list">
              {visible.map((donation) => (
                <DonationCard donation={donation} key={donation.id} />
              ))}
            </div>
            {moreError ? (
              <PortalState kind="error" title={t("donorX.donations.errorTitle")}>
                {moreError}
              </PortalState>
            ) : null}
            {!search.trim() && donations.length < total ? (
              <button
                className="btn btn-outline donor-load-more"
                type="button"
                onClick={() => void loadMore()}
                disabled={loadingMore}
                aria-busy={loadingMore}
              >
                {loadingMore ? t("donorX.shared.refresh") : t("donorX.donations.loadMore")}
              </button>
            ) : !search.trim() && donations.length > 0 ? (
              <p className="donor-results-count">{t("donorX.donations.allLoaded")}</p>
            ) : null}
          </>
        )}
      </PortalSection>
    </DonorPage>
  );
}
