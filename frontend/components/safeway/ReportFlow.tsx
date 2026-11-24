"use client";

import Link from "next/link";
import dynamic from "next/dynamic";
import { useEffect, useRef, useState } from "react";
import { useI18n, useT } from "@/lib/i18n";
import { statusTone } from "@/lib/citizen-status";
import {
  caseRequestErrorMessage,
  submitCrisisReport,
  uploadCrisisReportEvidence,
  type CrisisReportReceipt
} from "@/lib/crisis";
import { formatDate } from "@/lib/i18n/core";
import type { MapPoint } from "@/components/map/MapPicker";
import { Brand } from "./Brand";
import { Icon } from "./Icon";
import { PortalNotice } from "./PortalPrimitives";

const MapPicker = dynamic(() => import("@/components/map/MapPicker"), { ssr: false });

const SUBMIT_LOCK_KEY = "safeway.report.lastSubmit";
const DUPLICATE_WINDOW_MS = 30_000;

const categories = [
  ["Food", "box", "catFood"],
  ["Medicine", "medical", "catMedicine"],
  ["Shelter", "home", "catShelter"],
  ["Transport", "map", "catTransport"],
  ["Accessibility", "users", "catAccessibility"],
  ["Other", "grid", "catOther"]
] as const;

const severities = [
  ["Needs attention", "sevLow"],
  ["Urgent", "sevHigh"],
  ["Immediate danger", "sevCritical"]
] as const;

function coordinatesAreValid(latText: string, lngText: string): boolean {
  if (!latText.trim() || !lngText.trim()) return false;
  const latValue = Number(latText);
  const lngValue = Number(lngText);
  return (
    Number.isFinite(latValue) &&
    Number.isFinite(lngValue) &&
    latValue >= -90 &&
    latValue <= 90 &&
    lngValue >= -180 &&
    lngValue <= 180
  );
}

function evidenceProblem(file: File, t: (key: string) => string): string | null {
  const extensionAllowed = /\.(jpe?g|png|pdf)$/i.test(file.name);
  const mimeAllowed = ["image/jpeg", "image/png", "application/pdf"].includes(file.type);
  if (!extensionAllowed && !mimeAllowed) return t("citizenX.report.evidenceType");
  if (file.size > 1_000_000) return t("citizenX.report.evidenceTooLarge");
  return null;
}

