"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { api, apiJson } from "@/lib/api";
import { Icon } from "@/components/safeway/Icon";
import { PortalState } from "@/components/safeway/PortalPrimitives";
import { useAuth } from "@/lib/auth";
import { useI18n, useT } from "@/lib/i18n";
import { formatDate } from "@/lib/i18n/core";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { hasAnyRole, RoleNames } from "@/lib/roles";

interface StaffApplication {
  id: string;
  reference: string;
  fullName: string;
  email?: string;
  organization: string;
  designation: string;
  requestedRole: string;
  status: string;
  submittedAt: string;
  reviewedAt?: string | null;
  decisionAt?: string | null;
  reviewedBy?: string | null;
  reviewerName?: string | null;
  rejectionReason?: string | null;
}

type Decision = "Approved" | "Rejected";
type Filter = "all" | "pending" | "approved" | "rejected";

interface PendingDecision {
  application: StaffApplication;
  decision: Decision;
}

function statusLabel(t: (key: string) => string, status: string): string {
  const key = `pw.staff.statusLabels.${status}`;
  const label = t(key);
  return label === key ? t("pw.common.statusUnknown") : label;
}

function roleLabel(t: (key: string) => string, role: string): string {
  const key = `roleLabels.${role}`;
  const label = t(key);
  return label === key ? t("pw.common.notProvided") : label;
}

function statusTone(status: string): string {
  const value = status.toLowerCase();
  if (value.includes("approv")) return "green";
  if (value.includes("reject")) return "coral";
  return "amber";
}

function matchesFilter(status: string, filter: Filter): boolean {
  if (filter === "all") return true;
  const value = status.toLowerCase();
  if (filter === "pending") return !value.includes("approv") && !value.includes("reject");
  if (filter === "approved") return value.includes("approv");
  return value.includes("reject");
}

