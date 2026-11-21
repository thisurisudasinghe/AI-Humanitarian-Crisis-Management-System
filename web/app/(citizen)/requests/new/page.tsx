"use client";

import dynamic from "next/dynamic";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import {
  CATEGORIES,
  URGENCIES,
  casesApi,
  type Category,
  type Urgency
} from "../../../../components/cases/casesApi";
import { ErrorState } from "../../../../components/cases/PageState";

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
  const [showMap, setShowMap] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

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
      <form className="panel" onSubmit={(event) => void submit(event)}>
        <label htmlFor="request-category">Service type</label>
        <select id="request-category" value={category} onChange={(event) => setCategory(event.target.value as Category)}>
          {CATEGORIES.map((item) => <option key={item} value={item}>{item}</option>)}
        </select>

        <label htmlFor="request-urgency">Urgency</label>
        <select id="request-urgency" value={urgency} onChange={(event) => setUrgency(event.target.value as Urgency)}>
          {URGENCIES.map((item) => <option key={item} value={item}>{item}</option>)}
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

        <label htmlFor="request-crisis">Crisis ID (optional)</label>
        <input id="request-crisis" value={crisisId} onChange={(event) => setCrisisId(event.target.value)} />

        <label htmlFor="request-area">Area ID (optional)</label>
        <input id="request-area" value={areaId} onChange={(event) => setAreaId(event.target.value)} />

        <button type="submit" disabled={busy}>{busy ? "Submitting…" : "Submit request"}</button>
      </form>
    </div>
  );
}
