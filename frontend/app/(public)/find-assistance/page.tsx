"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import MapView, { type MapMarker } from "@/components/map/MapView";
import { Icon } from "@/components/safeway/Icon";
import { PortalState } from "@/components/safeway/PortalPrimitives";
import { api } from "@/lib/api";
import { useI18n } from "@/lib/i18n";
import { localizedErrorMessage } from "@/lib/i18n/errors";

interface PublicServiceCentre {
  id: string;
  name: string;
  address: string;
  lat: number;
  lng: number;
  services: string;
  openingHours?: string | null;
}

type CategoryKey = "centres" | "food" | "water" | "medical" | "shelter" | "supplies";

const CATEGORY_FILTERS: { key: CategoryKey; labelKey: string; icon: string; patterns: RegExp[] }[] = [
  { key: "centres", labelKey: "categoryCentres", icon: "building", patterns: [] },
  { key: "food", labelKey: "categoryFood", icon: "box", patterns: [/\bfood\b/, /\bmeals?\b/, /\bparcels?\b/, /\brations?\b/] },
  { key: "water", labelKey: "categoryWater", icon: "droplet", patterns: [/\bwater\b/, /\bdrink(?:ing)?\b/] },
  { key: "medical", labelKey: "categoryMedical", icon: "medical", patterns: [/\bmedical\b/, /\bmedicine\b/, /\bhealth\b/, /\bclinic\b/] },
  { key: "shelter", labelKey: "categoryShelter", icon: "home", patterns: [/\bshelter\b/, /\bsafe space\b/, /\baccommodation\b/] },
  { key: "supplies", labelKey: "categorySupplies", icon: "heartHandshake", patterns: [/\bsupplies\b/, /\bblankets?\b/, /\bwheelchair\b/, /accessib/] }
];