export default function StaffAccessReviewPage() {
  const t = useT();
  const { locale } = useI18n();
  const { user, ready } = useAuth();
  const [applications, setApplications] = useState<StaffApplication[]>([]);
  const [filter, setFilter] = useState<Filter>("pending");
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [refreshToken, setRefreshToken] = useState(0);
  const [pendingDecision, setPendingDecision] = useState<PendingDecision | null>(null);
  const [rejectionReason, setRejectionReason] = useState("");
  const [deciding, setDeciding] = useState(false);

  const authorized = Boolean(
    ready && user && hasAnyRole(user.roles, [RoleNames.EmergencyCoordinator, RoleNames.SystemAdmin])
  );

  useEffect(() => {
    if (!authorized) return;
    let active = true;
    if (refreshToken === 0) setLoading(true);
    else setRefreshing(true);

    api<StaffApplication[] | { items: StaffApplication[] }>("/api/v1/admin/staff-access-applications")
      .then((payload) => {
        if (!active) return;
        setApplications(Array.isArray(payload) ? payload : payload.items ?? []);
        setError(null);
      })
      .catch((cause) => {
        if (active) setError(localizedErrorMessage(cause, t));
      })
      .finally(() => {
        if (!active) return;
        setLoading(false);
        setRefreshing(false);
      });

    return () => {
      active = false;
    };
  }, [authorized, t, refreshToken]);

  const visible = useMemo(
    () => applications.filter((application) => matchesFilter(application.status, filter)),
    [applications, filter]
  );

  function openDecision(application: StaffApplication, decision: Decision) {
    setRejectionReason("");
    setError(null);
    setPendingDecision({ application, decision });
  }

  async function confirmDecision() {
    if (!pendingDecision || deciding) return;
    if (pendingDecision.decision === "Rejected" && !rejectionReason.trim()) {
      setError(t("pw.staff.review.reasonRequired"));
      return;
    }

    setDeciding(true);
    setError(null);
    try {
      await apiJson<void>(
        `/api/v1/admin/staff-access-applications/${encodeURIComponent(pendingDecision.application.id)}/decision`,
        "POST",
        {
          approved: pendingDecision.decision === "Approved",
          reason: pendingDecision.decision === "Rejected" ? rejectionReason.trim() : null
        }
      );
      setPendingDecision(null);
      setRefreshToken((value) => value + 1);
    } catch (cause) {
      setError(localizedErrorMessage(cause, t));
    } finally {
      setDeciding(false);
    }
  }

  if (!ready) {
    return (
      <main className="public-info pw-page">
        <div className="page-body">
          <PortalState kind="loading" title={t("pw.staff.checking")}>{t("pw.staff.checkingBody")}</PortalState>
        </div>
      </main>
    );
  }

  if (!authorized) {
    return (
      <main className="public-info pw-page">
        <header className="page-hero">
          <div className="page-hero-lines" />
          <div className="eyebrow"><span /> {t("pw.staff.review.eyebrow")}</div>
          <h1>{t("pw.staff.review.title")}</h1>
          <p>{t("pw.staff.review.intro")}</p>
        </header>
        <div className="page-body">
          <PortalState
            kind="error"
            title={user ? t("pw.staff.review.notAuthorizedTitle") : t("pw.staff.signInTitle")}
            href={user ? undefined : "/login?next=/staff-access/review"}
            linkLabel={user ? undefined : t("pw.staff.signInLink")}
          >
            {user ? t("pw.staff.review.notAuthorizedBody") : t("pw.staff.signInBody")}
          </PortalState>
        </div>
      </main>
    );
  }

  return (
    <main className="public-info pw-page">
      <header className="page-hero">
        <div className="page-hero-lines" />
        <div className="eyebrow"><span /> {t("pw.staff.review.eyebrow")}</div>
        <h1>{t("pw.staff.review.title")}</h1>
        <p>{t("pw.staff.review.intro")}</p>
      </header>

      <div className="page-body">
        <section className="pw-section" aria-labelledby="pw-review-queue">
          <div className="pw-section__head">
            <h2 id="pw-review-queue">{t("pw.staff.review.queueTitle")}</h2>
            <p>{t("pw.staff.review.queueBody")}</p>
            <button
              className="btn btn-outline"
              type="button"
              onClick={() => setRefreshToken((value) => value + 1)}
              disabled={loading || refreshing}
              aria-busy={refreshing}
            >
              <Icon name="clock" size={16} />
              {refreshing ? t("pw.common.refreshing") : t("pw.common.refresh")}
            </button>
          </div>

          <div className="pw-filter-row" role="group" aria-label={t("pw.staff.review.filterLabel")}>
            {(["pending", "approved", "rejected", "all"] as Filter[]).map((item) => (
              <button
                className={filter === item ? "is-active" : ""}
                type="button"
                key={item}
                aria-pressed={filter === item}
                onClick={() => setFilter(item)}
              >
                {t(`pw.staff.review.filter.${item}`)}
              </button>
            ))}
          </div>

          {error ? <p className="pw-form-error" role="alert">{error}</p> : null}

          {loading ? (
            <PortalState kind="loading" title={t("pw.staff.review.loadingTitle")}>{t("pw.staff.review.loadingBody")}</PortalState>
          ) : visible.length === 0 ? (
            <PortalState kind="empty" title={t("pw.staff.review.emptyTitle")}>{t("pw.staff.review.emptyBody")}</PortalState>
          ) : (
            <div className="pw-list">
              {visible.map((application) => (
                <article className={`pw-card pw-card--${statusTone(application.status)}`} key={application.id}>
                  <div className="pw-card__topline">
                    <h3>{application.reference}</h3>
                    <span className={`pw-pill pw-pill--${statusTone(application.status)}`}>
                      {statusLabel(t, application.status)}
                    </span>
                  </div>
                  <dl className="pw-facts">
                    <div>
                      <dt>{t("pw.staff.apply.fullName")}</dt>
                      <dd>{application.fullName}</dd>
                    </div>
                    <div>
                      <dt>{t("pw.staff.apply.email")}</dt>
                      <dd>{application.email ?? t("pw.common.notProvided")}</dd>
                    </div>
                    <div>
                      <dt>{t("pw.staff.apply.organization")}</dt>
                      <dd>{application.organization}</dd>
                    </div>
                    <div>
                      <dt>{t("pw.staff.apply.designation")}</dt>
                      <dd>{application.designation}</dd>
                    </div>
                    <div>
                      <dt>{t("pw.staff.apply.requestedRole")}</dt>
                      <dd>{roleLabel(t, application.requestedRole)}</dd>
                    </div>
                    <div>
                      <dt>{t("pw.guestReport.submitted")}</dt>
                      <dd>{formatDate(application.submittedAt, locale)}</dd>
                    </div>
                    <div>
                      <dt>{t("pw.staff.review.decisionTime")}</dt>
                      <dd>{formatDate(application.decisionAt ?? application.reviewedAt, locale)}</dd>
                    </div>
                    <div>
                      <dt>{t("pw.staff.review.reviewer")}</dt>
                      <dd>{application.reviewerName ?? application.reviewedBy ?? t("pw.common.notProvided")}</dd>
                    </div>
                  </dl>
                  {application.rejectionReason ? (
                    <p className="pw-reason" role="note">
                      <b>{t("pw.staff.status.rejectionReason")}</b> {application.rejectionReason}
                    </p>
                  ) : null}
                  {!application.status.toLowerCase().includes("approv") &&
                  !application.status.toLowerCase().includes("reject") ? (
                    <div className="pw-card__links pw-card__links--row">
                      <button className="btn btn-primary" type="button" onClick={() => openDecision(application, "Approved")}>
                        <Icon name="check" size={16} /> {t("pw.staff.review.approve")}
                      </button>
                      <button className="btn btn-outline" type="button" onClick={() => openDecision(application, "Rejected")}>
                        <Icon name="close" size={16} /> {t("pw.staff.review.reject")}
                      </button>
                    </div>
                  ) : null}
                </article>
              ))}
            </div>
          )}
        </section>

        <aside className="pw-callout pw-callout--blue" role="note">
          <span className="pw-callout__icon" aria-hidden="true">
            <Icon name="info" size={20} />
          </span>
          <div>
            <b>{t("pw.staff.review.noteTitle")}</b>
            <p>{t("pw.staff.review.noteBody")}</p>
          </div>
          <Link href="/staff-access/status">{t("pw.staff.status.title")} <Icon name="arrow" size={14} /></Link>
        </aside>
      </div>

      {pendingDecision ? (
        <div className="pw-dialog-backdrop">
          <div className="pw-dialog" role="dialog" aria-modal="true" aria-labelledby="pw-decision-title">
            <h2 id="pw-decision-title">
              {pendingDecision.decision === "Approved" ? t("pw.staff.review.confirmApproveTitle") : t("pw.staff.review.confirmRejectTitle")}
            </h2>
            <p>
              {pendingDecision.decision === "Approved"
                ? t("pw.staff.review.confirmApproveBody", { reference: pendingDecision.application.reference })
                : t("pw.staff.review.confirmRejectBody", { reference: pendingDecision.application.reference })}
            </p>
            {pendingDecision.decision === "Rejected" ? (
              <label className="pw-field">
                <span className="pw-field__label">{t("pw.staff.review.reasonLabel")}</span>
                <textarea rows={4} value={rejectionReason} onChange={(event) => setRejectionReason(event.target.value)} />
              </label>
            ) : null}
            <div className="pw-card__links pw-card__links--row">
              <button className="btn btn-primary" type="button" onClick={() => void confirmDecision()} disabled={deciding}>
                {deciding ? t("pw.staff.review.deciding") : t("pw.common.confirm")}
              </button>
              <button className="btn btn-outline" type="button" onClick={() => setPendingDecision(null)} disabled={deciding}>
                {t("pw.common.cancel")}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </main>
  );
}
