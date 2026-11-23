"use client";

import Link from "next/link";
import { formatDate as formatDateForLocale, resolveLocale } from "@/lib/i18n/core";
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
import { useT } from "@/lib/i18n";

const filters: [string, string][] = [
  ["All reports", "file"],
  ["In progress", "clock"],
  ["Assistance provided", "check"]
];

function formatDate(value: string): string {
  const resolved = typeof document === "undefined" ? "en" : resolveLocale(document.documentElement.lang);
  return formatDateForLocale(value, resolved);
}

function urgencyTone(urgency: CaseRequestListItem["urgency"]): string {
  if (urgency === "Critical") return "coral";
  if (urgency === "High") return "amber";
  return "green";
}

export default function MyReportsPage() {
  const t = useT();
  const filterLabels: Record<string, string> = {
    "All reports": t("reports.filter.all"),
    "In progress": t("reports.filter.inProgress"),
    "Assistance provided": t("reports.filter.assistanceProvided")
  };
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
          setError(caseRequestErrorMessage(cause, t("reports.loadFailed"), t));
        }
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [t]);

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
        [reportId]: t("reports.feedback.error")
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
        [reportId]: caseRequestErrorMessage(cause, t("reports.feedback.submitFailed"), t)
      }));
    } finally {
      setFeedbackBusy((current) => ({ ...current, [reportId]: false }));
    }
  }

  return (
    <>
      <div className="reports-page-head">
        <div>
          <p>{t("reports.eyebrow")}</p>
          <h1>{t("reports.title")}</h1>
          <span>{t("reports.description")}</span>
        </div>
        <Link className="btn btn-primary" href="/report">
          <Icon name="alert" /> {t("reports.newReport")}
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
              <small>{filterLabels[label]}</small>
            </span>
          </button>
        ))}
      </div>

      <section className="reports-detail-list">
        {loading ? (
          <EmptyPanel title={t("reports.loadingTitle")}>{t("reports.loadingBody")}</EmptyPanel>
        ) : visibleReports.length === 0 ? (
          <EmptyPanel title={t("reports.empty.title", { filter: filterLabels[filter].toLowerCase() })}>
            {error
              ? t("reports.empty.error")
              : t("reports.empty.default")}
          </EmptyPanel>
        ) : (
          visibleReports.map((report) => (
            <article key={report.id}>
              <span className={`badge ${urgencyTone(report.urgency)}`}>
                <i /> {report.urgency}
              </span>
              <div className="report-detail-main">
                <span>
                  <b>{t("reports.categoryLabel", { category: report.category })}</b>
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
                  {t("reports.milestone.submitted")}
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
                <i /> {report.isOverdue ? t("reports.overdue") : t("reports.onTrack")}
              </span>
              {RESOLVED_REQUEST_STATUSES.includes(report.status) ? (
                <div style={{ gridColumn: "1 / -1", marginTop: 12 }}>
                  {feedbackSent[report.id] ? (
                    <p className="muted" role="status">{t("reports.feedback.success")}</p>
                  ) : (
                    <div style={{ display: "grid", gap: 8, maxWidth: 640 }}>
                      <label>
                        <span className="field-label">{t("reports.feedback.title")}</span>
                        <textarea
                          className="field-input"
                          value={feedbackDrafts[report.id] ?? ""}
                          maxLength={2000}
                          placeholder={t("reports.feedback.placeholder")}
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
                          {feedbackBusy[report.id] ? t("reports.feedback.submitting") : t("reports.feedback.submit")}
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
          <b>{t("reports.help.title")}</b>
          <small>
            {t("reports.help.body")}
          </small>
        </span>
      </div>
    </>
  );
}
