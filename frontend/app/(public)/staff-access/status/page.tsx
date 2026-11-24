"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { Icon } from "@/components/safeway/Icon";
import { PortalState } from "@/components/safeway/PortalPrimitives";
import { useAuth } from "@/lib/auth";
import { useI18n, useT } from "@/lib/i18n";
import { formatDate } from "@/lib/i18n/core";
import { localizedErrorMessage } from "@/lib/i18n/errors";

interface StaffApplication {
  id: string;
  reference: string;
  fullName: string;
  organization: string;
  designation: string;
  requestedRole: string;
  status: string;
  submittedAt: string;
  reviewedAt?: string | null;
  rejectionReason?: string | null;
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
  const normalized = status.toLowerCase();
  if (normalized.includes("approv")) return "green";
  if (normalized.includes("reject")) return "coral";
  return "amber";
}

export default function StaffAccessStatusPage() {
  const t = useT();
  const { locale } = useI18n();
  const { user, ready } = useAuth();
  const [applications, setApplications] = useState<StaffApplication[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [refreshToken, setRefreshToken] = useState(0);

  useEffect(() => {
    if (!ready || !user) return;
    let active = true;
    if (refreshToken === 0) setLoading(true);
    else setRefreshing(true);

    api<StaffApplication[]>("/api/v1/staff-access/applications/mine")
      .then((rows) => {
        if (!active) return;
        setApplications(rows);
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
  }, [ready, user, t, refreshToken]);

  if (!ready) {
    return (
      <main className="public-info pw-page">
        <div className="page-body">
          <PortalState kind="loading" title={t("pw.staff.checking")}>{t("pw.staff.checkingBody")}</PortalState>
        </div>
      </main>
    );
  }

  if (!user) {
    return (
      <main className="public-info pw-page">
        <header className="page-hero">
          <div className="page-hero-lines" />
          <div className="eyebrow"><span /> {t("pw.staff.status.eyebrow")}</div>
          <h1>{t("pw.staff.status.title")}</h1>
          <p>{t("pw.staff.status.intro")}</p>
        </header>
        <div className="page-body">
          <PortalState
            kind="empty"
            title={t("pw.staff.signInTitle")}
            href="/login?next=/staff-access/status"
            linkLabel={t("pw.staff.signInLink")}
          >
            {t("pw.staff.signInBody")}
          </PortalState>
        </div>
      </main>
    );
  }

  return (
    <main className="public-info pw-page">
      <header className="page-hero">
        <div className="page-hero-lines" />
        <div className="eyebrow"><span /> {t("pw.staff.status.eyebrow")}</div>
        <h1>{t("pw.staff.status.title")}</h1>
        <p>{t("pw.staff.status.intro")}</p>
      </header>

      <div className="page-body">
        <section className="pw-section" aria-labelledby="pw-staff-status-list">
          <div className="pw-section__head">
            <h2 id="pw-staff-status-list">{t("pw.staff.status.listTitle")}</h2>
            <p>{t("pw.staff.status.listBody")}</p>
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

          {error ? <p className="pw-form-error" role="alert">{error}</p> : null}

          {loading ? (
            <PortalState kind="loading" title={t("pw.staff.status.loadingTitle")}>{t("pw.staff.status.loadingBody")}</PortalState>
          ) : applications.length === 0 ? (
            <PortalState
              kind="empty"
              title={t("pw.staff.status.emptyTitle")}
              href="/staff-access"
              linkLabel={t("pw.staff.status.applyLink")}
            >
              {t("pw.staff.status.emptyBody")}
            </PortalState>
          ) : (
            <div className="pw-list">
              {applications.map((application) => (
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
                      <dt>{t("pw.staff.status.reviewed")}</dt>
                      <dd>{formatDate(application.reviewedAt, locale)}</dd>
                    </div>
                  </dl>
                  {application.rejectionReason ? (
                    <p className="pw-reason" role="note">
                      <b>{t("pw.staff.status.rejectionReason")}</b> {application.rejectionReason}
                    </p>
                  ) : null}
                </article>
              ))}
            </div>
          )}
        </section>

        <aside className="pw-callout pw-callout--blue" role="note">
          <span className="pw-callout__icon" aria-hidden="true">
            <Icon name="shield" size={20} />
          </span>
          <div>
            <b>{t("pw.staff.approvalTitle")}</b>
            <p>{t("pw.staff.approvalBody")}</p>
          </div>
          <Link href="/staff-access">{t("pw.staff.status.applyLink")} <Icon name="arrow" size={14} /></Link>
        </aside>
      </div>
    </main>
  );
}