export default function FindAssistancePage() {
  const { t } = useI18n();
  const [centres, setCentres] = useState<PublicServiceCentre[]>([]);
  const [query, setQuery] = useState("");
  const [category, setCategory] = useState<CategoryKey>("centres");
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [refreshToken, setRefreshToken] = useState(0);

  useEffect(() => {
    let active = true;
    if (refreshToken === 0) setLoading(true);
    else setRefreshing(true);

    api<PublicServiceCentre[]>("/api/v1/public/service-centres")
      .then((rows) => {
        if (!active) return;
        setCentres(rows);
        setError(null);
      })
      .catch((cause) => {
        if (active) setError(localizedErrorMessage(cause, t));
      })
      .finally(() => {
        if (!active) return;
        setLoading(false);
        setRefreshing(false);
      });

    return () => {
      active = false;
    };
  }, [t, refreshToken]);

  const visibleCentres = useMemo(() => {
    const needle = query.trim().toLocaleLowerCase();
    const patterns = CATEGORY_FILTERS.find((item) => item.key === category)?.patterns ?? [];

    return centres.filter((centre) => {
      const haystack = [centre.name, centre.address, centre.services, centre.openingHours ?? ""]
        .join(" ")
        .toLocaleLowerCase();
      if (needle && !haystack.includes(needle)) return false;
      return patterns.length === 0 || patterns.some((pattern) => pattern.test(haystack));
    });
  }, [category, centres, query]);

  const markers: MapMarker[] = visibleCentres.map((centre) => ({
    id: centre.id,
    lat: Number(centre.lat),
    lng: Number(centre.lng),
    label: centre.name,
    type: "service-centre",
    description: centre.address
  }));

  function clearFilters() {
    setQuery("");
    setCategory("centres");
  }

  return (
    <main className="public-info citizen-assistance">
      <header className="page-hero">
        <div className="page-hero-lines" />
        <div className="eyebrow">
          <span /> {t("find.eyebrow") || t("public.find.eyebrowFallback")}
        </div>
        <h1>{t("find.title") || t("public.find.titleFallback")}</h1>
        <p>{t("find.intro") || t("public.find.introFallback")}</p>
      </header>

      <div className="page-body">
        <aside className="public-availability-note" role="note">
          <span>
            <Icon name="info" size={20} />
          </span>
          <div>
            <b>{t("find.noteTitle") || t("public.find.noteTitleFallback")}</b>
            <p>{t("find.noteCopy") || t("public.find.noteCopyFallback")}</p>
          </div>
        </aside>

        <section className="citizen-assistance__controls" aria-label={t("citizenX.assistance.categoryLabel")}>
          <label className="field-label" htmlFor="assistance-search">
            {t("citizenX.assistance.searchLabel")}
          </label>
          <div className="citizen-assistance__search">
            <Icon name="search" size={18} />
            <input
              id="assistance-search"
              type="search"
              value={query}
              placeholder={t("citizenX.assistance.searchPlaceholder")}
              onChange={(event) => setQuery(event.target.value)}
            />
          </div>

          <p className="field-label citizen-assistance__filter-label">{t("citizenX.assistance.categoryLabel")}</p>
          <div className="citizen-assistance__categories" role="group" aria-label={t("citizenX.assistance.categoryLabel")}>
            {CATEGORY_FILTERS.map((item) => (
              <button
                className={category === item.key ? "active" : ""}
                type="button"
                key={item.key}
                aria-pressed={category === item.key}
                onClick={() => setCategory(item.key)}
              >
                <Icon name={item.icon} size={18} />
                <span>{t(`citizenX.assistance.${item.labelKey}`)}</span>
              </button>
            ))}
          </div>

          {query || category !== "centres" ? (
            <button className="btn btn-outline citizen-assistance__clear" type="button" onClick={clearFilters}>
              {t("citizenX.assistance.clear")}
            </button>
          ) : null}
        </section>

        <section className="citizen-assistance__results" aria-labelledby="assistance-results-heading">
          <div className="section-head">
            <div>
              <h2 id="assistance-results-heading">{t("citizenX.assistance.resultsTitle")}</h2>
              <p>{t("citizenX.assistance.resultsDescription")}</p>
            </div>
            {!loading && !error ? (
              <span className="badge blue">
                <i aria-hidden="true" /> {t("citizenX.assistance.resultsCount", { count: visibleCentres.length })}
              </span>
            ) : null}
          </div>

          {error ? (
            <div className="citizen-error-bar" role="alert">
              <Icon name="info" size={18} />
              <span>{error}</span>
              <button
                className="btn btn-outline"
                type="button"
                onClick={() => setRefreshToken((value) => value + 1)}
                disabled={refreshing}
                aria-busy={refreshing}
              >
                {refreshing ? t("citizenX.tracking.refreshing") : t("citizenX.tracking.refresh")}
              </button>
            </div>
          ) : null}

          {loading ? (
            <PortalState kind="loading" title={t("citizenX.tracking.loadingTitle")}>
              {t("citizenX.tracking.loadingBody")}
            </PortalState>
          ) : visibleCentres.length === 0 ? (
            <PortalState kind="empty" title={t("citizenX.assistance.noResultsTitle")}>
              {t("citizenX.assistance.noResultsBody")}
            </PortalState>
          ) : (
            <div className="citizen-assistance__grid">
              {visibleCentres.map((centre) => {
                const services = centre.services
                  .split(";")
                  .map((item) => item.trim())
                  .filter(Boolean);
                const mapUrl = `https://www.openstreetmap.org/?mlat=${centre.lat}&mlon=${centre.lng}#map=15/${centre.lat}/${centre.lng}`;
                return (
                  <article className="citizen-centre-card" key={centre.id}>
                    <div className="citizen-centre-card__head">
                      <i aria-hidden="true"><Icon name="pin" size={22} /></i>
                      <div>
                        <h3>{centre.name}</h3>
                        <p>{centre.address}</p>
                      </div>
                    </div>
                    <div className="citizen-centre-card__services">
                      <b>{t("citizenX.assistance.services")}</b>
                      <div className="citizen-chip-row">
                        {services.length > 0 ? (
                          services.map((service) => <span key={service}>{service}</span>)
                        ) : (
                          <span>{t("citizenX.tracking.notProvided")}</span>
                        )}
                      </div>
                    </div>
                    <dl className="citizen-centre-card__meta">
                      <div>
                        <dt>{t("citizenX.assistance.hours")}</dt>
                        <dd>{centre.openingHours?.trim() || t("citizenX.tracking.notProvided")}</dd>
                      </div>
                    </dl>
                    <a
                      className="citizen-centre-card__link"
                      href={mapUrl}
                      target="_blank"
                      rel="noreferrer"
                      aria-label={`${t("citizenX.assistance.mapAction")}: ${centre.name}`}
                    >
                      {t("citizenX.assistance.mapAction")} <Icon name="arrow" size={16} />
                    </a>
                  </article>
                );
              })}
            </div>
          )}
        </section>

        {!loading && !error && markers.length > 0 ? (
          <section className="citizen-assistance__map" aria-labelledby="assistance-map-heading">
            <div className="section-head">
              <div>
                <h2 id="assistance-map-heading">{t("citizenX.assistance.mapTitle")}</h2>
              </div>
            </div>
            <MapView markers={markers} height={320} />
          </section>
        ) : null}

        <section className="service-choice public-single-choice">
          <div>
            <small>{t("find.ctaSmall") || t("public.find.ctaSmallFallback")}</small>
            <h2>{t("find.ctaTitle") || t("public.find.ctaTitleFallback")}</h2>
          </div>
          <Link className="btn btn-primary" href="/report">
            {t("nav.reportCrisis")} <Icon name="arrow" />
          </Link>
        </section>
      </div>
    </main>
  );
}
