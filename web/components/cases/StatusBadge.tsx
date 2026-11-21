import type { RequestStatus } from "./casesApi";
import { REQUEST_STATUS_LABELS } from "./statusLabels";

export default function StatusBadge({ status, overdue }: { status: RequestStatus; overdue?: boolean }) {
  return (
    <span className="status-badge">
      {REQUEST_STATUS_LABELS[status]}
      {overdue ? " · overdue" : ""}
    </span>
  );
}
