"use client";

import Link from "next/link";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { Icon } from "@/components/safeway/Icon";
import {
  PortalHeader,
  PortalNotice,
  PortalSection,
  PortalStats,
  PortalState
} from "@/components/safeway/PortalPrimitives";

import { useAuth } from "@/lib/auth";
import {
  RESOLVED_REQUEST_STATUSES,
  caseRequestErrorMessage,
  getMyHousehold,
  listMyCaseRequests,
  type CaseRequestListItem,
  type HouseholdProfile
} from "@/lib/crisis";
import { formatMoney } from "@/lib/donations";
import { formatShortDate, humanizeRole, initials } from "@/lib/format";
import { formatDate, useI18n } from "@/lib/i18n";
import { getInbox, type NotificationInboxDto } from "@/lib/notifications";
import {
  getDonorDonations,
  getMyVolunteerTasks,
  type DonationDto,
  type VolunteerTask
} from "@/lib/portal";
import { hasRole, isStaff, RoleNames, type PublicPurpose } from "@/lib/roles";

type ActivityItem = {
  id: string;
  icon: string;
  title: string;
  detail: string;
  at: string;
  href: string;
  tone?: string;
};

const roleCards: {
  purpose: PublicPurpose;
  role: string;
  icon: string;
  titleKey: string;
  copyKey: string;
  href: string;
}[] = [
  {
    purpose: "citizen",
    role: RoleNames.Citizen,
    icon: "file",
    titleKey: "dashboard.role.title.citizen",
    copyKey: "dashboard.role.copy.citizen",
    href: "/portal/citizen"
  },
  {
    purpose: "volunteer",
    role: RoleNames.Volunteer,
    icon: "users",
    titleKey: "dashboard.role.title.volunteer",
    copyKey: "dashboard.role.copy.volunteer",
    href: "/portal/volunteer"
  },
  {
    purpose: "donor",
    role: RoleNames.Donor,
    icon: "heart",
    titleKey: "dashboard.role.title.donor",
    copyKey: "dashboard.role.copy.donor",
    href: "/portal/donor"
  }
];

function timeValue(value: string): number {
  const parsed = Date.parse(value);
  return Number.isNaN(parsed) ? 0 : parsed;
}

