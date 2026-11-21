"use client";

import Link from "next/link";
import { useState } from "react";
import { Brand } from "./Brand";
import { Icon } from "./Icon";
import { IntegrationNotice } from "./IntegrationNotice";
import {
  FeatureNotAvailableError,
  crisisModuleOwner,
  submitCrisisReport
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
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [phone, setPhone] = useState("");
  const [email, setEmail] = useState("");
  const [consent, setConsent] = useState(false);
  const [attachment, setAttachment] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [attempted, setAttempted] = useState(false);

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

    if (!firstName.trim() || !lastName.trim() || !phone.trim()) {
      setFormError("Add your name and a mobile number so response teams can reach you.");
      return;
    }

    if (!consent) {
      setFormError("Confirm that the information may be shared with authorized response teams.");
      return;
    }

    setBusy(true);

    try {
      await submitCrisisReport({
        category,
        description,
        location,
        urgency: severity,
        firstName,
        lastName,
        phone,
        email,
        consent
      });
      setAttempted(true);
    } catch (caught) {
      if (caught instanceof FeatureNotAvailableError) {
        setAttempted(true);
      } else {
        setFormError(caught instanceof Error ? caught.message : "Something went wrong.");
      }
    } finally {
      setBusy(false);
    }
  }

  if (attempted) {
    return (
      <div className="form-shell">
        <SimpleHeader />
        <main className="success-page">
          <div className="success-check">
            <Icon name="check" size={42} />
          </div>
          <div className="eyebrow dark">
            <span /> Report preview only
          </div>
          <h1>Your report is ready, but it wasn&apos;t sent.</h1>
          <p>
            Crisis intake is owned by {crisisModuleOwner} and its API is not available in this
            build. Nothing was transmitted — the details you entered stayed in this browser.
          </p>
          <div className="reference-box">
            <span>Submission status</span>
            <strong>Not submitted</strong>
            <button
              type="button"
              onClick={() => {
                setAttempted(false);
                setStep(3);
              }}
            >
              Edit report
            </button>
          </div>
          <div className="next-steps">
            <b>What will happen once intake is connected?</b>
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
            {["What happened", "Location & needs", "Your details"].map((label, index) => (
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
          <IntegrationNotice
            inline
            title="Crisis intake is not connected in this build"
            owner="Dev 2 — crisis & case management"
          >
            You can complete every step and review the design, but the final submit will not store
            a report until the crisis endpoint is available.
          </IntegrationNotice>

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
                  <small>Optional · JPG, PNG or PDF up to 10 MB</small>
                  <small>Attachments upload with the report once intake is connected.</small>
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
                  <h2>How can we reach you?</h2>
                  <p>Contact details help response teams confirm information and share updates.</p>
                </div>
              </div>

              <div className="two-fields">
                <label>
                  <span className="field-label">First name</span>
                  <input
                    value={firstName}
                    placeholder="First name"
                    onChange={(event) => setFirstName(event.target.value)}
                    autoComplete="given-name"
                  />
                </label>
                <label>
                  <span className="field-label">Last name</span>
                  <input
                    value={lastName}
                    placeholder="Last name"
                    onChange={(event) => setLastName(event.target.value)}
                    autoComplete="family-name"
                  />
                </label>
              </div>

              <label>
                <span className="field-label">Mobile number</span>
                <input
                  value={phone}
                  placeholder="+1 555 000 0000"
                  onChange={(event) => setPhone(event.target.value)}
                  autoComplete="tel"
                />
              </label>

              <label>
                <span className="field-label">
                  Email address <small>(optional)</small>
                </span>
                <input
                  type="email"
                  value={email}
                  placeholder="you@example.org"
                  onChange={(event) => setEmail(event.target.value)}
                  autoComplete="email"
                />
              </label>

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
                  <span>Urgency</span>
                  <strong>{severity}</strong>
                </div>
              </div>

              <label className="consent">
                <input
                  type="checkbox"
                  checked={consent}
                  onChange={(event) => setConsent(event.target.checked)}
                />
                <span>
                  I confirm this information is accurate to the best of my knowledge and may be
                  shared with authorized response teams.
                </span>
              </label>

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
                {busy ? "Preparing report…" : "Review submission"} <Icon name="arrow" />
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
