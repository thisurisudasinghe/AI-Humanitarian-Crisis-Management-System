"use client";

import { useState } from "react";
import type { AttachmentMetadata } from "./casesApi";
import { casesApi } from "./casesApi";
import { ErrorState } from "./PageState";

export default function AttachmentList({
  requestId,
  attachments,
  canUpload
}: {
  requestId: string;
  attachments: AttachmentMetadata[];
  canUpload: boolean;
}) {
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function upload() {
    if (!file) return;
    setBusy(true);
    setError("");
    try {
      await casesApi.uploadAttachment(requestId, file);
      setFile(null);
      window.location.reload();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Upload failed.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="panel" aria-labelledby="attachments-heading">
      <h2 id="attachments-heading">Evidence</h2>
      {attachments.length === 0 ? <p className="muted">No files attached.</p> : null}
      <ul>
        {attachments.map((item) => (
          <li key={item.id}>
            <button
              type="button"
              onClick={() => {
                void casesApi
                  .downloadAttachment(requestId, item.id, item.originalFileName)
                  .catch((cause: unknown) =>
                    setError(cause instanceof Error ? cause.message : "Download failed.")
                  );
              }}
            >
              {item.originalFileName}
            </button>{" "}
            <span className="muted">{Math.max(1, Math.round(item.sizeBytes / 1024))} KB</span>
          </li>
        ))}
      </ul>
      {canUpload ? (
        <div>
          <label htmlFor="attachment-file">Add evidence (max 5 MB)</label>
          <input
            id="attachment-file"
            type="file"
            onChange={(event) => setFile(event.target.files?.[0] ?? null)}
          />
          <button type="button" onClick={() => void upload()} disabled={!file || busy}>
            {busy ? "Uploading…" : "Upload"}
          </button>
        </div>
      ) : null}
      {error ? <ErrorState message={error} /> : null}
    </section>
  );
}
