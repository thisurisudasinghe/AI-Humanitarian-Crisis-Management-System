"use client";

import Link from "next/link";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useEffect, useState, type FormEvent } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import {
  PortalHeader,
  PortalNotice,
  PortalSection,
  PortalState,
  PortalStats
} from "@/components/safeway/PortalPrimitives";
import { api } from "@/lib/api";
import {
  RESOLVED_REQUEST_STATUSES,
  caseRequestErrorMessage,
  listMyCaseRequests,
  submitRequestFeedback,
  type CaseRequestListItem
} from "@/lib/crisis";
import { formatShortDate } from "@/lib/format";
import { useT } from "@/lib/i18n";
import { getInbox, type NotificationInboxDto } from "@/lib/notifications";
import { RoleNames } from "@/lib/roles";

interface PublicAlert {
  id: string;
  title: string;
  body: string;
  status: string;
  publishedAt?: string | null;
  createdAt: string;
}

interface PublicServiceCentre {
  id: string;
  name: string;
  address: string;
  services: string;
  openingHours?: string | null;
}

function jsonList(value?: string | null): string {
  if (!value) return "—";
  try {
    const parsed: unknown = JSON.parse(value);
    if (Array.isArray(parsed) && parsed.length > 0) {
      return parsed.filter((item): item is string => typeof item === "string").join(", ") || "—";
    }
  } catch {
    return value;
  }
  return "—";
}

