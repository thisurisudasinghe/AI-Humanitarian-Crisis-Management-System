"use client";

import Link from "next/link";
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
import { api, errorMessage } from "@/lib/api";
import {
  RESOLVED_REQUEST_STATUSES,
  caseRequestErrorMessage,
  listMyCaseRequests,
  submitRequestFeedback,
  type CaseRequestListItem
} from "@/lib/crisis";
import { formatShortDate } from "@/lib/format";
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
        if (active) setReportError(caseRequestErrorMessage(cause, "Reports could not be loaded."));
      });

    const inboxRequest = getInbox({ limit: 8 })
      .then((result) => {
        if (active) {
          setInbox(result);
          setInboxError(null);
        }
      })
      .catch((cause) => {
        if (active) setInboxError(errorMessage(cause));
      });

    const alertRequest = api<PublicAlert[]>("/api/v1/public/alerts")
      .then((rows) => {
        if (active) {
          setAlerts(rows);
          setAlertError(null);
        }
      })
      .catch((cause) => {
        if (active) setAlertError(errorMessage(cause));
      });

    const centreRequest = api<PublicServiceCentre[]>("/api/v1/public/service-centres")
      .then((rows) => {
        if (active) {
          setCentres(rows);
          setCentreError(null);
        }
      })
      .catch((cause) => {
        if (active) setCentreError(errorMessage(cause));
      });

    Promise.all([reportRequest, inboxRequest, alertRequest, centreRequest]).finally(() => {
      if (active) setLoading(false);
    });

    return () => {
      active = false;
    };
  }, []);

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
      setFeedbackError("Select a completed report before sending feedback.");
      return;
    }

    if (feedback.trim().length < 2) {
      setFeedbackError("Enter at least two characters of feedback.");
      return;
    }

    setFeedbackBusy(true);
    try {
      await submitRequestFeedback(selectedFeedbackId, feedback);
      setFeedback("");
      setFeedbackNotice("Thank you. Your feedback was submitted for review.");
    } catch (cause) {
      setFeedbackError(errorMessage(cause));
    } finally {
      setFeedbackBusy(false);
    }
  }

  return (
    <AuthGuard allowedRoles={[RoleNames.Citizen]}>
      <PortalHeader
        eyebrow="Citizen portal"
        title="Your reports, assistance and updates"
        description="Track what you submitted, find verified support and respond to important messages."
        action={
          <Link className="btn btn-primary" href="/report">
            <Icon name="alert" /> New report
          </Link>
        }
      />

      <PortalStats
        items={[
          { label: "Open reports", value: loading ? "—" : openReports, icon: "clock", tone: "blue" },
          {
            label: "Resolved reports",
            value: loading ? "—" : resolvedReports.length,
            icon: "check",
            tone: "green"
          },
          {
            label: "Unread messages",
            value: loading ? "—" : (inbox?.unreadCount ?? 0),
            icon: "bell",
            tone: "green"
          },
          { label: "Public alerts", value: loading ? "—" : alerts.length, icon: "alert", tone: "blue" }
        ]}
      />

      <PortalSection
        title="Report and tracking"
        description="Review status and milestones for the reports connected to your account."
        action={
          <Link className="portal-text-link" href="/my-reports">
            Full report history →
          </Link>
        }
      >
        {loading ? (
          <PortalState kind="loading" title="Loading reports">
            Reading your submitted case requests.
          </PortalState>
        ) : reportError ? (
          <PortalState kind="error" title="Reports could not be loaded">
            {reportError}
          </PortalState>
        ) : reports.length === 0 ? (
          <PortalState kind="empty" title="No reports submitted" href="/report" linkLabel="Submit a report">
            Once you report a crisis, its reference and review progress appear here.
          </PortalState>
        ) : (
          <div className="portal-list">
            {reports.map((report) => (
              <article className="portal-row" key={report.id}>
                <i>
                  <Icon name="file" size={18} />
                </i>
                <div>
                  <b>{report.category} report</b>
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
          title="Assistance and service centres"
          description="Approved public support points published by response coordinators."
          action={
            <Link className="portal-text-link" href="/find-assistance">
              Find assistance →
            </Link>
          }
        >
          {loading ? (
            <PortalState kind="loading" title="Checking service centres">
              Loading the published support points.
            </PortalState>
          ) : centreError ? (
            <PortalState kind="error" title="Service centres could not be loaded">
              {centreError}
            </PortalState>
          ) : centres.length === 0 ? (
            <PortalState kind="empty" title="No service centres published yet">
              Approved support points will appear here when coordinators publish them.
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
                      Services: {jsonList(centre.services)} · Hours: {centre.openingHours || "Not published"}
                    </small>
                  </div>
                  <span className="badge green">
                    <i /> Published
                  </span>
                </article>
              ))}
            </div>
          )}
        </PortalSection>

        <PortalSection
          title="Alerts and notifications"
          description="Published coordinator alerts and messages sent to your account."
          action={
            <Link className="portal-text-link" href="/notifications">
              Open inbox →
            </Link>
          }
        >
          {loading ? (
            <PortalState kind="loading" title="Loading updates">
              Checking alerts and your inbox.
            </PortalState>
          ) : (
            <>
              {alertError ? (
                <PortalState kind="error" title="Public alerts could not be loaded">
                  {alertError}
                </PortalState>
              ) : alerts.length === 0 ? (
                <p className="portal-subtle">No published public alerts right now.</p>
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
                <PortalState kind="error" title="Inbox could not be loaded">
                  {inboxError}
                </PortalState>
              ) : !inbox || inbox.items.length === 0 ? (
                <p className="portal-subtle">No personal messages yet.</p>
              ) : (
                <div className="portal-list">
                  {inbox.items.slice(0, 3).map((notification) => (
                    <article className="portal-row" key={notification.id}>
                      <i className={notification.isRead ? "muted" : ""}>
                        <Icon name={notification.isRead ? "check" : "bell"} size={18} />
                      </i>
                      <div>
                        <b>{notification.subject ?? notification.templateKey}</b>
                        <p>{notification.body ?? "Open the inbox for details."}</p>
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
        title="Share feedback"
        description="Tell the response team how assistance went for a resolved report."
      >
        {feedbackNotice ? <PortalNotice>{feedbackNotice}</PortalNotice> : null}
        {feedbackError ? <PortalNotice tone="error">{feedbackError}</PortalNotice> : null}
        {loading ? (
          <PortalState kind="loading" title="Loading completed reports">
            Checking which reports are ready for feedback.
          </PortalState>
        ) : resolvedReports.length === 0 ? (
          <PortalState kind="empty" title="No completed reports yet">
            Feedback becomes available after a report reaches a resolved status.
          </PortalState>
        ) : (
          <form className="portal-feedback-form" onSubmit={(event) => void onFeedbackSubmit(event)}>
            <label>
              <span>Report</span>
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
              <span>Your feedback</span>
              <textarea
                value={feedback}
                onChange={(event) => setFeedback(event.target.value)}
                maxLength={2000}
                placeholder="What worked well, and what could be improved?"
              />
            </label>
            <button className="btn btn-primary" type="submit" disabled={feedbackBusy}>
              {feedbackBusy ? "Submitting…" : "Submit feedback"}
              {feedbackBusy ? null : <Icon name="arrow" size={16} />}
            </button>
          </form>
        )}
      </PortalSection>
    </AuthGuard>
  );
}