export default function DashboardPage() {
  const { t, locale } = useI18n();
  const router = useRouter();
  const { user, ready, enroll } = useAuth();

  const [today, setToday] = useState("");
  const [greeting, setGreeting] = useState(t("dashboard.greeting.default"));
  const [reports, setReports] = useState<CaseRequestListItem[]>([]);
  const [reportTotal, setReportTotal] = useState(0);
  const [reportLoading, setReportLoading] = useState(true);
  const [reportError, setReportError] = useState<string | null>(null);
  const [inbox, setInbox] = useState<NotificationInboxDto | null>(null);
  const [inboxLoading, setInboxLoading] = useState(true);
  const [inboxError, setInboxError] = useState<string | null>(null);
  const [household, setHousehold] = useState<HouseholdProfile | null>(null);
  const [householdLoaded, setHouseholdLoaded] = useState(false);
  const [donations, setDonations] = useState<DonationDto[]>([]);
  const [tasks, setTasks] = useState<VolunteerTask[]>([]);
  const [enrolling, setEnrolling] = useState<PublicPurpose | null>(null);
  const [enrollError, setEnrollError] = useState<string | null>(null);
  const [enrollSuccess, setEnrollSuccess] = useState<string | null>(null);

  const roles = user?.roles ?? [];
  const staff = user ? isStaff(roles) : false;
  const isCitizen = hasRole(roles, RoleNames.Citizen);
  const isVolunteer = hasRole(roles, RoleNames.Volunteer);
  const isDonor = hasRole(roles, RoleNames.Donor);

  useEffect(() => {
    if (ready && user && isStaff(user.roles)) {
      router.replace("/operations");
    }
  }, [ready, user, router]);

  useEffect(() => {
    const now = new Date();
    setToday(
      formatDate(now, locale, {
        weekday: "long",
        day: "numeric",
        month: "long"
      }).toUpperCase()
    );
    const hour = now.getHours();
    setGreeting(hour < 12 ? t("dashboard.greeting.morning") : hour < 18 ? t("dashboard.greeting.afternoon") : t("dashboard.greeting.evening"));
  }, [t, locale]);

  useEffect(() => {
    if (!ready || !user) {
      return;
    }

    let active = true;
    setReportLoading(true);
    setInboxLoading(true);
    setHouseholdLoaded(isCitizen ? false : true);

    void listMyCaseRequests(1, 5)
      .then((result) => {
        if (!active) return;
        setReports(result.items);
        setReportTotal(result.total);
        setReportError(null);
      })
      .catch((cause) => {
        if (active) setReportError(caseRequestErrorMessage(cause, t("dashboard.tracking.loadFailed"), t));
      })
      .finally(() => {
        if (active) setReportLoading(false);
      });

    void getInbox({ limit: 5 })
      .then((result) => {
        if (!active) return;
        setInbox(result);
        setInboxError(null);
      })
      .catch((cause) => {
        if (active) setInboxError(localizedErrorMessage(cause, t));
      })
      .finally(() => {
        if (active) setInboxLoading(false);
      });

    if (isCitizen) {
      void getMyHousehold()
        .then((profile) => {
          if (active) setHousehold(profile);
        })
        .catch(() => {
          if (active) setHousehold(null);
        })
        .finally(() => {
          if (active) setHouseholdLoaded(true);
        });
    }

    if (isDonor) {
      void getDonorDonations(1, 5)
        .then((result) => {
          if (active) setDonations(result.items);
        })
        .catch(() => {
          if (active) setDonations([]);
        });
    }

    if (isVolunteer) {
      void getMyVolunteerTasks()
        .then((result) => {
          if (active) setTasks(result.items.slice(0, 5));
        })
        .catch(() => {
          if (active) setTasks([]);
        });
    }

    return () => {
      active = false;
    };
  }, [ready, user, isCitizen, isDonor, isVolunteer, t]);

  const activities = useMemo<ActivityItem[]>(() => {
    const rows: ActivityItem[] = [];

    reports.forEach((report) => {
      rows.push({
        id: `report-${report.id}`,
        icon: "file",
        title: RESOLVED_REQUEST_STATUSES.includes(report.status)
          ? t("dashboard.activity.reportResolved", { category: report.category })
          : t("dashboard.activity.reportUpdated", { category: report.category }),
        detail: `${report.status} · ${report.id.slice(0, 8)}`,
        at: report.submittedAt,
        href: "/portal/citizen",
        tone: "blue"
      });
    });

    (inbox?.items ?? []).forEach((notification) => {
      rows.push({
        id: `notice-${notification.id}`,
        icon: notification.isRead ? "check" : "bell",
        title: notification.subject ?? notification.templateKey,
        detail: notification.body ?? t("dashboard.inbox.inboxDetails"),
        at: notification.createdAt,
        href: "/notifications",
        tone: notification.isRead ? "muted" : "green"
      });
    });

    donations.forEach((donation) => {
      rows.push({
        id: `donation-${donation.id}`,
        icon: "heart",
        title: t("dashboard.activity.donationRecorded"),
        detail: `${donation.reference} · ${formatMoney(donation.amount, donation.currency)}`,
        at: donation.receivedAt,
        href: "/portal/donor",
        tone: "green"
      });
    });

    tasks.forEach((task) => {
      rows.push({
        id: `task-${task.id}`,
        icon: "users",
        title: t("dashboard.activity.volunteerTask", { status: task.status.toLowerCase() }),
        detail: task.title,
        at: task.scheduledAt ?? task.completedAt ?? task.id,
        href: "/portal/volunteer",
        tone: "blue"
      });
    });

    return rows
      .filter((row) => timeValue(row.at) > 0)
      .sort((a, b) => timeValue(b.at) - timeValue(a.at))
      .slice(0, 6);
  }, [reports, inbox, donations, tasks, t]);

  async function onEnroll(purpose: PublicPurpose, label: string) {
    setEnrolling(purpose);
    setEnrollError(null);
    setEnrollSuccess(null);

    try {
      await enroll(purpose);
      setEnrollSuccess(t("dashboard.enroll.success", { role: label }));
    } catch (cause) {
      setEnrollError(localizedErrorMessage(cause, t));
    } finally {
      setEnrolling(null);
    }
  }

  if (staff) {
    return (
      <div className="page">
        <section className="card">
          <h1>{t("dashboard.redirect.title")}</h1>
          <p>
            {t("dashboard.redirect.body")}{" "}
            <Link href="/operations">{t("dashboard.redirect.link")}</Link>.
          </p>
        </section>
      </div>
    );
  }

  const firstName = user?.fullName?.trim().split(/\s+/)[0] ?? t("dashboard.account.there");
  const activePurposeCount = roleCards.filter((card) => hasRole(roles, card.role)).length;

  return (
    <>
      <PortalHeader
        eyebrow={today || "SAFEWAY"}
        title={`${greeting}, ${firstName}.`}
        description={t("dashboard.header.description")}
        action={
          <Link className="btn btn-primary" href="/report">
            <Icon name="alert" /> {t("dashboard.reportCrisis")}
          </Link>
        }
      />

      <PortalStats
        items={[
          {
            label: t("dashboard.stats.myReports"),
            value: reportLoading ? "—" : reportTotal,
            hint: t("dashboard.stats.submittedCases"),
            icon: "file",
            tone: "blue"
          },
          {
            label: t("dashboard.stats.unreadNotifications"),
            value: inboxLoading ? "—" : (inbox?.unreadCount ?? 0),
            hint: t("dashboard.stats.updatesWaiting"),
            icon: "bell",
            tone: "green"
          },
          {
            label: t("dashboard.stats.recentActivity"),
            value: activities.length,
            hint: t("dashboard.stats.fromConnected"),
            icon: "clock",
            tone: "blue"
          },
          {
            label: t("dashboard.stats.participationRoles"),
            value: activePurposeCount,
            hint: t("dashboard.stats.serverRoles"),
            icon: "shield",
            tone: "green"
          }
        ]}
      />

      <section className="portal-profile-summary">
        <div className="portal-identity">
          <span className="portal-avatar">{initials(user?.fullName)}</span>
          <div>
            <small>{t("dashboard.account.accountHolder")}</small>
            <h2>{user?.fullName ?? t("dashboard.account.yourAccount")}</h2>
            <p>{user?.email ?? t("dashboard.account.noEmail")}</p>
          </div>
        </div>
        <dl>
          <div>
            <dt>{t("dashboard.account.roles")}</dt>
            <dd className="role-chips">
              {roles.length > 0 ? (
                roles.map((role) => (
                  <span className="badge blue" key={role}>
                    {humanizeRole(role, t)}
                  </span>
                ))
              ) : (
                <span>—</span>
              )}
            </dd>
          </div>
          <div>
            <dt>{t("dashboard.account.reference")}</dt>
            <dd>{user?.userId ? user.userId.slice(0, 8).toUpperCase() : "—"}</dd>
          </div>
          <div>
            <dt>{t("dashboard.account.household")}</dt>
            <dd>
              {!isCitizen
                ? t("dashboard.household.notApplicable")
                : !householdLoaded
                  ? t("dashboard.household.loading")
                  : household
                    ? household.size === 1
                      ? t("dashboard.household.person", { count: household.size })
                      : t("dashboard.household.people", { count: household.size })
                    : t("dashboard.household.notAdded")}
            </dd>
          </div>
        </dl>
      </section>

      {enrollError ? <PortalNotice tone="error">{enrollError}</PortalNotice> : null}
      {enrollSuccess ? <PortalNotice>{enrollSuccess}</PortalNotice> : null}

      <PortalSection
        title={t("dashboard.roles.title")}
        description={t("dashboard.roles.description")}
      >
        <div className="portal-role-cards">
          {roleCards.map((card) => {
            const active = hasRole(roles, card.role);
            const title = t(card.titleKey);
            const copy = t(card.copyKey);
            return (
              <article className={`portal-role-card ${active ? "active" : ""}`} key={card.purpose}>
                <i>
                  <Icon name={card.icon} />
                </i>
                <div>
                  <span className={`badge ${active ? "green" : "amber"}`}>
                    <i /> {active ? t("dashboard.roles.available") : t("dashboard.roles.notAdded")}
                  </span>
                  <h3>{title}</h3>
                  <p>{copy}</p>
                </div>
                {active ? (
                  <Link className="btn btn-outline" href={card.href}>
                    {t("dashboard.roles.openPortal")} <Icon name="arrow" size={16} />
                  </Link>
                ) : (
                  <button
                    className="btn btn-primary"
                    type="button"
                    disabled={enrolling !== null}
                    onClick={() => void onEnroll(card.purpose, title)}
                  >
                    {enrolling === card.purpose
                      ? t("dashboard.roles.adding")
                      : t("dashboard.roles.add", { role: title.toLowerCase() })}
                    {enrolling === card.purpose ? null : <Icon name="arrow" size={16} />}
                  </button>
                )}
              </article>
            );
          })}
        </div>
      </PortalSection>

      <div className="portal-dashboard-grid">
        <PortalSection
          title={t("dashboard.tracking.title")}
          description={t("dashboard.tracking.description")}
          action={
            <Link href="/portal/citizen" className="portal-text-link">
              {t("dashboard.tracking.link")}
            </Link>
          }
        >
          {reportLoading ? (
            <PortalState kind="loading" title={t("dashboard.tracking.loadingTitle")}>
              {t("dashboard.tracking.loadingBody")}
            </PortalState>
          ) : reportError ? (
            <PortalState kind="error" title={t("dashboard.tracking.errorTitle")}>
              {reportError}
            </PortalState>
          ) : reports.length === 0 ? (
            <PortalState kind="empty" title={t("dashboard.tracking.emptyTitle")} href="/report" linkLabel={t("dashboard.tracking.emptyLink")}>
              {t("dashboard.tracking.emptyBody")}
            </PortalState>
          ) : (
            <div className="portal-list">
              {reports.map((report) => (
                <article className="portal-row" key={report.id}>
                  <i>
                    <Icon name="file" size={18} />
                  </i>
                  <div>
                    <b>{t("dashboard.tracking.categoryLabel", { category: report.category })}</b>
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

        <PortalSection
          title={t("dashboard.inbox.title")}
          description={t("dashboard.inbox.description")}
          action={
            <Link href="/notifications" className="portal-text-link">
              {t("dashboard.inbox.viewAll")}
            </Link>
          }
        >
          {inboxLoading ? (
            <PortalState kind="loading" title={t("dashboard.inbox.loadingTitle")}>
              {t("dashboard.inbox.loadingBody")}
            </PortalState>
          ) : inboxError ? (
            <PortalState kind="error" title={t("dashboard.inbox.errorTitle")}>
              {inboxError}
            </PortalState>
          ) : !inbox || inbox.items.length === 0 ? (
            <PortalState kind="empty" title={t("dashboard.inbox.emptyTitle")}>
              {t("dashboard.inbox.emptyBody")}
            </PortalState>
          ) : (
            <div className="portal-list">
              {inbox.items.map((notification) => (
                <article className="portal-row" key={notification.id}>
                  <i className={notification.isRead ? "muted" : ""}>
                    <Icon name={notification.isRead ? "check" : "bell"} size={18} />
                  </i>
                  <div>
                    <b>{notification.subject ?? notification.templateKey}</b>
                    <p>{notification.body ?? t("dashboard.inbox.inboxDetails")}</p>
                    <small>{formatShortDate(notification.createdAt)}</small>
                  </div>
                  {!notification.isRead ? (
                    <span className="badge green">
                      <i /> {t("dashboard.inbox.new")}
                    </span>
                  ) : null}
                </article>
              ))}
            </div>
          )}
        </PortalSection>
      </div>

      <PortalSection
        title={t("dashboard.activity.title")}
        description={t("dashboard.activity.description")}
      >
        {activities.length === 0 ? (
          <PortalState kind="empty" title={t("dashboard.activity.emptyTitle")}>
            {t("dashboard.activity.emptyBody")}
          </PortalState>
        ) : (
          <div className="portal-activity">
            {activities.map((item) => (
              <Link className="portal-activity-row" href={item.href} key={item.id}>
                <i className={item.tone ?? ""}>
                  <Icon name={item.icon} size={17} />
                </i>
                <span>
                  <b>{item.title}</b>
                  <small>{item.detail}</small>
                </span>
                <time>{formatShortDate(item.at)}</time>
                <Icon name="chevron" size={16} />
              </Link>
            ))}
          </div>
        )}
      </PortalSection>

      <section className="safety-banner">
        <div>
          <Icon name="shield" size={27} />
        </div>
        <span>
          <b>{t("dashboard.safety.title")}</b>
          <p>
            {t("dashboard.safety.body")}
          </p>
        </span>
        <Link href="/find-assistance">
          {t("dashboard.safety.link")} <Icon name="arrow" size={16} />
        </Link>
      </section>
    </>
  );
}
