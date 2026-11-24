"use client";

import { useEffect, useState, type FormEvent } from "react";
import { caseRequestErrorMessage, submitRequestFeedback } from "@/lib/crisis";
import { useI18n } from "@/lib/i18n";
import { Icon } from "./Icon";
import { PortalNotice } from "./PortalPrimitives";

function feedbackKey(requestId: string): string {
  return `safeway.feedback.submitted.${requestId}`;
}

export function CitizenFeedbackForm({ requestId }: { requestId: string }) {
  const { t } = useI18n();
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [alreadySent, setAlreadySent] = useState(false);

  useEffect(() => {
    try {
      setAlreadySent(window.localStorage.getItem(feedbackKey(requestId)) === "1");
    } catch {
      setAlreadySent(false);
    }
  }, [requestId]);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setNotice(null);
    setError(null);

    if (comment.trim().length < 2) {
      setError(t("citizenX.feedback.error"));
      return;
    }

    setBusy(true);
    try {
      await submitRequestFeedback(requestId, comment);
      setNotice(t("citizenX.feedback.success"));
      setComment("");
      setAlreadySent(true);
      try {
        window.localStorage.setItem(feedbackKey(requestId), "1");
      } catch {
        // Local storage may be disabled. The form is still disabled for this
        // session, which prevents accidental duplicate submissions here.
      }
    } catch (cause) {
      setError(caseRequestErrorMessage(cause, t("citizenX.feedback.failed"), t));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="citizen-feedback" aria-labelledby="citizen-feedback-heading">
      <div className="citizen-feedback__head">
        <i aria-hidden="true"><Icon name="heartHandshake" size={20} /></i>
        <div>
          <h2 id="citizen-feedback-heading">{t("citizenX.feedback.eligibleTitle")}</h2>
          <p>{t("citizenX.feedback.eligibleBody")}</p>
        </div>
      </div>

      {notice ? <PortalNotice>{notice}</PortalNotice> : null}
      {alreadySent ? (
        <PortalNotice>{t("citizenX.feedback.duplicate")}</PortalNotice>
      ) : (
        <form onSubmit={(event) => void onSubmit(event)} className="citizen-feedback__form">
          <label className="field-label" htmlFor="citizen-feedback-comment">
            {t("citizenX.feedback.label")}
          </label>
          <textarea
            id="citizen-feedback-comment"
            value={comment}
            maxLength={2000}
            placeholder={t("citizenX.feedback.placeholder")}
            onChange={(event) => setComment(event.target.value)}
          />
          {error ? (
            <p className="form-error" role="alert">{error}</p>
          ) : null}
          <button className="btn btn-primary" type="submit" disabled={busy} aria-busy={busy}>
            <Icon name="arrow" size={16} />
            {busy ? t("citizenX.feedback.submitting") : t("citizenX.feedback.submit")}
          </button>
        </form>
      )}
    </section>
  );
}
