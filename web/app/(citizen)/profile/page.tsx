"use client";

import { useEffect, useState, type FormEvent } from "react";
import { casesApi, type Household } from "../../../components/cases/casesApi";
import { ErrorState, LoadingState } from "../../../components/cases/PageState";

export default function CitizenProfilePage() {
  const [household, setHousehold] = useState<Household | null>(null);
  const [size, setSize] = useState(1);
  const [accessibilityNeeds, setAccessibilityNeeds] = useState("");
  const [contactPhone, setContactPhone] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    casesApi
      .household()
      .then((result) => {
        setHousehold(result);
        setSize(result.size);
        setAccessibilityNeeds(result.accessibilityNeeds ?? "");
        setContactPhone(result.contactPhone ?? "");
      })
      .catch((cause: unknown) =>
        setError(cause instanceof Error ? cause.message : "Profile could not be loaded.")
      )
      .finally(() => setLoading(false));
  }, []);

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setMessage("");
    setError("");
    try {
      await casesApi.saveHousehold({
        size,
        accessibilityNeeds: accessibilityNeeds || undefined,
        contactPhone: contactPhone || undefined
      });
      setMessage("Profile saved.");
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Profile could not be saved.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <h1>My household</h1>
      {loading ? <LoadingState /> : null}
      {error ? <ErrorState message={error} /> : null}
      {message ? <p role="status">{message}</p> : null}
      {!loading ? (
        <form className="panel" onSubmit={(event) => void save(event)}>
          <label htmlFor="household-size">Household size</label>
          <input
            id="household-size"
            type="number"
            min={1}
            max={100}
            required
            value={size}
            onChange={(event) => setSize(Number(event.target.value))}
          />

          <label htmlFor="household-needs">Accessibility needs</label>
          <textarea
            id="household-needs"
            value={accessibilityNeeds}
            onChange={(event) => setAccessibilityNeeds(event.target.value)}
          />

          <label htmlFor="household-phone">Contact phone</label>
          <input
            id="household-phone"
            type="tel"
            value={contactPhone}
            onChange={(event) => setContactPhone(event.target.value)}
          />

          <button type="submit" disabled={busy}>{busy ? "Saving…" : "Save profile"}</button>
          {household ? <p className="muted">Profile ID: {household.id}</p> : null}
        </form>
      ) : null}
    </div>
  );
}
