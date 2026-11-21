"use client";

import { useEffect, useRef, useState } from "react";

export default function ReasonDialog({
  title,
  confirmLabel,
  onConfirm,
  onCancel,
  decisionIdRequired = false
}: {
  title: string;
  confirmLabel: string;
  onConfirm: (reason: string, decisionId?: string) => void;
  onCancel: () => void;
  decisionIdRequired?: boolean;
}) {
  const [reason, setReason] = useState("");
  const [decisionId, setDecisionId] = useState("");
  const dialogRef = useRef<HTMLDivElement>(null);
  const onCancelRef = useRef(onCancel);

  useEffect(() => {
    onCancelRef.current = onCancel;
  }, [onCancel]);

  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null;

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault();
        onCancelRef.current();
        return;
      }

      if (event.key !== "Tab" || !dialogRef.current) return;
      const focusable = Array.from(
        dialogRef.current.querySelectorAll<HTMLElement>(
          "button:not([disabled]), textarea:not([disabled]), input:not([disabled])"
        )
      );
      if (focusable.length === 0) return;
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }

    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("keydown", onKeyDown);
      previous?.focus();
    };
  }, []);

  const canConfirm =
    reason.trim().length >= 2 && (!decisionIdRequired || decisionId.trim().length > 0);

  return (
    <div ref={dialogRef} className="panel" role="dialog" aria-modal="true" aria-labelledby="dialog-title">
      <h2 id="dialog-title">{title}</h2>
      <label htmlFor="dialog-reason">Reason</label>
      <textarea
        id="dialog-reason"
        value={reason}
        minLength={2}
        maxLength={1000}
        onChange={(event) => setReason(event.target.value)}
        autoFocus
      />
      {decisionIdRequired ? (
        <>
          <label htmlFor="dialog-decision">Approved CloseCase decision ID</label>
          <input
            id="dialog-decision"
            value={decisionId}
            placeholder="Paste the decision ID from the M3 approval flow"
            onChange={(event) => setDecisionId(event.target.value)}
          />
          <p className="muted">
            Critical close cannot proceed without an approved CloseCase decision from Member 3.
          </p>
        </>
      ) : null}
      <div>
        <button type="button" onClick={() => onConfirm(reason.trim(), decisionId.trim() || undefined)} disabled={!canConfirm}>
          {confirmLabel}
        </button>
        <button type="button" onClick={onCancel}>Cancel</button>
      </div>
    </div>
  );
}