export default function CitizenPortalPage() {
  const t = useT();
  const [reports, setReports] = useState<CaseRequestListItem[]>([]);
  const [inbox, setInbox] = useState<NotificationInboxDto | null>(null);
  const [alerts, setAlerts] = useState<PublicAlert[]>([]);
  const [centres, setCentres] = useState<PublicServiceCentre[]>([]);
  const [loading, setLoading] = useState(true);
  const [reportError, setReportError] = useState<string | null>(null);
  const [inboxError, setInboxError] = useState<string | null>(null);
  const [alertError, setAlertError] = useState<string | null>(null);
  const [centreError, setCentreError] = useState<string | null>(null);
  const [feedbackReportId, setFeedbackReportId] = useState("");
  const [feedback, setFeedback] = useState("");
  const [feedbackBusy, setFeedbackBusy] = useState(false);
  const [feedbackNotice, setFeedbackNotice] = useState<string | null>(null);
  const [feedbackError, setFeedbackError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;

    const reportRequest = listMyCaseRequests(1, 8)
      .then((result) => {
        if (active) {
          setReports(result.items);
          setReportError(null);
        }
      })
      .catch((cause) => {
        if (active) setReportError(caseRequestErrorMessage(cause, t("portal.citizen.reports.loadFailed"), t));
      });

    const inboxRequest = getInbox({ limit: 8 })
      .then((result) => {
        if (active) {
          setInbox(result);
          setInboxError(null);
        }
      })
      .catch((cause) => {
        if (active) setInboxError(localizedErrorMessage(cause, t));
      });

    const alertRequest = api<PublicAlert[]>("/api/v1/public/alerts")
      .then((rows) => {
        if (active) {
          setAlerts(rows);
          setAlertError(null);
        }
      })
      .catch((cause) => {
        if (active) setAlertError(localizedErrorMessage(cause, t));
      });

    const centreRequest = api<PublicServiceCentre[]>("/api/v1/public/service-centres")
      .then((rows) => {
        if (active) {
          setCentres(rows);
          setCentreError(null);
        }
      })
      .catch((cause) => {
        if (active) setCentreError(localizedErrorMessage(cause, t));
      });

    Promise.all([reportRequest, inboxRequest, alertRequest, centreRequest]).finally(() => {
      if (active) setLoading(false);
    });

    return () => {
      active = false;
    };
  }, [t]);

  const openReports = reports.filter(
    (report) => !RESOLVED_REQUEST_STATUSES.includes(report.status)
  ).length;
  const resolvedReports = reports.filter((report) =>
    RESOLVED_REQUEST_STATUSES.includes(report.status)
  );
  const selectedFeedbackId = feedbackReportId || resolvedReports[0]?.id || "";

  async function onFeedbackSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFeedbackNotice(null);
    setFeedbackError(null);

    if (!selectedFeedbackId) {
      setFeedbackError(t("portal.citizen.feedback.selectError"));
      return;
    }

    if (feedback.trim().length < 2) {
      setFeedbackError(t("portal.citizen.feedback.minLengthError"));
      return;
    }

    setFeedbackBusy(true);
    try {
      await submitRequestFeedback(selectedFeedbackId, feedback);
      setFeedback("");
      setFeedbackNotice(t("portal.citizen.feedback.success"));
    } catch (cause) {
      setFeedbackError(localizedErrorMessage(cause, t));
    } finally {
      setFeedbackBusy(false);
    }
  }

  return (
    <AuthGuard allowedRoles={[RoleNames.Citizen]}>
      <PortalHeader
        eyebrow={t("portal.citizen.eyebrow")}
        title={t("portal.citizen.title")}
        description={t("portal.citizen.description")}
        action={
          <Link className="btn btn-primary" href="/report">
            <Icon name="alert" /> {t("portal.citizen.newReport")}
          </Link>
        }
      />

      <PortalStats
        items={[
          { label: t("portal.citizen.stats.openReports"), value: loading ? "—" : openReports, icon: "clock", tone: "blue" },
          {
            label: t("portal.citizen.stats.resolvedReports"),
            value: loading ? "—" : resolvedReports.length,
            icon: "check",
            tone: "green"
          },
          {
            label: t("portal.citizen.stats.unreadMessages"),
            value: loading ? "—" : (inbox?.unreadCount ?? 0),
            icon: "bell",
            tone: "green"
          },
          { label: t("portal.citizen.stats.publicAlerts"), value: loading ? "—" : alerts.length, icon: "alert", tone: "blue" }
        ]}
      />

      <PortalSection
        title={t("portal.citizen.reports.title")}
        description={t("portal.citizen.reports.description")}
        action={
          <Link className="portal-text-link" href="/my-reports">
            {t("portal.citizen.reports.fullHistory")}
          </Link>
        }
      >
        {loading ? (
          <PortalState kind="loading" title={t("portal.citizen.reports.loadingTitle")}>
            {t("portal.citizen.reports.loadingBody")}
          </PortalState>
        ) : reportError ? (
          <PortalState kind="error" title={t("portal.citizen.reports.errorTitle")}>
            {reportError}
          </PortalState>
        ) : reports.length === 0 ? (
          <PortalState kind="empty" title={t("portal.citizen.reports.emptyTitle")} href="/report" linkLabel={t("portal.citizen.reports.emptyLink")}>
            {t("portal.citizen.reports.emptyBody")}
          </PortalState>
        ) : (
          <div className="portal-list">
            {reports.map((report) => (
              <article className="portal-row" key={report.id}>
                <i>
                  <Icon name="file" size={18} />
                </i>
                <div>
                  <b>{t("portal.citizen.reports.categoryLabel", { category: report.category })}</b>
                  <p>{report.description}</p>
                  <small>
                    {formatShortDate(report.submittedAt)} · {report.id.slice(0, 8)}
                  </small>
                </div>
                <span className={`badge ${report.isOverdue ? "coral" : "green"}`}>
                  <i /> {report.status}
                </span>
              </article>
            ))}
          </div>
        )}
      </PortalSection>

      <div className="portal-split-grid">
        <PortalSection
          title={t("portal.citizen.centres.title")}
          description={t("portal.citizen.centres.description")}
          action={
            <Link className="portal-text-link" href="/find-assistance">
              {t("portal.citizen.centres.findAssistance")}
            </Link>
          }
        >
          {loading ? (
            <PortalState kind="loading" title={t("portal.citizen.centres.loadingTitle")}>
              {t("portal.citizen.centres.loadingBody")}
            </PortalState>
          ) : centreError ? (
            <PortalState kind="error" title={t("portal.citizen.centres.errorTitle")}>
              {centreError}
            </PortalState>
          ) : centres.length === 0 ? (
            <PortalState kind="empty" title={t("portal.citizen.centres.emptyTitle")}>
              {t("portal.citizen.centres.emptyBody")}
            </PortalState>
          ) : (
            <div className="portal-list">
              {centres.slice(0, 6).map((centre) => (
                <article className="portal-row" key={centre.id}>
                  <i>
                    <Icon name="pin" size={18} />
                  </i>
                  <div>
                    <b>{centre.name}</b>
                    <p>{centre.address}</p>
                    <small>
                      {t("portal.citizen.centres.services")} {jsonList(centre.services)} · {t("portal.citizen.centres.hours")} {centre.openingHours || t("portal.citizen.centres.notPublished")}
                    </small>
                  </div>
                  <span className="badge green">
                    <i /> {t("portal.citizen.centres.published")}
                  </span>
                </article>
              ))}
            </div>
          )}
        </PortalSection>

        <PortalSection
          title={t("portal.citizen.alerts.title")}
          description={t("portal.citizen.alerts.description")}
          action={
            <Link className="portal-text-link" href="/notifications">
              {t("portal.citizen.alerts.openInbox")}
            </Link>
          }
        >
          {loading ? (
            <PortalState kind="loading" title={t("portal.citizen.alerts.loadingTitle")}>
              {t("portal.citizen.alerts.loadingBody")}
            </PortalState>
          ) : (
            <>
              {alertError ? (
                <PortalState kind="error" title={t("portal.citizen.alerts.errorTitle")}>
                  {alertError}
                </PortalState>
              ) : alerts.length === 0 ? (
                <p className="portal-subtle">{t("portal.citizen.alerts.noAlerts")}</p>
              ) : (
                <div className="portal-list">
                  {alerts.slice(0, 3).map((alert) => (
                    <article className="portal-row" key={alert.id}>
                      <i className="amber">
                        <Icon name="alert" size={18} />
                      </i>
                      <div>
                        <b>{alert.title}</b>
                        <p>{alert.body}</p>
                        <small>{formatShortDate(alert.publishedAt ?? alert.createdAt)}</small>
                      </div>
                    </article>
                  ))}
                </div>
              )}

              {inboxError ? (
                <PortalState kind="error" title={t("portal.citizen.alerts.inboxErrorTitle")}>
                  {inboxError}
                </PortalState>
              ) : !inbox || inbox.items.length === 0 ? (
                <p className="portal-subtle">{t("portal.citizen.alerts.noMessages")}</p>
              ) : (
                <div className="portal-list">
                  {inbox.items.slice(0, 3).map((notification) => (
                    <article className="portal-row" key={notification.id}>
                      <i className={notification.isRead ? "muted" : ""}>
                        <Icon name={notification.isRead ? "check" : "bell"} size={18} />
                      </i>
                      <div>
                        <b>{notification.subject ?? notification.templateKey}</b>
                        <p>{notification.body ?? t("portal.citizen.alerts.inboxDetails")}</p>
                        <small>{formatShortDate(notification.createdAt)}</small>
                      </div>
                    </article>
                  ))}
                </div>
              )}
            </>
          )}
        </PortalSection>
      </div>

      <PortalSection
        title={t("portal.citizen.feedback.title")}
        description={t("portal.citizen.feedback.description")}
      >
        {feedbackNotice ? <PortalNotice>{feedbackNotice}</PortalNotice> : null}
        {feedbackError ? <PortalNotice tone="error">{feedbackError}</PortalNotice> : null}
        {loading ? (
          <PortalState kind="loading" title={t("portal.citizen.feedback.loadingTitle")}>
            {t("portal.citizen.feedback.loadingBody")}
          </PortalState>
        ) : resolvedReports.length === 0 ? (
          <PortalState kind="empty" title={t("portal.citizen.feedback.emptyTitle")}>
            {t("portal.citizen.feedback.emptyBody")}
          </PortalState>
        ) : (
          <form className="portal-feedback-form" onSubmit={(event) => void onFeedbackSubmit(event)}>
            <label>
              <span>{t("portal.citizen.feedback.reportLabel")}</span>
              <select
                value={selectedFeedbackId}
                onChange={(event) => setFeedbackReportId(event.target.value)}
              >
                {resolvedReports.map((report) => (
                  <option key={report.id} value={report.id}>
                    {report.category} · {report.id.slice(0, 8)} · {formatShortDate(report.submittedAt)}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span>{t("portal.citizen.feedback.yourFeedback")}</span>
              <textarea
                value={feedback}
                onChange={(event) => setFeedback(event.target.value)}
                maxLength={2000}
                placeholder={t("portal.citizen.feedback.placeholder")}
              />
            </label>
            <button className="btn btn-primary" type="submit" disabled={feedbackBusy}>
              {feedbackBusy ? t("portal.citizen.feedback.submitting") : t("portal.citizen.feedback.submit")}
              {feedbackBusy ? null : <Icon name="arrow" size={16} />}
            </button>
          </form>
        )}
      </PortalSection>
    </AuthGuard>
  );
}
