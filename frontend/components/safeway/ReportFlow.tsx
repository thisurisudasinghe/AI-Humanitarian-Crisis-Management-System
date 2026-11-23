"use client";

import Link from "next/link";
import dynamic from "next/dynamic";
import { useEffect, useState } from "react";
import { useI18n, useT } from "@/lib/i18n";
import { Brand } from "./Brand";
import { Icon } from "./Icon";
import { LanguageSwitcher } from "@/components/i18n/LanguageSwitcher";
import {
  caseRequestErrorMessage,
  submitCrisisReport,
  uploadCrisisReportEvidence,
  type CrisisReportReceipt
} from "@/lib/crisis";
import type { MapPoint } from "@/components/map/MapPicker";

const MapPicker = dynamic(() => import("@/components/map/MapPicker"), { ssr: false });

const categories = [
  ["Food", "box", "catFood"],
  ["Medicine", "heart", "catMedicine"],
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
  if (!latText.trim() || !lngText.trim()) {
    return false;
  }

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
  const [evidenceFile, setEvidenceFile] = useState<File | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [receipt, setReceipt] = useState<CrisisReportReceipt | null>(null);

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

  async function onSubmit() {
    setFormError(null);

    if (!coordinatesAreValid(lat, lng)) {
      setFormError(t("report.errCoordsSubmit"));
      return;
    }

    const parsedLat = Number(lat);
    const parsedLng = Number(lng);
    setBusy(true);

    try {
      const submitted = await submitCrisisReport({
        category,
        description,
        location,
        urgency: severity,
        lat: parsedLat,
        lng: parsedLng
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
      setFormError(caseRequestErrorMessage(caught, t("report.errSubmitFallback"), t));
    } finally {
      setBusy(false);
    }
  }

  if (receipt) {
    return (
      <div className="form-shell">
        <main className="success-page">
          <div className="success-check">
            <Icon name="check" size={42} />
          </div>
          <div className="eyebrow dark">
            <span /> {t("report.title")}
          </div>
          <h1>{t("report.submittedTitle")}</h1>
          <p>
            {t("report.submittedCopy")}
          </p>
          <div className="reference-box">
            <span>{t("report.reference")}</span>
            <strong>{receipt.reference}</strong>
          </div>
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
          <p className="muted">
            {t("report.noEdit")}
          </p>
          <div className="next-steps">
            <b>{t("report.nextSteps")}</b>
            <div>
              <span>1</span>
              <p>
                <strong>{t("report.stepReview")}</strong> {t("report.stepReviewCopy")}
              </p>
            </div>
            <div>
              <span>2</span>
              <p>
                <strong>{t("report.stepResponse")}</strong> {t("report.stepResponseCopy")}
              </p>
            </div>
            <div>
              <span>3</span>
              <p>
                <strong>{t("report.stepUpdates")}</strong> {t("report.stepUpdatesCopy")}
              </p>
            </div>
          </div>
          <Link className="btn btn-primary" href="/dashboard">
            {t("report.viewMyReports")} <Icon name="arrow" />
          </Link>
          <Link className="text-btn" href="/">
            {t("report.returnHome")}
          </Link>
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
          <div style={{display:"flex",justifyContent:"space-between",gap:12,alignItems:"center",flexWrap:"wrap"}}><Link className="back-link report-back-link" href="/">{t("common.backToHome")}</Link><LanguageSwitcher variant="compact" /></div>
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
              <p className="muted">
                {t("report.locationNote")}
              </p>

              <div className="two-fields">
                <label>
                  <span className="field-label">{t("report.latLabel")}</span>
                  <input
                    inputMode="decimal"
                    value={lat}
                    placeholder="7.2906"
                    onChange={(event) => setLat(event.target.value)}
                  />
                </label>
                <label>
                  <span className="field-label">{t("report.lngLabel")}</span>
                  <input
                    inputMode="decimal"
                    value={lng}
                    placeholder="80.6337"
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
                  accept=".jpg,.jpeg,.png,.webp,.pdf"
                  onChange={(event) => setEvidenceFile(event.target.files?.[0] ?? null)}
                />
              </label>

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


