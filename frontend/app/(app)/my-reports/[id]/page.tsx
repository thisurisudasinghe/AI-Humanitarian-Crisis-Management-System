"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { CitizenFeedbackForm } from "@/components/safeway/CitizenFeedbackForm";
import { CitizenStatusTimeline } from "@/components/safeway/CitizenStatusTimeline";
import { Icon } from "@/components/safeway/Icon";
import { PortalHeader, PortalState } from "@/components/safeway/PortalPrimitives";
import { ApiError } from "@/lib/api";
import { statusTone } from "@/lib/citizen-status";
import {
  RESOLVED_REQUEST_STATUSES,
  caseRequestErrorMessage,
  getCaseRequestDetail,
  type CaseRequestDetail
} from "@/lib/crisis";
import { useI18n } from "@/lib/i18n";
import { formatDate } from "@/lib/i18n/core";
import { RoleNames } from "@/lib/roles";

export default function RequestDetailPage() {
  const params = useParams<{ id: string }>();
  const requestId = typeof params.id === "string" ? params.id : "";
  const { t, locale } = useI18n();
  const [detail, setDetail] = useState<CaseRequestDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notFound, setNotFound] = useState(false);
  const [refreshToken, setRefreshToken] = useState(0);

  useEffect(() => {
    let active = true;
    if (refreshToken === 0) setLoading(true);
    else setRefreshing(true);

    getCaseRequestDetail(requestId)
      .then((result) => {
        if (!active) return;
        setDetail(result);
        setNotFound(false);
        setError(null);
      })
      .catch((cause) => {
        if (!active) return;
        if (cause instanceof ApiError && cause.status === 404) {
          setNotFound(true);
          setError(null);
        } else {
          setError(caseRequestErrorMessage(cause, t("citizenX.tracking.errorTitle"), t));
        }
      })
      .finally(() => {
        if (!active) return;
        setLoading(false);
        setRefreshing(false);
      });

    return () => {
      active = false;
    };
  }, [requestId, t, refreshToken]);

  const eligibleForFeedback = detail ? RESOLVED_REQUEST_STATUSES.includes(detail.status) : false;
  const history = detail ? [...detail.timeline].reverse() : [];

  return (
    <AuthGuard allowedRoles={[RoleNames.Citizen]} activationPurpose="citizen">
      <div className="citizen-detail-page">
        <Link className="back-link citizen-detail-back" href="/my-reports">
          <Icon name="arrowLeft" size={16} /> {t("citizenX.tracking.back")}
        </Link>

        <PortalHeader
          eyebrow={t("citizenX.tracking.detailEyebrow")}
          title={t("citizenX.tracking.detailTitle")}
          description={t("citizenX.tracking.description")}
          action={
            detail ? (
              <button
                className="btn btn-outline"
                type="button"
                onClick={() => setRefreshToken((value) => value + 1)}
                disabled={refreshing}
                aria-busy={refreshing}
              >
                <Icon name="clock" size={16} />
                {refreshing ? t("citizenX.tracking.refreshing") : t("citizenX.tracking.refresh")}
              </button>
            ) : undefined
          }
        />

        {loading ? (
          <PortalState kind="loading" title={t("citizenX.tracking.loadingTitle")}>
            {t("citizenX.tracking.loadingBody")}
          </PortalState>
        ) : notFound ? (
          <PortalState kind="empty" title={t("citizenX.tracking.notFoundTitle")} href="/my-reports" linkLabel={t("citizenX.tracking.back")}>
            {t("citizenX.tracking.notFoundBody")}
          </PortalState>
        ) : error ? (
          <PortalState kind="error" title={t("citizenX.tracking.errorTitle")}>
            {error}
          </PortalState>
        ) : !detail ? null : (
          <div className="citizen-detail-layout">
            <section className="citizen-detail-main" aria-label={t("citizenX.tracking.detailTitle")}>
              <div className="citizen-detail-headline">
                <div>
                  <span className="citizen-detail-kicker">{t(`citizenX.category.${detail.category}`)}</span>
                  <h2>{detail.description}</h2>
                  <p className="citizen-detail-reference">
                    {t("citizenX.tracking.reference")}: <span>{detail.id}</span>
                  </p>
                </div>
                <span className={`badge ${statusTone(detail.status)}`}>
                  <i aria-hidden="true" /> {t(`citizenX.status.${detail.status}`)}
                </span>
              </div>

              <dl className="citizen-detail-facts">
                <div>
                  <dt>{t("citizenX.tracking.submitted")}</dt>
                  <dd>{formatDate(detail.submittedAt, locale)}</dd>
                </div>
                <div>
                  <dt>{t("citizenX.tracking.location")}</dt>
                  <dd>{detail.address?.trim() || t("citizenX.tracking.notProvided")}</dd>
                </div>
                <div>
                  <dt>{t("citizenX.tracking.requestedAssistance")}</dt>
                  <dd>{t(`citizenX.category.${detail.category}`)}</dd>
                </div>
                <div>
                  <dt>{t("citizenX.tracking.affectedPeople")}</dt>
                  <dd>{detail.householdSize}</dd>
                </div>
                <div>
                  <dt>{t("citizenX.tracking.accessibilityNeeds")}</dt>
                  <dd>{detail.accessibilityNeeds?.trim() || t("citizenX.tracking.notProvided")}</dd>
                </div>
                <div>
                  <dt>{t("citizenX.tracking.coordinates")}</dt>
                  <dd>{Number(detail.lat).toFixed(5)}, {Number(detail.lng).toFixed(5)}</dd>
                </div>
              </dl>

              <section className="citizen-panel" aria-labelledby="citizen-timeline-heading">
                <div className="citizen-panel__head">
                  <i aria-hidden="true"><Icon name="route" size={20} /></i>
                  <div>
                    <h2 id="citizen-timeline-heading">{t("citizenX.tracking.timelineTitle")}</h2>
                    <p>{t("citizenX.tracking.timelineDescription")}</p>
                  </div>
                </div>
                <CitizenStatusTimeline status={detail.status} timeline={detail.timeline} />
              </section>

              <section className="citizen-panel" aria-labelledby="citizen-history-heading">
                <div className="citizen-panel__head">
                  <i aria-hidden="true"><Icon name="clock" size={20} /></i>
                  <div>
                    <h2 id="citizen-history-heading">{t("citizenX.tracking.statusHistoryTitle")}</h2>
                    <p>{t("citizenX.tracking.statusHistoryDescription")}</p>
                  </div>
                </div>
                {history.length === 0 ? (
                  <p className="portal-subtle">{t("citizenX.tracking.noHistory")}</p>
                ) : (
                  <ol className="citizen-history-list">
                    {history.map((entry) => (
                      <li key={entry.id}>
                        <span className={`badge ${statusTone(entry.toStatus)}`}>
                          <i aria-hidden="true" /> {t(`citizenX.status.${entry.toStatus}`)}
                        </span>
                        <div>
                          <b>{t(`citizenX.statusNote.${entry.toStatus}`)}</b>
                          {entry.reason ? <p>{entry.reason}</p> : null}
                          <small>{formatDate(entry.createdAt, locale)}</small>
                        </div>
                      </li>
                    ))}
                  </ol>
                )}
              </section>

              <section className="citizen-panel" aria-labelledby="citizen-attachments-heading">
                <div className="citizen-panel__head">
                  <i aria-hidden="true"><Icon name="upload" size={20} /></i>
                  <div>
                    <h2 id="citizen-attachments-heading">{t("citizenX.tracking.attachmentsTitle")}</h2>
                  </div>
                </div>
                {detail.attachments.length === 0 ? (
                  <p className="portal-subtle">{t("citizenX.tracking.noAttachments")}</p>
                ) : (
                  <ul className="citizen-attachment-list">
                    {detail.attachments.map((attachment) => (
                      <li key={attachment.id}>
                        <Icon name="file" size={16} /> <span>{attachment.originalFileName}</span>
                        <small>{formatDate(attachment.createdAt, locale)}</small>
                      </li>
                    ))}
                  </ul>
                )}
              </section>
            </section>

            {eligibleForFeedback ? (
              <aside className="citizen-detail-side">
                <CitizenFeedbackForm requestId={detail.id} />
              </aside>
            ) : null}
          </div>
        )}
      </div>
    </AuthGuard>
  );
}
