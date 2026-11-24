"use client";

import Link from "next/link";
import { useState, type FormEvent } from "react";
import { Brand } from "@/components/safeway/Brand";
import { Icon } from "@/components/safeway/Icon";
import { resendVerificationEmail } from "@/lib/verification";
import {
  resendView,
  type ResendState,
  type VerificationView
} from "@/lib/verification-state";

export function VerificationCard({
  view,
  email = "",
  showResend = false,
  busy = false
}: {
  view: VerificationView;
  email?: string;
  showResend?: boolean;
  busy?: boolean;
}) {
  const [resendEmail, setResendEmail] = useState(email);
  const [resendState, setResendState] = useState<ResendState>("idle");

  async function onResend(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setResendState("loading");
    const result = await resendVerificationEmail(resendEmail.trim());
    setResendState(result.state);
  }

  const iconName =
    view.tone === "success" ? "check" : view.tone === "warning" ? "clock" : "alert";

  return (
    <section className={`verification-card verification-${view.tone}`}>
      <div className="verification-brand">
        <Brand />
      </div>

      <i className="verification-icon" aria-hidden="true">
        <Icon name={iconName} size={34} />
      </i>

      <span className={`badge ${view.tone === "danger" ? "coral" : view.tone === "warning" ? "amber" : "green"}`}>
        <i />
        Email verification
      </span>

      <h1>{busy ? "Checking your link" : view.title}</h1>
      <p className="verification-message">{view.message}</p>

      {busy ? (
        <div className="verification-progress" role="status" aria-live="polite">
          <span className="skeleton-block" />
          <span>Please wait…</span>
        </div>
      ) : null}

      {view.state === "success" || view.state === "already-verified" ? (
        <Link className="btn btn-primary verification-continue" href="/login">
          Continue to login <Icon name="arrow" />
        </Link>
      ) : null}

      {!busy && view.canResend && showResend ? (
        <form className="verification-resend" onSubmit={onResend}>
          <label>
            <span className="field-label">Your Safeway email address</span>
            <input
              type="email"
              value={resendEmail}
              onChange={(event) => setResendEmail(event.target.value)}
              placeholder="you@example.org"
              autoComplete="email"
              required
            />
          </label>
          <button
            className="btn btn-outline"
            type="submit"
            disabled={resendState === "loading"}
          >
            {resendState === "loading" ? "Sending…" : "Send a new link"}
          </button>
          {resendState !== "idle" ? (
            <p className="verification-feedback" role="status">
              {resendView(resendState).message}
            </p>
          ) : null}
        </form>
      ) : null}

      <p className="verification-footnote">
        Blue for Trust. White for Clarity. Green for Hope.
      </p>
    </section>
  );
}
