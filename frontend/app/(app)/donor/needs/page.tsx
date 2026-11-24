"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { CampaignCard } from "@/components/safeway/CampaignCard";
import { DonorPage } from "@/components/safeway/DonorPage";
import { Icon } from "@/components/safeway/Icon";
import { PortalSection, PortalState } from "@/components/safeway/PortalPrimitives";
import { backendTimestamp } from "@/lib/donor";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useT } from "@/lib/i18n";
import { getDonorCampaigns, type CampaignDto } from "@/lib/portal";

type LoadState = "loading" | "ready" | "error";

export default function DonorNeedsPage() {
  const t = useT();
  const router = useRouter();
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [currency, setCurrency] = useState("");
  const [deadline, setDeadline] = useState("all");
  const [campaigns, setCampaigns] = useState<CampaignDto[]>([]);
  const [state, setState] = useState<LoadState>("loading");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedSearch(search.trim()), 250);
    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    let active = true;
    setState("loading");
    getDonorCampaigns(1, 50, debouncedSearch)
      .then((result) => {
        if (!active) return;
        setCampaigns(result.items);
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
  }, [debouncedSearch, t]);

  const visible = useMemo(() => {
    const now = Date.now();
    const soon = now + 7 * 24 * 60 * 60 * 1000;
    return campaigns.filter((campaign) => {
      if (currency && campaign.currency?.toUpperCase() !== currency) return false;
      if (deadline !== "soon") return true;
      const timestamp = backendTimestamp(campaign.endsAt);
      if (!timestamp) return false;
      const ends = new Date(timestamp).getTime();
      return Number.isFinite(ends) && ends >= now && ends <= soon;
    });
  }, [campaigns, currency, deadline]);

  function support(campaign: CampaignDto) {
    const params = new URLSearchParams({ campaign: campaign.id });
    if (campaign.currency) params.set("currency", campaign.currency.toUpperCase());
    router.push(`/donor/donate?${params.toString()}`);
  }

  return (
    <DonorPage
      eyebrowKey="donorX.needs.eyebrow"
      titleKey="donorX.needs.title"
      descriptionKey="donorX.needs.description"
      action={
        <Link className="btn btn-outline" href="/donor/donate">
          {t("donorX.needs.donateGeneral")} <Icon name="arrow" size={15} />
        </Link>
      }
    >
      <PortalSection title={t("donorX.needs.title")}>
        <div className="donor-filter-bar">
          <div className="donor-field donor-filter-bar__search">
            <label htmlFor="donor-needs-search">{t("donorX.needs.searchLabel")}</label>
            <div className="donor-input-icon">
              <Icon name="search" size={16} />
              <input
                id="donor-needs-search"
                type="search"
                value={search}
                placeholder={t("donorX.needs.searchPlaceholder")}
                onChange={(event) => setSearch(event.target.value)}
              />
            </div>
          </div>
          <div className="donor-field">
            <label htmlFor="donor-needs-currency">{t("donorX.needs.filterCurrencyLabel")}</label>
            <select
              id="donor-needs-currency"
              value={currency}
              onChange={(event) => setCurrency(event.target.value)}
            >
              <option value="">{t("donorX.needs.filterCurrencyAll")}</option>
              <option value="LKR">LKR</option>
              <option value="USD">USD</option>
            </select>
          </div>
          <div className="donor-field">
            <label htmlFor="donor-needs-deadline">{t("donorX.needs.filterDeadlineLabel")}</label>
            <select
              id="donor-needs-deadline"
              value={deadline}
              onChange={(event) => setDeadline(event.target.value)}
            >
              <option value="all">{t("donorX.needs.filterDeadlineAll")}</option>
              <option value="soon">{t("donorX.needs.filterDeadlineSoon")}</option>
            </select>
          </div>
        </div>

        {state === "loading" ? (
          <PortalState kind="loading" title={t("donorX.needs.loadingTitle")}>
            {t("donorX.needs.loadingBody")}
          </PortalState>
        ) : state === "error" ? (
          <PortalState kind="error" title={t("donorX.needs.errorTitle")}>
            {error}
          </PortalState>
        ) : visible.length === 0 ? (
          <PortalState
            kind="empty"
            title={t("donorX.needs.emptyTitle")}
            href="/donor/donate"
            linkLabel={t("donorX.needs.donateGeneral")}
          >
            {t("donorX.needs.emptyBody")}
          </PortalState>
        ) : (
          <>
            <p className="donor-results-count">
              {t(
                visible.length === 1 ? "donorX.needs.resultsOne" : "donorX.needs.results",
                { count: visible.length }
              )}
            </p>
            <div className="donor-campaign-grid">
              {visible.map((campaign) => (
                <CampaignCard campaign={campaign} key={campaign.id} onSupport={support} />
              ))}
            </div>
            <p className="donor-campaign-note">
              <Icon name="info" size={14} /> {t("donorX.needs.currencyNote")}
            </p>
          </>
        )}
      </PortalSection>
    </DonorPage>
  );
}