export function ReportFlow() {
  const t = useT();
  const { locale } = useI18n();
  const [step, setStep] = useState(1);
  const [category, setCategory] = useState<string>("Food");
  const [severity, setSeverity] = useState<string>("Urgent");
  const [description, setDescription] = useState("");
  const [location, setLocation] = useState("");
  const [lat, setLat] = useState("");
  const [lng, setLng] = useState("");
  const [householdSize, setHouseholdSize] = useState("1");
  const [accessibilityNeeds, setAccessibilityNeeds] = useState("");
  const [evidenceFile, setEvidenceFile] = useState<File | null>(null);
  const [evidenceError, setEvidenceError] = useState<string | null>(null);
  const [locationMessage, setLocationMessage] = useState<string | null>(null);
  const [locating, setLocating] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [receipt, setReceipt] = useState<CrisisReportReceipt | null>(null);
  const submittingRef = useRef(false);

  useEffect(() => {
    document.title = `${t("report.title")} — SAFEWAY`;
    document.querySelector('meta[name="description"]')?.setAttribute("content", t("report.subtitle"));
  }, [locale, t]);

  function goToStep(next: number) {
    setFormError(null);

    if (next > 1 && description.trim().length < 10) {
      setFormError(t("report.errDesc"));
      return;
    }

    if (next > 2 && location.trim().length < 3) {
      setFormError(t("report.errLocation"));
      return;
    }

    if (next > 2 && !coordinatesAreValid(lat, lng)) {
      setFormError(t("report.errCoords"));
      return;
    }

    setStep(next);
  }

  function useMyLocation() {
    setLocationMessage(null);
    if (!navigator.geolocation) {
      setLocationMessage(t("citizenX.report.locationUnavailable"));
      return;
    }

    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      (position) => {
        const nextLat = position.coords.latitude.toFixed(6);
        const nextLng = position.coords.longitude.toFixed(6);
        setLat(nextLat);
        setLng(nextLng);
        if (!location.trim()) setLocation(`${nextLat}, ${nextLng}`);
        setLocationMessage(null);
        setLocating(false);
      },
      () => {
        setLocationMessage(t("citizenX.report.locationDenied"));
        setLocating(false);
      },
      { enableHighAccuracy: true, timeout: 10_000, maximumAge: 60_000 }
    );
  }

  async function onSubmit() {
    if (submittingRef.current) return;
    setFormError(null);

    if (!coordinatesAreValid(lat, lng)) {
      setFormError(t("report.errCoordsSubmit"));
      return;
    }

    const size = Number(householdSize);
    if (!Number.isInteger(size) || size < 1 || size > 100) {
      setFormError(t("citizenX.report.affectedError"));
      return;
    }

    const signature = JSON.stringify({
      category,
      severity,
      description: description.trim(),
      location: location.trim(),
      lat,
      lng,
      size,
      accessibilityNeeds: accessibilityNeeds.trim()
    });

    try {
      const raw = window.sessionStorage.getItem(SUBMIT_LOCK_KEY);
      const last = raw ? (JSON.parse(raw) as { signature?: string; at?: number }) : null;
      if (last?.signature === signature && typeof last.at === "number" && Date.now() - last.at < DUPLICATE_WINDOW_MS) {
        setFormError(t("citizenX.report.duplicate"));
        return;
      }
      window.sessionStorage.setItem(SUBMIT_LOCK_KEY, JSON.stringify({ signature, at: Date.now() }));
    } catch {
      // Private browsing may block storage; the in-flight ref still prevents
      // accidental double submission.
    }

    const parsedLat = Number(lat);
    const parsedLng = Number(lng);
    submittingRef.current = true;
    setBusy(true);

    try {
      const submitted = await submitCrisisReport({
        category,
        description,
        location,
        urgency: severity,
        lat: parsedLat,
        lng: parsedLng,
        householdSize: size,
        accessibilityNeeds
      });

      if (!evidenceFile) {
        setReceipt(submitted);
        return;
      }

      try {
        await uploadCrisisReportEvidence(submitted.reference, evidenceFile);
        setReceipt({
          ...submitted,
          evidenceAttached: true,
          evidenceFileName: evidenceFile.name
        });
      } catch (caught) {
        // The request exists, so keep its reference but never claim the
        // evidence was attached. The error is shown on the receipt screen.
        setReceipt({
          ...submitted,
          evidenceAttached: false,
          evidenceFileName: evidenceFile.name,
          evidenceError: caseRequestErrorMessage(caught, t("report.errEvidenceFallback"), t)
        });
      }
    } catch (caught) {
      try {
        window.sessionStorage.removeItem(SUBMIT_LOCK_KEY);
      } catch {
        // Ignore storage failures on the error path.
      }
      setFormError(caseRequestErrorMessage(caught, t("report.errSubmitFallback"), t));
    } finally {
      submittingRef.current = false;
      setBusy(false);
    }
  }

  if (receipt) {
    return (
      <div className="form-shell">
        <main className="success-page citizen-confirmation">
          <div className="success-check">
            <Icon name="checkCircle" size={44} />
          </div>
          <div className="eyebrow dark">
            <span /> {t("report.title")}
          </div>
          <h1>{t("citizenX.report.submittedTitle")}</h1>
          <p>{t("citizenX.report.submittedCopy")}</p>

          <div className="citizen-confirmation__facts">
            <div className="reference-box">
              <span>{t("report.reference")}</span>
              <strong>{receipt.reference}</strong>
            </div>
            <div>
              <span>{t("citizenX.report.submissionTime")}</span>
              <strong>
                {receipt.statusConfirmed
                  ? formatDate(receipt.submittedAt, locale)
                  : t("citizenX.tracking.notProvided")}
              </strong>
            </div>
            <div>
              <span>{t("citizenX.report.currentStatus")}</span>
              <strong className={`badge ${statusTone(receipt.status)}`}>
                <i aria-hidden="true" /> {t(`citizenX.status.${receipt.status}`)}
              </strong>
            </div>
          </div>

          {!receipt.statusConfirmed ? (
            <PortalNotice tone="error">{t("citizenX.report.statusUnconfirmed")}</PortalNotice>
          ) : null}
          {receipt.evidenceAttached ? (
            <p className="muted">
              {t("report.evidenceAttached", { file: receipt.evidenceFileName ?? "uploaded file" })}
            </p>
          ) : null}
          {receipt.evidenceError ? (
            <div className="form-error" role="alert">
              <strong>{t("report.evidenceErrorTitle")}</strong>{" "}{receipt.evidenceError} {t("report.evidenceErrorCopy")}
            </div>
          ) : null}

          <div className="citizen-confirmation__actions">
            <Link className="btn btn-primary" href={`/my-reports/${receipt.reference}`}>
              {t("citizenX.report.trackRequest")} <Icon name="arrow" />
            </Link>
            <Link className="btn btn-outline" href="/portal/citizen">
              <Icon name="arrowLeft" /> {t("citizenX.report.backPortal")}
            </Link>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className="form-shell">
      <main className="report-layout">
        <aside className="form-aside">
          <Brand light />
          <div>
            <div className="eyebrow">
              <span /> {t("report.sidebarEyebrow")}
            </div>
            <h1>{t("report.sidebarTitle")}</h1>
            <p>{t("report.sidebarCopy")}</p>
          </div>
          <div className="step-list">
            {[t("report.step1"), t("report.step2"), t("report.step3")].map((label, index) => (
              <div
                className={`${step === index + 1 ? "active" : ""} ${step > index + 1 ? "done" : ""}`}
                key={index}
              >
                <span>{step > index + 1 ? <Icon name="check" size={15} /> : index + 1}</span>
                <b>{label}</b>
              </div>
            ))}
          </div>
          <div className="safety-note">
            <Icon name="phone" />
            <p>
              <b>{t("report.dangerTitle")}</b>{t("report.dangerCopy")}
            </p>
          </div>
        </aside>

        <section className="form-panel">
          <div className="report-back-row">
            <Link className="back-link report-back-link" href="/portal/citizen">
              <Icon name="arrowLeft" size={16} /> {t("citizenX.report.backPortal")}
            </Link>
          </div>
          <div className="mobile-progress">
            {t("report.stepOf", { step: String(step), total: "3" })}{" "}
            <span>
              <i style={{ width: `${step * 33.3}%` }} />
            </span>
          </div>

          {step === 1 ? (
            <>
              <div className="form-heading">
                <span>01</span>
                <div>
                  <h2>{t("report.step1Heading")}</h2>
                  <p>{t("report.step1Copy")}</p>
                </div>
              </div>

              <label className="field-label">{t("report.categoryLabel")}</label>
              <div className="category-grid">
                {categories.map(([item, icon, labelKey]) => (
                  <button
                    className={category === item ? "selected" : ""}
                    type="button"
                    onClick={() => setCategory(item)}
                    key={item}
                  >
                    <Icon name={icon} />
                    <span>{t(`report.${labelKey}`)}</span>
                    {category === item ? <Icon name="check" size={16} /> : null}
                  </button>
                ))}
              </div>

              <label className="field-label" htmlFor="description">
                {t("report.descLabel")}
              </label>
              <textarea
                id="description"
                rows={5}
                maxLength={1000}
                value={description}
                onChange={(event) => setDescription(event.target.value)}
                placeholder={t("report.descPlaceholder")}
              />
              <div className="char-count">{description.length} / 1,000</div>

              <div className="two-fields">
                <div>
                  <label className="field-label" htmlFor="household-size">{t("citizenX.report.affectedLabel")}</label>
                  <input
                    id="household-size"
                    type="number"
                    min={1}
                    max={100}
                    inputMode="numeric"
                    value={householdSize}
                    onChange={(event) => setHouseholdSize(event.target.value)}
                  />
                  <small className="field-hint">{t("citizenX.report.affectedHint")}</small>
                </div>
                <div>
                  <label className="field-label" htmlFor="accessibility-needs">{t("citizenX.report.accessibilityLabel")}</label>
                  <input
                    id="accessibility-needs"
                    value={accessibilityNeeds}
                    maxLength={2000}
                    placeholder={t("citizenX.report.accessibilityPlaceholder")}
                    onChange={(event) => setAccessibilityNeeds(event.target.value)}
                  />
                </div>
              </div>

              <button className="btn btn-primary form-next" type="button" onClick={() => goToStep(2)}>
                {t("report.continue")} <Icon name="arrow" />
              </button>
            </>
          ) : null}

          {step === 2 ? (
            <>
              <div className="form-heading">
                <span>02</span>
                <div>
                  <h2>{t("report.step2Heading")}</h2>
                  <p>{t("report.step2Copy")}</p>
                </div>
              </div>

              <label className="field-label" htmlFor="location">
                {t("report.locationLabel")}
              </label>
              <div className="input-icon">
                <Icon name="pin" />
                <input
                  id="location"
                  value={location}
                  placeholder={t("report.locationPlaceholder")}
                  onChange={(event) => setLocation(event.target.value)}
                />
              </div>
              <p className="muted">{t("report.locationNote")}</p>

              <button
                className="btn btn-outline citizen-locate-btn"
                type="button"
                onClick={useMyLocation}
                disabled={locating}
                aria-busy={locating}
              >
                <Icon name="pin" size={16} />
                {locating ? t("citizenX.report.locating") : t("citizenX.report.useMyLocation")}
              </button>
              {locationMessage ? (
                <p className="portal-subtle" role="status">{locationMessage}</p>
              ) : null}

              <p className="field-label citizen-manual-label">{t("citizenX.report.manualLocation")}</p>
              <div className="two-fields">
                <label>
                  <span className="field-label">{t("report.latLabel")}</span>
                  <input
                    inputMode="decimal"
                    value={lat}
                    placeholder={t("citizenX.report.latPlaceholder")}
                    onChange={(event) => setLat(event.target.value)}
                  />
                </label>
                <label>
                  <span className="field-label">{t("report.lngLabel")}</span>
                  <input
                    inputMode="decimal"
                    value={lng}
                    placeholder={t("citizenX.report.lngPlaceholder")}
                    onChange={(event) => setLng(event.target.value)}
                  />
                </label>
              </div>

              <div className="dev4-operations report-map-picker">
                <MapPicker
                  height={320}
                  value={
                    coordinatesAreValid(lat, lng)
                      ? {
                          lat: Number(lat),
                          lng: Number(lng),
                          address: location || undefined
                        }
                      : undefined
                  }
                  onChange={(point: MapPoint) => {
                    setLat(String(point.lat));
                    setLng(String(point.lng));
                    if (point.address?.trim()) {
                      setLocation(point.address.trim());
                    }
                  }}
                />
              </div>

              <label className="field-label">{t("report.urgencyLabel")}</label>
              <div className="severity-row">
                {severities.map(([item, labelKey]) => (
                  <button
                    className={severity === item ? "selected" : ""}
                    type="button"
                    onClick={() => setSeverity(item)}
                    key={item}
                  >
                    <i />
                    {t(`report.${labelKey}`)}
                  </button>
                ))}
              </div>

              <label className="upload-box">
                <Icon name="upload" />
                <span>
                  <b>{evidenceFile?.name ?? t("report.evidenceAdd")}</b>
                  <small>{t("report.evidenceHint")}</small>
                  <small>{t("report.evidenceUpload")}</small>
                </span>
                <input
                  type="file"
                  accept=".jpg,.jpeg,.png,.pdf"
                  onChange={(event) => {
                    const file = event.target.files?.[0] ?? null;
                    setEvidenceFile(file);
                    setEvidenceError(file ? evidenceProblem(file, t) : null);
                  }}
                />
              </label>
              {evidenceError ? <p className="form-error" role="alert">{evidenceError}</p> : null}

              <button className="btn btn-primary form-next" type="button" onClick={() => goToStep(3)}>
                {t("report.continue")} <Icon name="arrow" />
              </button>
            </>
          ) : null}

          {step === 3 ? (
            <>
              <div className="form-heading">
                <span>03</span>
                <div>
                  <h2>{t("report.step3Heading")}</h2>
                  <p>{t("report.step3Copy")}</p>
                </div>
              </div>

              <div className="review-card">
                <b>{t("report.summary")}</b>
                <div>
                  <span>{t("report.fieldCategory")}</span>
                  <strong>{t(`report.${categories.find(([v]) => v === category)?.[2] ?? "catOther"}`)}</strong>
                </div>
                <div>
                  <span>{t("report.descLabel")}</span>
                  <strong>{description || "—"}</strong>
                </div>
                <div>
                  <span>{t("report.fieldLocation")}</span>
                  <strong>{location || "—"}</strong>
                </div>
                <div>
                  <span>{t("report.fieldCoordinates")}</span>
                  <strong>{lat && lng ? `${lat}, ${lng}` : t("report.notProvided")}</strong>
                </div>
                <div>
                  <span>{t("report.fieldUrgency")}</span>
                  <strong>{t(`report.${severities.find(([v]) => v === severity)?.[1] ?? "sevHigh"}`)}</strong>
                </div>
                <div>
                  <span>{t("citizenX.report.fieldAffected")}</span>
                  <strong>{householdSize}</strong>
                </div>
                <div>
                  <span>{t("citizenX.report.fieldAccessibility")}</span>
                  <strong>{accessibilityNeeds.trim() || t("report.notProvided")}</strong>
                </div>
                <div>
                  <span>{t("report.fieldEvidence")}</span>
                  <strong>{evidenceFile?.name ?? t("report.noneSelected")}</strong>
                </div>
              </div>

              {formError ? (
                <div className="form-error" role="alert">
                  {formError}
                </div>
              ) : null}

              <button
                className="btn btn-primary form-next"
                type="button"
                onClick={() => void onSubmit()}
                disabled={busy}
                aria-busy={busy}
              >
                {busy ? t("report.submitting") : t("report.submit")} <Icon name="arrow" />
              </button>
            </>
          ) : null}

          {formError && step !== 3 ? (
            <div className="form-error" role="alert">
              {formError}
            </div>
          ) : null}
        </section>
      </main>
    </div>
  );
}
