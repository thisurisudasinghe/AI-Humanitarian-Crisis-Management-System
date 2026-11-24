"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useState } from "react";
import MapView, { type MapMarker } from "@/components/map/MapView";
import { Icon } from "@/components/safeway/Icon";
import { PortalState } from "@/components/safeway/PortalPrimitives";
import { useI18n } from "@/lib/i18n";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import {
  finiteCoordinate,
  getPublicMap,
  incidentMarkerType,
  publicMapSeverityLabelKey,
  publicMapStatusLabelKey,
  publicMapTypeLabelKey,
  type PublicCrisisFilter,
  type PublicMapData
} from "@/lib/publicMap";
import "./public-map.css";

type LayerKey = "incidents" | "shelters" | "aidLocations";
type Detail = { layer: LayerKey; id: string } | null;

const CRISIS_FILTERS: { key: PublicCrisisFilter; icon: string; labelKey: string }[] = [
  { key: "all", icon: "layers", labelKey: "publicMap.typeAll" },
  { key: "flood", icon: "droplet", labelKey: "publicMap.typeFlood" },
  { key: "landslide", icon: "layers", labelKey: "publicMap.typeLandslide" },
  { key: "fire", icon: "alert", labelKey: "publicMap.typeFire" },
  { key: "severe-weather", icon: "info", labelKey: "publicMap.typeSevereWeather" },
  { key: "drought", icon: "info", labelKey: "publicMap.typeDrought" },
  { key: "other-crisis", icon: "alert", labelKey: "publicMap.typeOther" }
];

const LEGEND = [
  { type: "flood", icon: "droplet", labelKey: "map.markerTypes.flood" },
  { type: "landslide", icon: "layers", labelKey: "map.markerTypes.landslide" },
  { type: "fire", icon: "alert", labelKey: "map.markerTypes.fire" },
  { type: "severe-weather", icon: "info", labelKey: "map.markerTypes.severeWeather" },
  { type: "drought", icon: "info", labelKey: "map.markerTypes.drought" },
  { type: "other-crisis", icon: "alert", labelKey: "map.markerTypes.otherCrisis" },
  { type: "public-shelter", icon: "home", labelKey: "map.markerTypes.publicShelter" },
  { type: "aid-centre", icon: "building", labelKey: "map.markerTypes.aidCentre" }
];

