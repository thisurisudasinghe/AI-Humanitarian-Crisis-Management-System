"use client";

import Link from "next/link";
import dynamic from "next/dynamic";
import { useState } from "react";
import { useT } from "@/lib/i18n";
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
  ["Food", "box"],
  ["Medicine", "heart"],
  ["Shelter", "home"],
  ["Transport", "map"],
  ["Accessibility", "users"],
  ["Other", "grid"]
] as const;

const severities = ["Needs attention", "Urgent", "Immediate danger"] as const;

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

  function goToStep(next: number) {
    setFormError(null);

    if (next > 1 && description.trim().length < 10) {
      setFormError("Add a short description of the situation before continuing.");
      return;
    }

    if (next > 2 && location.trim().length < 3) {
      setFormError("Add a location so response teams know where help is needed.");
      return;
    }

    if (next > 2 && !coordinatesAreValid(lat, lng)) {
      setFormError("Enter valid latitude (-90 to 90) and longitude (-180 to 180) before continuing.");
      return;
    }

    setStep(next);
  }

  async function onSubmit() {
    setFormError(null);

    if (!coordinatesAreValid(lat, lng)) {
      setFormError("Enter valid latitude (-90 to 90) and longitude (-180 to 180) before submitting.");
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
          evidenceError: caseRequestErrorMessage(caught, "Evidence could not be uploaded.")
        });
      }
    } catch (caught) {
      setFormError(caseRequestErrorMessage(caught, "Your report could not be submitted."));
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
              <span /> Crisis report
            </div>
            <h1>Help us understand what&apos;s happening.</h1>
            <p>Share only what you safely can. You can review everything before submitting.</p>
          </div>
          <div className="step-list">
            {["What happened", "Location & needs", "Review"].map((label, index) => (
              <div
                className={`${step === index + 1 ? "active" : ""} ${step > index + 1 ? "done" : ""}`}
                key={label}
              >
                <span>{step > index + 1 ? <Icon name="check" size={15} /> : index + 1}</span>
                <b>{label}</b>
              </div>
            ))}
          </div>
          <div className="safety-note">
            <Icon name="phone" />
            <p>
              <b>In immediate danger?</b>Contact your local emergency services first.
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
                  <h2>What happened?</h2>
                  <p>Choose the option that best describes the situation.</p>
                </div>
              </div>

              <label className="field-label">Crisis category</label>
              <div className="category-grid">
                {categories.map(([item, icon]) => (
                  <button
                    className={category === item ? "selected" : ""}
                    type="button"
                    onClick={() => setCategory(item)}
                    key={item}
                  >
                    <Icon name={icon} />
                    <span>{item}</span>
                    {category === item ? <Icon name="check" size={16} /> : null}
                  </button>
                ))}
              </div>

              <label className="field-label" htmlFor="description">
                Briefly describe the situation
              </label>
              <textarea
                id="description"
                rows={5}
                maxLength={1000}
                value={description}
                onChange={(event) => setDescription(event.target.value)}
                placeholder="Tell us what you can see, who may need help, and any immediate risks..."
              />
              <div className="char-count">{description.length} / 1,000</div>
              <button className="btn btn-primary form-next" type="button" onClick={() => goToStep(2)}>
                Continue <Icon name="arrow" />
              </button>
            </>
          ) : null}

          {step === 2 ? (
            <>
              <div className="form-heading">
                <span>02</span>
                <div>
                  <h2>Where is help needed?</h2>
                  <p>Add a location and tell us how urgent the situation feels.</p>
                </div>
              </div>

              <label className="field-label" htmlFor="location">
                Location
              </label>
              <div className="input-icon">
                <Icon name="pin" />
                <input
                  id="location"
                  value={location}
                  placeholder="Street, neighbourhood or landmark"
                  onChange={(event) => setLocation(event.target.value)}
                />
              </div>
              <p className="muted">
                This free-text location is saved with the request. Both coordinates are required so response teams can locate it.
              </p>

              <div className="two-fields">
                <label>
                  <span className="field-label">Latitude (required)</span>
                  <input
                    inputMode="decimal"
                    value={lat}
                    placeholder="7.2906"
                    onChange={(event) => setLat(event.target.value)}
                  />
                </label>
                <label>
                  <span className="field-label">Longitude (required)</span>
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

              <label className="field-label">How urgent is the situation?</label>
              <div className="severity-row">
                {severities.map((item) => (
                  <button
                    className={severity === item ? "selected" : ""}
                    type="button"
                    onClick={() => setSeverity(item)}
                    key={item}
                  >
                    <i />
                    {item}
                  </button>
                ))}
              </div>

              <label className="upload-box">
                <Icon name="upload" />
                <span>
                  <b>{evidenceFile?.name ?? "Add photos or documents"}</b>
                  <small>Optional · JPG, PNG, WebP or PDF up to 1 MB</small>
                  <small>The selected file is uploaded after the report is created.</small>
                </span>
                <input
                  type="file"
                  accept=".jpg,.jpeg,.png,.webp,.pdf"
                  onChange={(event) => setEvidenceFile(event.target.files?.[0] ?? null)}
                />
              </label>

              <button className="btn btn-primary form-next" type="button" onClick={() => goToStep(3)}>
                Continue <Icon name="arrow" />
              </button>
            </>
          ) : null}

          {step === 3 ? (
            <>
              <div className="form-heading">
                <span>03</span>
                <div>
                  <h2>Review and submit</h2>
                  <p>
                    This report is linked to your signed-in account. Contact details and consent are
                    not collected here because the case API does not accept them.
                  </p>
                </div>
              </div>

              <div className="review-card">
                <b>Report summary</b>
                <div>
                  <span>Category</span>
                  <strong>{category}</strong>
                </div>
                <div>
                  <span>Location</span>
                  <strong>{location || "—"}</strong>
                </div>
                <div>
                  <span>Coordinates</span>
                  <strong>{lat && lng ? `${lat}, ${lng}` : "Not provided"}</strong>
                </div>
                <div>
                  <span>Urgency</span>
                  <strong>{severity}</strong>
                </div>
                <div>
                  <span>Evidence</span>
                  <strong>{evidenceFile?.name ?? "None selected"}</strong>
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
                {busy ? "Submitting report…" : "Submit report"} <Icon name="arrow" />
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


