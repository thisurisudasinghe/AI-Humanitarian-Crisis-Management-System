"use client";

import dynamic from "next/dynamic";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState, type ChangeEvent, type FormEvent } from "react";
import {
  CATEGORIES,
  URGENCIES,
  casesApi,
  type Area,
  type Category,
  type Crisis,
  type Urgency
} from "../../../../components/cases/casesApi";
import { ErrorState } from "../../../../components/cases/PageState";
import { CATEGORY_LABELS, URGENCY_LABELS } from "../../../../components/cases/statusLabels";

const ALLOWED_TYPES = ["image/jpeg", "image/png", "application/pdf"];
const MAX_BYTES = 5 * 1024 * 1024;

const MapPicker = dynamic(() => import("../../../../components/map/MapPicker"), {
  ssr: false,
  loading: () => <p className="muted">Loading map…</p>
});

export default function NewRequestPage() {
  const router = useRouter();
  const [category, setCategory] = useState<Category>("Food");
  const [urgency, setUrgency] = useState<Urgency>("Medium");
  const [description, setDescription] = useState("");
  const [lat, setLat] = useState("7.2906");
  const [lng, setLng] = useState("80.6337");
  const [address, setAddress] = useState("");
  const [householdSize, setHouseholdSize] = useState(1);
  const [accessibilityNeeds, setAccessibilityNeeds] = useState("");
  const [crisisId, setCrisisId] = useState("");
  const [areaId, setAreaId] = useState("");
  const [crises, setCrises] = useState<Crisis[]>([]);
  const [areas, setAreas] = useState<Area[]>([]);
  const [evidence, setEvidence] = useState<File | null>(null);
  const [showMap, setShowMap] = useState(false);
  const [busy, setBusy] = useState(false);
  const [createdId, setCreatedId] = useState("");
  const [error, setError] = useState("");
  const [referenceError, setReferenceError] = useState("");

  useEffect(() => {
    casesApi
      .crises()
      .then(setCrises)
      .catch(() => setReferenceError("Crisis reference data could not be loaded. Plain coordinates can still be used."));
  }, []);

  useEffect(() => {
    casesApi
      .areas(crisisId || undefined)
      .then(setAreas)
      .catch(() => setReferenceError("Area reference data could not be loaded. Plain coordinates can still be used."));
  }, [crisisId]);

  function chooseEvidence(event: ChangeEvent<HTMLInputElement>) {
    const selected = event.target.files?.[0] ?? null;
    if (!selected) {
      setEvidence(null);
      return;
    }
    if (!ALLOWED_TYPES.includes(selected.type)) {
      setEvidence(null);
      setError("Use a JPG, PNG or PDF evidence file.");
      event.target.value = "";
      return;
    }
    if (selected.size > MAX_BYTES) {
      setEvidence(null);
      setError("Evidence files must be 5 MB or smaller.");
      event.target.value = "";
      return;
    }
    setError("");
    setEvidence(selected);
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const result = await casesApi.submitRequest({
        category,
        urgency,
        description,
        lat: Number(lat),
        lng: Number(lng),
        address: address || undefined,
        householdSize,
        accessibilityNeeds: accessibilityNeeds || undefined,
        crisisId: crisisId || undefined,
        areaId: areaId || undefined
      });
      setCreatedId(result.id);
      if (evidence) {
        try {
          await casesApi.uploadAttachment(result.id, evidence);
        } catch (cause) {
          setError(
            `Request ${result.id.slice(0, 8)} was submitted, but evidence was not uploaded: ` +
              (cause instanceof Error ? cause.message : "upload failed.")
          );
          setBusy(false);
          return;
        }
      }
      router.push(`/requests/${result.id}`);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "The request could not be submitted.");
      setBusy(false);
    }
  }

  return (
    <div>
      <h1>Request assistance</h1>
      {error ? <ErrorState message={error} /> : null}
      {referenceError ? <ErrorState message={referenceError} /> : null}
      {createdId ? <p><Link href={`/requests/${createdId}`}>Open submitted request</Link></p> : null}
      <form className="panel" onSubmit={(event) => void submit(event)}>
        <label htmlFor="request-category">Service type</label>
        <select id="request-category" value={category} onChange={(event) => setCategory(event.target.value as Category)}>
          {CATEGORIES.map((item) => <option key={item} value={item}>{CATEGORY_LABELS[item]}</option>)}
        </select>

        <label htmlFor="request-urgency">Urgency</label>
        <select id="request-urgency" value={urgency} onChange={(event) => setUrgency(event.target.value as Urgency)}>
          {URGENCIES.map((item) => <option key={item} value={item}>{URGENCY_LABELS[item]}</option>)}
        </select>

        <label htmlFor="request-description">Description</label>
        <textarea
          id="request-description"
          required
          minLength={5}
          maxLength={2000}
          value={description}
          onChange={(event) => setDescription(event.target.value)}
        />

        <label htmlFor="request-lat">Latitude</label>
        <input
          id="request-lat"
          type="number"
          step="any"
          min={-90}
          max={90}
          required
          value={lat}
          onChange={(event) => setLat(event.target.value)}
        />

        <label htmlFor="request-lng">Longitude</label>
        <input
          id="request-lng"
          type="number"
          step="any"
          min={-180}
          max={180}
          required
          value={lng}
          onChange={(event) => setLng(event.target.value)}
        />

        <p>
          <button type="button" onClick={() => setShowMap((value) => !value)}>
            {showMap ? "Hide optional map" : "Use optional map"}
          </button>
        </p>
        {showMap ? (
          <MapPicker
            value={{ lat: Number(lat) || 7.2906, lng: Number(lng) || 80.6337 }}
            onChange={(point) => {
              setLat(String(point.lat));
              setLng(String(point.lng));
            }}
          />
        ) : null}
        <p className="muted">Latitude and longitude inputs remain available if the map is unavailable.</p>

        <label htmlFor="request-address">Address (optional)</label>
        <input id="request-address" value={address} onChange={(event) => setAddress(event.target.value)} />
        <p className="muted">Free-text address is saved with the request. Latitude and longitude remain the actionable map location.</p>

        <label htmlFor="request-household">Household size</label>
        <input
          id="request-household"
          type="number"
          min={1}
          max={100}
          required
          value={householdSize}
          onChange={(event) => setHouseholdSize(Number(event.target.value))}
        />

        <label htmlFor="request-needs">Accessibility needs (optional)</label>
        <textarea
          id="request-needs"
          value={accessibilityNeeds}
          onChange={(event) => setAccessibilityNeeds(event.target.value)}
        />

        <label htmlFor="request-crisis">Crisis (optional)</label>
        <select
          id="request-crisis"
          value={crisisId}
          onChange={(event) => {
            setCrisisId(event.target.value);
            setAreaId("");
          }}
        >
          <option value="">No crisis selected</option>
          {crises.map((crisis) => <option key={crisis.id} value={crisis.id}>{crisis.name}</option>)}
        </select>

        <label htmlFor="request-area">Area (optional)</label>
        <select id="request-area" value={areaId} onChange={(event) => setAreaId(event.target.value)}>
          <option value="">No area selected</option>
          {areas.map((area) => <option key={area.id} value={area.id}>{area.name}</option>)}
        </select>

        <label htmlFor="request-evidence">Evidence (optional)</label>
        <input
          id="request-evidence"
          type="file"
          accept=".jpg,.jpeg,.png,.pdf,image/jpeg,image/png,application/pdf"
          onChange={chooseEvidence}
        />
        <p className="muted">JPG, PNG or PDF, max 5 MB. Evidence is uploaded after the request is created.</p>

        <button type="submit" disabled={busy || Boolean(createdId)}>
          {busy ? "Submitting…" : "Submit request"}
        </button>
      </form>
    </div>
  );
}
