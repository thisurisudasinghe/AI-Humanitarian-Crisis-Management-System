"use client";

import Link from "next/link";
import dynamic from "next/dynamic";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { Icon } from "@/components/safeway/Icon";
import { apiJson } from "@/lib/api";
import { useI18n, useT } from "@/lib/i18n";
import { formatDate } from "@/lib/i18n/core";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import type { MapPoint } from "@/components/map/MapPicker";

const MapPicker = dynamic(() => import("@/components/map/MapPicker"), { ssr: false });

const TRACK_TOKEN_KEY = "safeway.guestTrackingToken";

const INCIDENT_TYPES = [
  { value: "Flood", icon: "droplet", labelKey: "pw.guestReport.categoryFlood" },
  { value: "Fire", icon: "alert", labelKey: "pw.guestReport.categoryFire" },
  { value: "Medical", icon: "medical", labelKey: "pw.guestReport.categoryMedical" },
  { value: "Shelter", icon: "home", labelKey: "pw.guestReport.categoryShelter" },
  { value: "Food", icon: "box", labelKey: "pw.guestReport.categoryFood" },
  { value: "Water", icon: "droplet", labelKey: "pw.guestReport.categoryWater" },
  { value: "Other", icon: "help-circle", labelKey: "pw.guestReport.categoryOther" }
] as const;

interface GuestReportReceipt {
  reference: string;
  trackingToken: string;
  status: string;
  submittedAt: string;
}

function createClientToken(): string {
  if (typeof crypto === "undefined") return "";

  if (typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }

  if (typeof crypto.getRandomValues === "function") {
    const bytes = new Uint8Array(16);
    crypto.getRandomValues(bytes);
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    const hex = Array.from(bytes, (value) => value.toString(16).padStart(2, "0")).join("");
    return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
  }

  return "";
}

function guestStatusLabel(t: (key: string) => string, status: string): string {
  const key = `pw.guestReport.status.${status}`;
  const label = t(key);
  return label === key ? t("pw.common.statusUnknown") : label;
}

