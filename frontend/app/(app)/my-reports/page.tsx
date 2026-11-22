"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { Icon } from "@/components/safeway/Icon";
import { EmptyPanel } from "@/components/safeway/IntegrationNotice";
import {
  OPEN_REQUEST_STATUSES,
  RESOLVED_REQUEST_STATUSES,
  caseRequestErrorMessage,
  listMyCaseRequests,
  submitRequestFeedback,
  type CaseRequestListItem
} from "@/lib/crisis";

const filters: [string, string][] = [
  ["All reports", "file"],
  ["In progress", "clock"],
  ["Assistance provided", "check"]
];

function formatDate(value: string): string {
  return new Date(value).toLocaleString();
}

function urgencyTone(urgency: CaseRequestListItem["urgency"]): string {
  if (urgency === "Critical") return "coral";
  if (urgency === "High") return "amber";
  return "green";
}

export default function MyReportsPage() {
  const [filter, setFilter] = useState("All reports");
  const [reports, setReports] = useState<CaseRequestListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [feedbackDrafts, setFeedbackDrafts] = useState<Record<string, string>>({});
  const [feedbackBusy, setFeedbackBusy] = useState<Record<string, boolean>>({});
  const [feedbackErrors, setFeedbackErrors] = useState<Record<string, string>>({});
  const [feedbackSent, setFeedbackSent] = useState<Record<string, boolean>>({});

  useEffect(() => {
    let active = true;

    listMyCaseRequests()
      .then((result) => {
        if (active) setReports(result.items);
      })
      .catch((cause: unknown) => {
        if (active) {
          setError(caseRequestErrorMessage(cause, "Your reports could not be loaded."));
        }
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, []);

  const counts = useMemo<Record<string, number>>(() => {
    const inProgress = reports.filter((report) =>
      OPEN_REQUEST_STATUSES.includes(report.status)
    ).length;
    const assistance = reports.filter((report) =>
      RESOLVED_REQUEST_STATUSES.includes(report.status)
    ).length;

    return {
      "All reports": reports.length,
      "In progress": inProgress,
      "Assistance provided": assistance
    };
  }, [reports]);

  const visibleReports = useMemo(() => {
    if (filter === "In progress") {
      return reports.filter((report) => OPEN_REQUEST_STATUSES.includes(report.status));
    }
    if (filter === "Assistance provided") {
      return reports.filter((report) => RESOLVED_REQUEST_STATUSES.includes(report.status));
    }
    return reports;
  }, [reports, filter]);

  async function onFeedbackSubmit(reportId: string) {
    const comment = (feedbackDrafts[reportId] ?? "").trim();
    if (comment.length < 2) {
      setFeedbackErrors((current) => ({
        ...current,
        [reportId]: "Feedback must be at least 2 characters."
      }));
      return;
    }

    setFeedbackBusy((current) => ({ ...current, [reportId]: true }));
    setFeedbackErrors((current) => ({ ...current, [reportId]: "" }));
    try {
      await submitRequestFeedback(reportId, comment);
      setFeedbackSent((current) => ({ ...current, [reportId]: true }));
      setFeedbackDrafts((current) => ({ ...current, [reportId]: "" }));
    } catch (cause) {
      setFeedbackErrors((current) => ({
        ...current,
        [reportId]: caseRequestErrorMessage(cause, "Feedback could not be submitted.")
      }));
    } finally {
      setFeedbackBusy((current) => ({ ...current, [reportId]: false }));
    }
  }

  return (
    <>
      <div className="reports-page-head">
        <div>
          <p>MY REPORTS</p>
          <h1>Submitted crisis reports</h1>
          <span>Follow report status, response progress and important updates.</span>
        </div>
        <Link className="btn btn-primary" href="/report">
          <Icon name="alert" /> New crisis report
        </Link>
      </div>

      {error ? (
        <div className="form-error" role="alert" style={{ marginBottom: 16 }}>
          {error}
        </div>
      ) : null}

      <div className="report-summary">
        {filters.map(([label, icon]) => (
          <button
            className={filter === label ? "active" : ""}
            type="button"
            onClick={() => setFilter(label)}
            key={label}
          >
            <i>
              <Icon name={icon} />
            </i>
            <span>
              <b>{counts[label]}</b>
              <small>{label}</small>
            </span>
          </button>
        ))}
      </div>

      <section className="reports-detail-list">
        {loading ? (
          <EmptyPanel title="Loading reports">Reading your submitted case requests.</EmptyPanel>
        ) : visibleReports.length === 0 ? (
          <EmptyPanel title={`No ${filter.toLowerCase()} to show`}>
            {error
              ? "Reports could not be loaded. Sign in as a citizen and try again."
              : "Submit a crisis report to see its reference, review milestone and assistance status here."}
          </EmptyPanel>
        ) : (
          visibleReports.map((report) => (
            <article key={report.id}>
              <span className={`badge ${urgencyTone(report.urgency)}`}>
                <i /> {report.urgency}
              </span>
              <div className="report-detail-main">
                <span>
                  <b>{report.category} report</b>
                </span>
                <p>{report.description}</p>
                <small>
                  {formatDate(report.submittedAt)} · {report.id.slice(0, 8)}
                </small>
              </div>
              <div className="report-milestone">
                <span>
                  <i className="done">
                    <Icon name="check" size={11} />
                  </i>
                  Submitted
                </span>
                <em />
                <span>
                  <i className={OPEN_REQUEST_STATUSES.includes(report.status) ? "active" : "done"}>
                    <Icon name="clock" size={11} />
                  </i>
                  {report.status}
                </span>
              </div>
              <span className={`badge ${report.isOverdue ? "coral" : "green"}`}>
                <i /> {report.isOverdue ? "Overdue" : "On track"}
              </span>
              {RESOLVED_REQUEST_STATUSES.includes(report.status) ? (
                <div style={{ gridColumn: "1 / -1", marginTop: 12 }}>
                  {feedbackSent[report.id] ? (
                    <p className="muted" role="status">Thank you. Your feedback was submitted.</p>
                  ) : (
                    <div style={{ display: "grid", gap: 8, maxWidth: 640 }}>
                      <label>
                        <span className="field-label">Feedback after assistance</span>
                        <textarea
                          className="field-input"
                          value={feedbackDrafts[report.id] ?? ""}
                          maxLength={2000}
                          placeholder="Share what went well or what could be improved."
                          onChange={(event) =>
                            setFeedbackDrafts((current) => ({
                              ...current,
                              [report.id]: event.target.value
                            }))
                          }
                        />
                      </label>
                      {feedbackErrors[report.id] ? (
                        <p className="form-error" role="alert">{feedbackErrors[report.id]}</p>
                      ) : null}
                      <div>
                        <button
                          className="btn btn-outline"
                          type="button"
                          disabled={feedbackBusy[report.id]}
                          onClick={() => void onFeedbackSubmit(report.id)}
                        >
                          {feedbackBusy[report.id] ? "Submitting…" : "Submit feedback"}
                        </button>
                      </div>
                    </div>
                  )}
                </div>
              ) : null}
            </article>
          ))
        )}
      </section>

      <div className="report-help-note">
        <Icon name="shield" />
        <span>
          <b>Need to add important information?</b>
          <small>
            This screen shows status only. Use the official citizen portal for detail, timeline
            and updates rather than creating a duplicate report.
          </small>
        </span>
      </div>
    </>
  );
}
