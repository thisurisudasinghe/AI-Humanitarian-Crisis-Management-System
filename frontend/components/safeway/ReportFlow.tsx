"use client";

import Link from "next/link";
import { useState } from "react";
import { Brand } from "./Brand";
import { Icon } from "./Icon";
import {
  caseRequestErrorMessage,
  submitCrisisReport,
  type CrisisReportReceipt
} from "@/lib/crisis";

const categories = [
  ["Flooding", "map"],
  ["Fire", "alert"],
  ["Medical emergency", "heart"],
  ["Displacement", "users"],
  ["Food or water", "box"],
  ["Other", "grid"]
] as const;

const severities = ["Needs attention", "Urgent", "Immediate danger"] as const;

export function ReportFlow() {
  const [step, setStep] = useState(1);
  const [category, setCategory] = useState<string>("Flooding");
  const [severity, setSeverity] = useState<string>("Urgent");
  const [description, setDescription] = useState("");
  const [location, setLocation] = useState("");
  const [lat, setLat] = useState("");
  const [lng, setLng] = useState("");
  const [attachment, setAttachment] = useState<string | null>(null);
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

    setStep(next);
  }

  async function onSubmit() {
    setFormError(null);

    const parsedLat = lat.trim() ? Number(lat) : undefined;
    const parsedLng = lng.trim() ? Number(lng) : undefined;
    if (
      (parsedLat !== undefined && !Number.isFinite(parsedLat)) ||
      (parsedLng !== undefined && !Number.isFinite(parsedLng)) ||
      (parsedLat === undefined) !== (parsedLng === undefined)
    ) {
      setFormError("Enter both latitude and longitude as numbers, or leave both blank.");
      return;
    }

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
      setReceipt(submitted);
    } catch (caught) {
      setFormError(caseRequestErrorMessage(caught, "Your report could not be submitted."));
    } finally {
      setBusy(false);
    }
  }

  if (receipt) {
    return (
      <div className="form-shell">
        <SimpleHeader />
        <main className="success-page">
          <div className="success-check">
            <Icon name="check" size={42} />
          </div>
          <div className="eyebrow dark">
            <span /> Report submitted
          </div>
          <h1>Your report has been received.</h1>
          <p>
            The report was submitted to the crisis and case service and is linked to your
            signed-in account. You can follow its status from your reports page.
          </p>
          <div className="reference-box">
            <span>Reference</span>
            <strong>{receipt.reference}</strong>
          </div>
          <p className="muted">
            Submitted reports are not edited here. Create a separate update through your reports page
            instead of submitting a duplicate.
          </p>
          <div className="next-steps">
            <b>What happens next?</b>
            <div>
              <span>1</span>
              <p>
                <strong>Review</strong>Your report is checked and prioritized.
              </p>
            </div>
            <div>
              <span>2</span>
              <p>
                <strong>Response</strong>A response team coordinates the next action.
              </p>
            </div>
            <div>
              <span>3</span>
              <p>
                <strong>Updates</strong>You can follow progress from your dashboard.
              </p>
            </div>
          </div>
          <Link className="btn btn-primary" href="/dashboard">
            View my reports <Icon name="arrow" />
          </Link>
          <Link className="text-btn" href="/">
            Return to homepage
          </Link>
        </main>
      </div>
    );
  }

  return (
    <div className="form-shell">
      <SimpleHeader />
      <main className="report-layout">
        <aside className="form-aside">
          <button
            className="back-link"
            type="button"
            onClick={() => (step > 1 ? setStep(step - 1) : undefined)}
          >
            ← Back
          </button>
          <div className="eyebrow">
            <span /> Crisis report
          </div>
          <h1>Help us understand what&apos;s happening.</h1>
          <p>Share only what you safely can. You can review everything before submitting.</p>
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
          <div className="mobile-progress">
            Step {step} of 3{" "}
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
                This free-text location is saved with the request. Add coordinates below if you have them.
              </p>

              <div className="two-fields">
                <label>
                  <span className="field-label">Latitude (optional)</span>
                  <input
                    inputMode="decimal"
                    value={lat}
                    placeholder="7.2906"
                    onChange={(event) => setLat(event.target.value)}
                  />
                </label>
                <label>
                  <span className="field-label">Longitude (optional)</span>
                  <input
                    inputMode="decimal"
                    value={lng}
                    placeholder="80.6337"
                    onChange={(event) => setLng(event.target.value)}
                  />
                </label>
              </div>

              <div className="map-placeholder">
                <div className="map-roads" />
                <span className="map-pin">
                  <Icon name="pin" />
                </span>
                <div className="map-overlay">
                  Map preview only
                  <small>Geolocation and map tiles are not connected in this build.</small>
                </div>
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
                  <b>{attachment ?? "Add photos or documents"}</b>
                  <small>Optional · JPG, PNG or PDF up to 5 MB</small>
                  <small>File selection is a preview only; this form does not upload evidence.</small>
                </span>
                <input
                  type="file"
                  accept=".jpg,.jpeg,.png,.pdf"
                  onChange={(event) => setAttachment(event.target.files?.[0]?.name ?? null)}
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

function SimpleHeader() {
  return (
    <header className="simple-header">
      <Brand />
      <span>Need assistance?</span>
      <Link href="/find-assistance">Find help</Link>
    </header>
  );
}