export default function GuestReportPage() {
  const t = useT();
  const { locale } = useI18n();
  const [incidentType, setIncidentType] = useState<string>("Flood");
  const [location, setLocation] = useState("");
  const [description, setDescription] = useState("");
  const [contactInfo, setContactInfo] = useState("");
  const [latitude, setLatitude] = useState("");
  const [longitude, setLongitude] = useState("");
  const [clientToken, setClientToken] = useState("");
  const [locationMessage, setLocationMessage] = useState<string | null>(null);
  const [locating, setLocating] = useState(false);
  const [showMap, setShowMap] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [receipt, setReceipt] = useState<GuestReportReceipt | null>(null);
  const [copied, setCopied] = useState<"reference" | "token" | null>(null);
  const submittingRef = useRef(false);

  useEffect(() => {
    // Generated after hydration so server and client markup always match. The
    // same token is reused for every retry of this report.
    setClientToken(createClientToken());
  }, []);

  function useMyLocation() {
    setLocationMessage(null);
    if (!navigator.geolocation) {
      setLocationMessage(t("pw.guestReport.locationUnavailable"));
      return;
    }
    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      (position) => {
        setLatitude(position.coords.latitude.toFixed(6));
        setLongitude(position.coords.longitude.toFixed(6));
        if (!location.trim()) {
          setLocation(`${position.coords.latitude.toFixed(6)}, ${position.coords.longitude.toFixed(6)}`);
        }
        setLocating(false);
      },
      () => {
        setLocationMessage(t("pw.guestReport.locationDenied"));
        setLocating(false);
      },
      { enableHighAccuracy: true, timeout: 10_000, maximumAge: 60_000 }
    );
  }

  function onMapChange(point: MapPoint) {
    setLatitude(Number(point.lat.toFixed(6)).toString());
    setLongitude(Number(point.lng.toFixed(6)).toString());
    if (!location.trim() && point.address) setLocation(point.address);
  }

  async function copyValue(value: string, field: "reference" | "token") {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(field);
    } catch {
      setCopied(null);
      setFormError(t("pw.guestReport.copyFailed"));
    }
  }

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submittingRef.current) return;
    setFormError(null);
    setCopied(null);

    if (!clientToken) {
      setFormError(t("pw.guestReport.tokenNotReady"));
      return;
    }
    if (!location.trim()) {
      setFormError(t("pw.guestReport.locationRequired"));
      return;
    }
    if (description.trim().length < 10) {
      setFormError(t("pw.guestReport.descriptionRequired"));
      return;
    }

    const hasLat = latitude.trim().length > 0;
    const hasLng = longitude.trim().length > 0;
    let parsedLatitude: number | null = null;
    let parsedLongitude: number | null = null;

    if (hasLat || hasLng) {
      const latValue = Number(latitude);
      const lngValue = Number(longitude);
      if (
        !hasLat ||
        !hasLng ||
        !Number.isFinite(latValue) ||
        !Number.isFinite(lngValue) ||
        latValue < -90 ||
        latValue > 90 ||
        lngValue < -180 ||
        lngValue > 180
      ) {
        setFormError(t("pw.guestReport.coordinatesInvalid"));
        return;
      }
      parsedLatitude = latValue;
      parsedLongitude = lngValue;
    }

    submittingRef.current = true;
    setBusy(true);

    try {
      const created = await apiJson<GuestReportReceipt>("/api/v1/public/guest-reports", "POST", {
        incidentType,
        location: location.trim(),
        latitude: parsedLatitude,
        longitude: parsedLongitude,
        description: description.trim(),
        contactInfo: contactInfo.trim() || null,
        clientToken
      });
      setReceipt(created);
      try {
        window.sessionStorage.setItem(TRACK_TOKEN_KEY, created.trackingToken);
      } catch {
        // Tracking still works when the token is copied from the confirmation.
      }
    } catch (cause) {
      setFormError(localizedErrorMessage(cause, t));
    } finally {
      submittingRef.current = false;
      setBusy(false);
    }
  }

  if (receipt) {
    return (
      <main className="public-info pw-page">
        <header className="page-hero">
          <div className="page-hero-lines" />
          <div className="eyebrow">
            <span /> {t("pw.guestReport.eyebrow")}
          </div>
          <h1>{t("pw.guestReport.successTitle")}</h1>
          <p>{t("pw.guestReport.successBody")}</p>
        </header>

        <div className="page-body">
          <article className="pw-card pw-card--green pw-confirmation">
            <span className="pw-card__icon" aria-hidden="true">
              <Icon name="checkCircle" size={26} />
            </span>
            <h2>{t("pw.guestReport.keepDetails")}</h2>
            <dl className="pw-facts">
              <div>
                <dt>{t("pw.common.reference")}</dt>
                <dd className="pw-facts__reference">{receipt.reference}</dd>
              </div>
              <div>
                <dt>{t("pw.guestReport.trackingToken")}</dt>
                <dd className="pw-facts__reference">{receipt.trackingToken}</dd>
              </div>
              <div>
                <dt>{t("pw.common.status")}</dt>
                <dd>{guestStatusLabel(t, receipt.status)}</dd>
              </div>
              <div>
                <dt>{t("pw.guestReport.submitted")}</dt>
                <dd>{formatDate(receipt.submittedAt, locale)}</dd>
              </div>
            </dl>
            <div className="pw-card__links pw-card__links--row">
              <button className="btn btn-outline" type="button" onClick={() => void copyValue(receipt.reference, "reference")}>
                <Icon name="clipboard" size={16} />
                {copied === "reference" ? t("pw.common.copied") : t("pw.common.copyReference")}
              </button>
              <button className="btn btn-outline" type="button" onClick={() => void copyValue(receipt.trackingToken, "token")}>
                <Icon name="clipboard" size={16} />
                {copied === "token" ? t("pw.common.copied") : t("pw.common.copyToken")}
              </button>
              <Link className="btn btn-primary" href="/track-report">
                {t("pw.guestReport.trackLink")} <Icon name="arrow" size={16} />
              </Link>
            </div>
            {formError ? <p className="pw-form-error" role="alert">{formError}</p> : null}
          </article>

          <aside className="pw-callout pw-callout--coral" role="note">
            <span className="pw-callout__icon" aria-hidden="true">
              <Icon name="alert" size={20} />
            </span>
            <div>
              <b>{t("pw.guestReport.limitationTitle")}</b>
              <p>{t("pw.guestReport.limitation")}</p>
            </div>
          </aside>
        </div>
      </main>
    );
  }

  return (
    <main className="public-info pw-page">
      <header className="page-hero">
        <div className="page-hero-lines" />
        <div className="eyebrow">
          <span /> {t("pw.guestReport.eyebrow")}
        </div>
        <h1>{t("pw.guestReport.title")}</h1>
        <p>{t("pw.guestReport.intro")}</p>
      </header>

      <div className="page-body">
        <aside className="pw-callout pw-callout--coral" role="note">
          <span className="pw-callout__icon" aria-hidden="true">
            <Icon name="alert" size={22} />
          </span>
          <div>
            <b>{t("pw.guestReport.limitationTitle")}</b>
            <p>{t("pw.guestReport.limitation")}</p>
            <Link href="/emergency-services">{t("pw.guestReport.emergencyLink")} <Icon name="arrow" size={14} /></Link>
          </div>
        </aside>

        <form className="pw-card pw-form" onSubmit={(event) => void onSubmit(event)} noValidate>
          <h2>{t("pw.guestReport.formTitle")}</h2>
          <p className="pw-form__intro">{t("pw.guestReport.formIntro")}</p>

          <fieldset className="pw-fieldset">
            <legend>{t("pw.guestReport.incidentLabel")}</legend>
            <div className="pw-incident-grid">
              {INCIDENT_TYPES.map((item) => (
                <label className={`pw-incident${incidentType === item.value ? " is-selected" : ""}`} key={item.value}>
                  <input
                    type="radio"
                    name="incidentType"
                    value={item.value}
                    checked={incidentType === item.value}
                    onChange={() => setIncidentType(item.value)}
                  />
                  <span className="pw-incident__icon" aria-hidden="true"><Icon name={item.icon} size={22} /></span>
                  <span>{t(item.labelKey)}</span>
                </label>
              ))}
            </div>
          </fieldset>

          <label className="pw-field">
            <span className="pw-field__label">{t("pw.guestReport.locationLabel")}</span>
            <input
              type="text"
              value={location}
              onChange={(event) => setLocation(event.target.value)}
              placeholder={t("pw.guestReport.locationPlaceholder")}
              autoComplete="off"
              required
            />
          </label>

          <div className="pw-inline-actions">
            <button className="btn btn-outline" type="button" onClick={useMyLocation} disabled={locating}>
              <Icon name="pin" size={16} />
              {locating ? t("pw.guestReport.locating") : t("pw.guestReport.useMyLocation")}
            </button>
            <button
              className="btn btn-outline"
              type="button"
              aria-expanded={showMap}
              onClick={() => setShowMap((value) => !value)}
            >
              <Icon name="map" size={16} />
              {showMap ? t("pw.guestReport.hideMap") : t("pw.guestReport.chooseOnMap")}
            </button>
            <button className="btn btn-outline" type="button" onClick={() => { setLatitude(""); setLongitude(""); }}>
              {t("pw.guestReport.clearCoordinates")}
            </button>
          </div>

          {locationMessage ? <p className="pw-form-hint" role="status">{locationMessage}</p> : null}

          {showMap ? (
            <div className="pw-map-panel">
              <MapPicker value={latitude && longitude ? { lat: Number(latitude), lng: Number(longitude), address: location } : undefined} onChange={onMapChange} height={280} />
            </div>
          ) : null}

          <label className="pw-field">
            <span className="pw-field__label">{t("pw.guestReport.descriptionLabel")}</span>
            <textarea
              rows={4}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              placeholder={t("pw.guestReport.descriptionPlaceholder")}
              required
            />
            <small>{t("pw.guestReport.descriptionHint")}</small>
          </label>

          <label className="pw-field">
            <span className="pw-field__label">{t("pw.guestReport.contactLabel")}</span>
            <input
              type="text"
              value={contactInfo}
              onChange={(event) => setContactInfo(event.target.value)}
              placeholder={t("pw.guestReport.contactPlaceholder")}
              autoComplete="off"
            />
            <small>{t("pw.guestReport.contactHint")}</small>
          </label>

          {formError ? <p className="pw-form-error" role="alert">{formError}</p> : null}

          <button className="btn btn-primary pw-submit" type="submit" disabled={busy || !clientToken}>
            {busy ? t("pw.guestReport.submitting") : t("pw.guestReport.submit")}
            <Icon name="arrow" size={16} />
          </button>
          <p className="pw-form__footnote">{t("pw.guestReport.privacyNote")}</p>
        </form>
      </div>
    </main>
  );
}
