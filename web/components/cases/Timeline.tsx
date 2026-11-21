import type { TimelineEntry } from "./casesApi";
import { REQUEST_STATUS_LABELS } from "./statusLabels";
import { EmptyState } from "./PageState";

export default function Timeline({ entries }: { entries: TimelineEntry[] }) {
  if (entries.length === 0) {
    return <EmptyState message="No status changes have been recorded yet." />;
  }

  return (
    <ol aria-label="Request status timeline">
      {entries.map((entry) => (
        <li key={entry.id} className="panel">
          <strong>
            {REQUEST_STATUS_LABELS[entry.fromStatus]} → {REQUEST_STATUS_LABELS[entry.toStatus]}
          </strong>
          <div className="muted">{new Date(entry.createdAt).toLocaleString()}</div>
          {entry.actorDisplayName ? <div>By {entry.actorDisplayName}</div> : null}
          {entry.reason ? <div>{entry.reason}</div> : null}
        </li>
      ))}
    </ol>
  );
}
