"use client";

import { useParams } from "next/navigation";
import { useState, type FormEvent } from "react";
import { casesApi } from "../../../../../components/cases/casesApi";
import { ErrorState } from "../../../../../components/cases/PageState";

export default function RequestFeedbackPage() {
  const params = useParams<{ id: string }>();
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      await casesApi.feedback(params.id, comment);
      setMessage("Thank you. Your feedback was recorded.");
      setComment("");
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Feedback could not be submitted.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <h1>Request feedback</h1>
      {error ? <ErrorState message={error} /> : null}
      {message ? <p role="status">{message}</p> : null}
      <form className="panel" onSubmit={(event) => void submit(event)}>
        <label htmlFor="feedback-comment">Comment</label>
        <textarea
          id="feedback-comment"
          required
          minLength={2}
          maxLength={2000}
          value={comment}
          onChange={(event) => setComment(event.target.value)}
        />
        <button type="submit" disabled={busy}>{busy ? "Sending…" : "Send feedback"}</button>
      </form>
    </div>
  );
}
