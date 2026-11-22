"use client";

import { useParams } from "next/navigation";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import type { RequestDetail } from "../../../../components/cases/casesApi";
import { casesApi } from "../../../../components/cases/casesApi";
import { ErrorState, LoadingState } from "../../../../components/cases/PageState";
import StatusBadge from "../../../../components/cases/StatusBadge";
import AttachmentList from "../../../../components/cases/AttachmentList";
import Timeline from "../../../../components/cases/Timeline";

export default function RequestDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const [request, setRequest] = useState<RequestDetail | null>(null);
  const [unread, setUnread] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState(false);
  const [feedbackMessage, setFeedbackMessage] = useState("");

  const load = useCallback(() => {
    setLoading(true);
    casesApi
      .request(id)
      .then(setRequest)
      .catch((cause: unknown) =>
        setError(cause instanceof Error ? cause.message : "Request could not be loaded.")
      )
      .finally(() => setLoading(false));
    casesApi
      .notifications()
      .then((inbox) => setUnread(inbox.unreadCount))
      .catch(() => setUnread(0));
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  async function submitFeedback(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setFeedbackMessage("");
    setError("");
    try {
      await casesApi.feedback(id, comment);
      setComment("");
      setFeedbackMessage("Feedback submitted.");
      load();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Feedback could not be submitted.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <h1>Request {id.slice(0, 8)}</h1>
      {loading ? <LoadingState /> : null}
      {error ? <ErrorState message={error} /> : null}
      {feedbackMessage ? <p role="status">{feedbackMessage}</p> : null}

      {request ? (
        <>
          <section className="panel" aria-labelledby="summary-heading">
            <h2 id="summary-heading">Summary</h2>
            <p><StatusBadge status={request.status} overdue={request.isOverdue} /></p>
            <p>Type: {request.category} · Urgency: {request.urgency}</p>
            <p>{request.description}</p>
            <p className="muted">Submitted {new Date(request.submittedAt).toLocaleString()}</p>
            <p className="muted">{unread} unread notification(s)</p>
          </section>

          <AttachmentList requestId={id} attachments={request.attachments} canUpload />

          <section className="panel" aria-labelledby="feedback-heading">
            <h2 id="feedback-heading">Feedback</h2>
            {request.status === "Fulfilled" || request.status === "Closed" ? (
              <form onSubmit={(event) => void submitFeedback(event)}>
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
            ) : (
              <p className="muted">
                Feedback becomes available when this request is Fulfilled or Closed.
              </p>
            )}
          </section>

          <section className="panel" aria-labelledby="timeline-heading">
            <h2 id="timeline-heading">Timeline</h2>
            <Timeline entries={request.timeline} />
          </section>
        </>
      ) : null}
    </div>
  );
}
