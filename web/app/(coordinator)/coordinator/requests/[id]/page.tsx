"use client";

import dynamic from "next/dynamic";
import { useParams } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import type { RequestDetail } from "../../../../../components/cases/casesApi";
import { casesApi } from "../../../../../components/cases/casesApi";
import { ErrorState, LoadingState } from "../../../../../components/cases/PageState";
import StatusBadge from "../../../../../components/cases/StatusBadge";
import AttachmentList from "../../../../../components/cases/AttachmentList";
import Timeline from "../../../../../components/cases/Timeline";
import ActionPanel from "../../../../../components/cases/ActionPanel";
import RecommendationPanel from "../../../../../components/cases/RecommendationPanel";
import { CATEGORY_LABELS, URGENCY_LABELS } from "../../../../../components/cases/statusLabels";
import type { MapMarker } from "../../../../../components/map/MapView";

const MapView = dynamic(() => import("../../../../../components/map/MapView"), {
  ssr: false,
  loading: () => <p className="muted">Loading map…</p>
});

export default function CoordinatorRequestDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const [request, setRequest] = useState<RequestDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const load = useCallback(() => {
    setLoading(true);
    casesApi
      .request(id)
      .then(setRequest)
      .catch((cause: unknown) =>
        setError(cause instanceof Error ? cause.message : "Request could not be loaded.")
      )
      .finally(() => setLoading(false));
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  const markers: MapMarker[] = request
    ? [{
        id: request.id,
        name: `${request.category} request ${request.id.slice(0, 8)}`,
        lat: Number(request.lat),
        lng: Number(request.lng),
        description: request.description
      }]
    : [];

  return (
    <div>
      <h1>Request {id.slice(0, 8)}</h1>
      {loading ? <LoadingState /> : null}
      {error ? <ErrorState message={error} /> : null}

      {request ? (
        <>
          <section className="panel" aria-labelledby="detail-heading">
            <h2 id="detail-heading">Full request</h2>
            <p><StatusBadge status={request.status} overdue={request.isOverdue} /></p>
            <p>Type: {CATEGORY_LABELS[request.category]} · Urgency: {URGENCY_LABELS[request.urgency]}</p>
            <p>{request.description}</p>
            <p className="muted">Requester: {request.requesterUserId.slice(0, 8)}… (masked)</p>
            <p className="muted">Household: {request.householdSize} · Submitted {new Date(request.submittedAt).toLocaleString()}</p>
            <p className="muted">SLA age: {request.slaAgeHours} h</p>
            <dl>
              <dt>Assigned to</dt>
              <dd>{request.assignedToUserId ? request.assignedToUserId.slice(0, 8) : "Unassigned"}</dd>
              <dt>Crisis / area</dt>
              <dd>{request.crisisId ?? "None"} / {request.areaId ?? "None"}</dd>
              <dt>Accessibility needs</dt>
              <dd>{request.accessibilityNeeds ?? "None recorded"}</dd>
              <dt>Merged into</dt>
              <dd>{request.mergedIntoRequestId ?? "Not merged"}</dd>
              <dt>Verified / triaged / closed</dt>
              <dd>
                {request.verifiedAt ? new Date(request.verifiedAt).toLocaleString() : "—"} /
                {" "}{request.triagedAt ? new Date(request.triagedAt).toLocaleString() : "—"} /
                {" "}{request.closedAt ? new Date(request.closedAt).toLocaleString() : "—"}
              </dd>
            </dl>
          </section>

          <MapView markers={markers} />
          <AttachmentList requestId={id} attachments={request.attachments} canUpload={false} />
          <RecommendationPanel targetId={id} />
          <ActionPanel request={request} onChanged={load} />

          <section className="panel" aria-labelledby="timeline-heading">
            <h2 id="timeline-heading">Timeline</h2>
            <Timeline entries={request.timeline} />
          </section>
        </>
      ) : null}
    </div>
  );
}
