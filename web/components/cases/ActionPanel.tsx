"use client";

import { useState } from "react";
import type { RequestDetail } from "./casesApi";
import { casesApi } from "./casesApi";
import ReasonDialog from "./ReasonDialog";
import { ErrorState } from "./PageState";

type DialogAction = "Reject" | "Escalate" | "Close" | "Merge";

export default function ActionPanel({
  request,
  onChanged
}: {
  request: RequestDetail;
  onChanged: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [dialog, setDialog] = useState<DialogAction | null>(null);
  const [assignee, setAssignee] = useState("");
  const [mergeTarget, setMergeTarget] = useState("");

  async function run(task: () => Promise<void>) {
    setBusy(true);
    setError("");
    try {
      await task();
      onChanged();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "The action failed.");
    } finally {
      setBusy(false);
    }
  }

  function transition(to: string, reason?: string, decisionId?: string) {
    return run(() => casesApi.transition(request.id, to, reason, decisionId));
  }

  function confirm(reason: string, decisionId?: string) {
    const action = dialog;
    setDialog(null);
    if (!action) return;

    if (action === "Merge") {
      if (!mergeTarget.trim()) {
        setError("A target request ID is required.");
        return;
      }
      void run(() => casesApi.merge(request.id, mergeTarget.trim(), reason));
      return;
    }

    const needsDecision = action === "Close" && request.urgency === "Critical";
    if (needsDecision && !decisionId) {
      setError("An approved CloseCase decision ID is required for a Critical request.");
      return;
    }
    void transition(action, reason, needsDecision ? decisionId : undefined);
  }

  return (
    <section className="panel" aria-labelledby="actions-heading">
      <h2 id="actions-heading">Actions</h2>
      {busy ? <p className="muted" role="status">Working…</p> : null}
      {error ? <ErrorState message={error} /> : null}

      <div>
        <button type="button" disabled={busy} onClick={() => void transition("Verified")}>Verify</button>
        <button type="button" disabled={busy} onClick={() => void transition("Triaged")}>Triage</button>
        <button type="button" disabled={busy} onClick={() => void transition("Reopened")}>Reopen</button>
        <button type="button" disabled={busy} onClick={() => void transition("OnHold")}>Hold</button>
        <button type="button" disabled={busy} onClick={() => setDialog("Reject")}>Reject…</button>
        <button type="button" disabled={busy} onClick={() => setDialog("Escalate")}>Escalate…</button>
        <button type="button" disabled={busy} onClick={() => setDialog("Close")}>Close…</button>
      </div>

      {request.urgency === "Critical" ? (
        <p className="muted">
          Critical close requires an approved CloseCase decision ID from the Member 3 flow.
        </p>
      ) : null}

      <div>
        <label htmlFor="assignee">Assign to user ID</label>
        <input id="assignee" value={assignee} onChange={(event) => setAssignee(event.target.value)} />
        <button
          type="button"
          disabled={busy || !assignee.trim()}
          onClick={() => void run(() => casesApi.assign(request.id, assignee.trim()))}
        >
          Assign
        </button>
      </div>

      <div>
        <label htmlFor="merge-target">Merge into request ID</label>
        <input id="merge-target" value={mergeTarget} onChange={(event) => setMergeTarget(event.target.value)} />
        <button type="button" disabled={busy || !mergeTarget.trim()} onClick={() => setDialog("Merge")}>
          Merge…
        </button>
      </div>

      {dialog ? (
        <ReasonDialog
          title={`${dialog} request`}
          confirmLabel={dialog}
          decisionIdRequired={dialog === "Close" && request.urgency === "Critical"}
          onConfirm={confirm}
          onCancel={() => setDialog(null)}
        />
      ) : null}
    </section>
  );
}