export default function PublicLiveMapPage() {
  const { t, locale } = useI18n();
  const [data, setData] = useState<PublicMapData | null>(null);
  const [query, setQuery] = useState("");
  const [crisisFilter, setCrisisFilter] = useState<PublicCrisisFilter>("all");
  const [layers, setLayers] = useState<Record<LayerKey, boolean>>({
    incidents: true,
    shelters: true,
    aidLocations: true
  });
  const [selected, setSelected] = useState<Detail>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [refreshToken, setRefreshToken] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    let active = true;

    if (refreshToken === 0) setLoading(true);
    else setRefreshing(true);

    getPublicMap(controller.signal)
      .then((result) => {
        if (!active) return;
        setData(result);
        setError(null);
      })
      .catch((cause: unknown) => {
        if (!active || controller.signal.aborted) return;
        setError(localizedErrorMessage(cause, t));
      })
      .finally(() => {
        if (!active) return;
        setLoading(false);
        setRefreshing(false);
      });

    return () => {
      active = false;
      controller.abort();
    };
  }, [refreshToken, t]);

  const incidents = useMemo(() => {
    const needle = query.trim().toLocaleLowerCase();
    return (data?.incidents ?? []).filter((incident) => {
      if (crisisFilter !== "all" && incidentMarkerType(incident.type) !== crisisFilter) return false;
      if (!needle) return true;
      return [incident.title, incident.summary, incident.district ?? "", incident.type, incident.severity]
        .join(" ")
        .toLocaleLowerCase()
        .includes(needle);
    });
  }, [crisisFilter, data, query]);

  const shelters = useMemo(() => {
    const needle = query.trim().toLocaleLowerCase();
    return (data?.shelters ?? []).filter((shelter) =>
      !needle || [shelter.name, shelter.services ?? ""].join(" ").toLocaleLowerCase().includes(needle)
    );
  }, [data, query]);

  const aidLocations = useMemo(() => {
    const needle = query.trim().toLocaleLowerCase();
    return (data?.aidLocations ?? []).filter((location) =>
      !needle || [location.name, location.address, location.services ?? ""].join(" ").toLocaleLowerCase().includes(needle)
    );
  }, [data, query]);

  const markers: MapMarker[] = useMemo(() => {
    const result: MapMarker[] = [];
    if (layers.incidents) {
      for (const incident of incidents) {
        const lat = finiteCoordinate(incident.latitude);
        const lng = finiteCoordinate(incident.longitude);
        if (lat === null || lng === null) continue;
        result.push({
          id: `incident:${incident.id}`,
          lat,
          lng,
          label: incident.title,
          type: incidentMarkerType(incident.type),
          description: incident.district || incident.summary
        });
      }
    }
    if (layers.shelters) {
      for (const shelter of shelters) {
        const lat = finiteCoordinate(shelter.latitude);
        const lng = finiteCoordinate(shelter.longitude);
        if (lat === null || lng === null) continue;
        result.push({
          id: `shelter:${shelter.id}`,
          lat,
          lng,
          label: shelter.name,
          type: "public-shelter",
          description: shelter.services || undefined
        });
      }
    }
    if (layers.aidLocations) {
      for (const location of aidLocations) {
        const lat = finiteCoordinate(location.latitude);
        const lng = finiteCoordinate(location.longitude);
        if (lat === null || lng === null) continue;
        result.push({
          id: `aid:${location.id}`,
          lat,
          lng,
          label: location.name,
          type: "aid-centre",
          description: location.address
        });
      }
    }
    return result;
  }, [aidLocations, incidents, layers, shelters]);

  const visibleCount = markers.length;
  const formatDate = useCallback(
    (value?: string | null) =>
      value
        ? new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" }).format(new Date(value))
        : t("publicMap.notProvided"),
    [locale, t]
  );

  function severityLabel(value: string): string {
    const key = publicMapSeverityLabelKey(value);
    return key ? t(key) : value;
  }

  function statusLabel(value: string): string {
    const key = publicMapStatusLabelKey(value);
    return key ? t(key) : value;
  }

  function toggleLayer(layer: LayerKey) {
    setLayers((current) => ({ ...current, [layer]: !current[layer] }));
  }

  function clearFilters() {
    setQuery("");
    setCrisisFilter("all");
    setLayers({ incidents: true, shelters: true, aidLocations: true });
    setSelected(null);
  }

  const selectedIncident = selected?.layer === "incidents"
    ? incidents.find((item) => item.id === selected.id)
    : undefined;
  const selectedShelter = selected?.layer === "shelters"
    ? shelters.find((item) => item.id === selected.id)
    : undefined;
  const selectedAid = selected?.layer === "aidLocations"
    ? aidLocations.find((item) => item.id === selected.id)
    : undefined;

  return (
    <main className="public-info public-live-map">
      <header className="page-hero">
        <div className="page-hero-lines" />
        <div className="eyebrow"><span /> {t("publicMap.eyebrow")}</div>
        <h1>{t("publicMap.title")}</h1>
        <p>{t("publicMap.intro")}</p>
        {data ? (
          <p className="public-live-map__updated">
            <Icon name="clock" size={16} /> {t("publicMap.updatedAt", { value: formatDate(data.updatedAt) })}
          </p>
        ) : null}
      </header>

      <div className="page-body">
        <aside className="public-availability-note public-live-map__privacy" role="note">
          <span><Icon name="shield" size={20} /></span>
          <div>
            <b>{t("publicMap.privacyTitle")}</b>
            <p>{t("publicMap.privacyBody")}</p>
          </div>
        </aside>

        <section className="public-live-map__controls" aria-label={t("publicMap.controlsLabel")}>
          <div className="public-live-map__search">
            <label className="field-label" htmlFor="public-map-search">{t("publicMap.searchLabel")}</label>
            <div className="citizen-assistance__search">
              <Icon name="search" size={18} />
              <input
                id="public-map-search"
                type="search"
                value={query}
                placeholder={t("publicMap.searchPlaceholder")}
                onChange={(event) => setQuery(event.target.value)}
              />
            </div>
          </div>

          <div className="public-live-map__filters">
            <p className="field-label">{t("publicMap.typeLabel")}</p>
            <div className="public-live-map__chips" role="group" aria-label={t("publicMap.typeLabel")}>
              {CRISIS_FILTERS.map((filter) => (
                <button
                  key={filter.key}
                  type="button"
                  className={crisisFilter === filter.key ? "active" : ""}
                  aria-pressed={crisisFilter === filter.key}
                  onClick={() => setCrisisFilter(filter.key)}
                >
                  <Icon name={filter.icon} size={16} />
                  {t(filter.labelKey)}
                </button>
              ))}
            </div>
          </div>

          <div className="public-live-map__layers">
            <p className="field-label">{t("publicMap.layersLabel")}</p>
            <div className="public-live-map__chips" role="group" aria-label={t("publicMap.layersLabel")}>
              {(["incidents", "shelters", "aidLocations"] as const).map((layer) => (
                <button
                  key={layer}
                  type="button"
                  className={layers[layer] ? "active" : ""}
                  aria-pressed={layers[layer]}
                  onClick={() => toggleLayer(layer)}
                >
                  <Icon name={layer === "incidents" ? "alert" : layer === "shelters" ? "home" : "building"} size={16} />
                  {t(`publicMap.layer${layer.charAt(0).toUpperCase()}${layer.slice(1)}`)}
                </button>
              ))}
            </div>
          </div>

          <div className="public-live-map__actions">
            <button
              className="btn btn-outline"
              type="button"
              onClick={() => setRefreshToken((value) => value + 1)}
              disabled={refreshing}
              aria-busy={refreshing}
            >
              <Icon name="clock" size={16} />
              {refreshing ? t("publicMap.refreshing") : t("publicMap.refresh")}
            </button>
            {(query || crisisFilter !== "all" || !layers.incidents || !layers.shelters || !layers.aidLocations) ? (
              <button className="btn btn-outline" type="button" onClick={clearFilters}>
                {t("publicMap.clear")}
              </button>
            ) : null}
          </div>
        </section>

        {error ? (
          <div className="citizen-error-bar" role="alert">
            <Icon name="info" size={18} />
            <span>{error}</span>
            <button
              className="btn btn-outline"
              type="button"
              onClick={() => setRefreshToken((value) => value + 1)}
              disabled={refreshing}
            >
              {t("publicMap.refresh")}
            </button>
          </div>
        ) : null}

        {loading ? (
          <PortalState kind="loading" title={t("publicMap.loadingTitle")}>
            {t("publicMap.loadingBody")}
          </PortalState>
        ) : visibleCount === 0 ? (
          <PortalState kind="empty" title={t("publicMap.emptyTitle")}>
            {t("publicMap.emptyBody")}
          </PortalState>
        ) : (
          <>
            <section className="public-live-map__map" aria-labelledby="public-map-heading">
              <div className="section-head">
                <div>
                  <h2 id="public-map-heading">{t("publicMap.mapTitle")}</h2>
                  <p>{t("publicMap.mapDescription")}</p>
                </div>
                <span className="badge blue"><i aria-hidden="true" /> {t("publicMap.count", { count: visibleCount })}</span>
              </div>
              <MapView
                markers={markers}
                height={440}
                showLegend={false}
                selectedId={selected ? `${selected.layer === "incidents" ? "incident" : selected.layer === "shelters" ? "shelter" : "aid"}:${selected.id}` : null}
                onMarkerSelect={(marker) => {
                  const [kind, id] = marker.id.split(":");
                  const layer = kind === "incident" ? "incidents" : kind === "shelter" ? "shelters" : "aidLocations";
                  setSelected({ layer, id });
                }}
              />
            </section>

            <section className="public-live-map__legend" aria-labelledby="public-map-legend-heading">
              <h2 id="public-map-legend-heading">{t("publicMap.legendTitle")}</h2>
              <ul>
                {LEGEND.map((item) => (
                  <li key={item.type}>
                    <Icon name={item.icon} size={16} />
                    <span>{t(item.labelKey)}</span>
                  </li>
                ))}
              </ul>
            </section>

            {selected ? (
              <section className="public-live-map__detail" aria-labelledby="public-map-detail-heading">
                <div className="section-head">
                  <h2 id="public-map-detail-heading">{t("publicMap.detailsTitle")}</h2>
                  <button className="btn btn-ghost" type="button" onClick={() => setSelected(null)}>
                    <Icon name="close" size={16} /> {t("publicMap.closeDetails")}
                  </button>
                </div>
                {selectedIncident ? (
                  <article>
                    <h3>{selectedIncident.title}</h3>
                    <p>{selectedIncident.summary}</p>
                    <dl>
                      <div><dt>{t("publicMap.typeLabel")}</dt><dd>{t(publicMapTypeLabelKey(selectedIncident.type))}</dd></div>
                      <div><dt>{t("publicMap.severity")}</dt><dd>{severityLabel(selectedIncident.severity)}</dd></div>
                      <div><dt>{t("publicMap.status")}</dt><dd>{statusLabel(selectedIncident.status)}</dd></div>
                      <div><dt>{t("publicMap.district")}</dt><dd>{selectedIncident.district || t("publicMap.notProvided")}</dd></div>
                      <div><dt>{t("publicMap.published")}</dt><dd>{formatDate(selectedIncident.publishedAt)}</dd></div>
                    </dl>
                  </article>
                ) : null}
                {selectedShelter ? (
                  <article>
                    <h3>{selectedShelter.name}</h3>
                    <dl>
                      <div><dt>{t("publicMap.capacity")}</dt><dd>{selectedShelter.capacity}</dd></div>
                      <div><dt>{t("publicMap.occupancy")}</dt><dd>{selectedShelter.occupancy}</dd></div>
                      <div><dt>{t("publicMap.services")}</dt><dd>{selectedShelter.services || t("publicMap.notProvided")}</dd></div>
                    </dl>
                    <Link className="btn btn-outline" href="/find-assistance">
                      {t("publicMap.viewAssistance")} <Icon name="arrow" size={16} />
                    </Link>
                  </article>
                ) : null}
                {selectedAid ? (
                  <article>
                    <h3>{selectedAid.name}</h3>
                    <p>{selectedAid.address}</p>
                    <dl>
                      <div><dt>{t("publicMap.services")}</dt><dd>{selectedAid.services || t("publicMap.notProvided")}</dd></div>
                      <div><dt>{t("publicMap.hours")}</dt><dd>{selectedAid.openingHours || t("publicMap.notProvided")}</dd></div>
                    </dl>
                    <Link className="btn btn-outline" href="/find-assistance">
                      {t("publicMap.viewAssistance")} <Icon name="arrow" size={16} />
                    </Link>
                  </article>
                ) : null}
              </section>
            ) : null}

            <section className="public-live-map__list" aria-labelledby="public-map-list-heading">
              <div className="section-head">
                <h2 id="public-map-list-heading">{t("publicMap.publishedTitle")}</h2>
              </div>
              {incidents.length === 0 ? (
                <PortalState kind="empty" title={t("publicMap.noIncidentsTitle")}>
                  {t("publicMap.noIncidentsBody")}
                </PortalState>
              ) : (
                <div className="public-live-map__cards">
                  {incidents.map((incident) => (
                    <article className="public-live-map__card" key={incident.id}>
                      <span className={`public-live-map__type public-live-map__type--${incidentMarkerType(incident.type)}`} aria-hidden="true">
                        <Icon name="alert" size={18} />
                      </span>
                      <div>
                        <h3>{incident.title}</h3>
                        <p>{incident.summary}</p>
                        <p className="public-live-map__meta">
                          {incident.district || t("publicMap.notProvided")} · {severityLabel(incident.severity)} · {statusLabel(incident.status)}
                        </p>
                      </div>
                      <button
                        className="btn btn-ghost"
                        type="button"
                        onClick={() => setSelected({ layer: "incidents", id: incident.id })}
                      >
                        {t("publicMap.detailsTitle")}
                      </button>
                    </article>
                  ))}
                </div>
              )}
            </section>
          </>
        )}

        <section className="service-choice public-single-choice">
          <div>
            <small>{t("find.ctaSmall")}</small>
            <h2>{t("publicMap.ctaTitle")}</h2>
          </div>
          <Link className="btn btn-primary" href="/report">
            {t("nav.reportCrisis")} <Icon name="arrow" />
          </Link>
        </section>
      </div>
    </main>
  );
}
