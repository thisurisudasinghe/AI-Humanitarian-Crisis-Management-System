"use client";

import { useState } from "react";

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

  const canConfirm =
    reason.trim().length >= 2 && (!decisionIdRequired || decisionId.trim().length > 0);

  return (
    <div className="panel" role="dialog" aria-modal="true" aria-labelledby="dialog-title">
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
