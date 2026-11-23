"use client";

import Link from "next/link";
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
import { errorMessage } from "@/lib/api";
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
  title: string;
  copy: string;
  href: string;
}[] = [
  {
    purpose: "citizen",
    role: RoleNames.Citizen,
    icon: "file",
    title: "Citizen",
    copy: "Submit reports, follow response progress, find assistance and share feedback.",
    href: "/portal/citizen"
  },
  {
    purpose: "volunteer",
    role: RoleNames.Volunteer,
    icon: "users",
    title: "Volunteer",
    copy: "Manage your skills and availability, review tasks and see open opportunities.",
    href: "/portal/volunteer"
  },
  {
    purpose: "donor",
    role: RoleNames.Donor,
    icon: "heart",
    title: "Donor",
    copy: "Record supported donations, review your history and read transparency data.",
    href: "/portal/donor"
  }
];

function timeValue(value: string): number {
  const parsed = Date.parse(value);
  return Number.isNaN(parsed) ? 0 : parsed;
}

export default function DashboardPage() {
  const router = useRouter();
  const { user, ready, enroll } = useAuth();

  const [today, setToday] = useState("");
  const [greeting, setGreeting] = useState("Welcome back");
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
      now
        .toLocaleDateString(undefined, {
          weekday: "long",
          day: "numeric",
          month: "long"
        })
        .toUpperCase()
    );
    const hour = now.getHours();
    setGreeting(hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening");
  }, []);

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
        if (active) setReportError(caseRequestErrorMessage(cause, "Reports could not be loaded."));
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
        if (active) setInboxError(errorMessage(cause));
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
  }, [ready, user, isCitizen, isDonor, isVolunteer]);

  const activities = useMemo<ActivityItem[]>(() => {
    const rows: ActivityItem[] = [];

    reports.forEach((report) => {
      rows.push({
        id: `report-${report.id}`,
        icon: "file",
        title: `${report.category} report ${RESOLVED_REQUEST_STATUSES.includes(report.status) ? "resolved" : "updated"}`,
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
        detail: notification.body ?? "Open the inbox for details.",
        at: notification.createdAt,
        href: "/notifications",
        tone: notification.isRead ? "muted" : "green"
      });
    });

    donations.forEach((donation) => {
      rows.push({
        id: `donation-${donation.id}`,
        icon: "heart",
        title: "Donation recorded",
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
        title: `Volunteer task ${task.status.toLowerCase()}`,
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
  }, [reports, inbox, donations, tasks]);

  async function onEnroll(purpose: PublicPurpose, label: string) {
    setEnrolling(purpose);
    setEnrollError(null);
    setEnrollSuccess(null);

    try {
      await enroll(purpose);
      setEnrollSuccess(`${label} access added to your account.`);
    } catch (cause) {
      setEnrollError(errorMessage(cause));
    } finally {
      setEnrolling(null);
    }
  }

  if (staff) {
    return (
      <div className="page">
        <section className="card">
          <h1>Redirecting to the command centre…</h1>
          <p>
            Staff accounts use the operations dashboard. If nothing happens,{" "}
            <Link href="/operations">open the command centre</Link>.
          </p>
        </section>
      </div>
    );
  }

  const firstName = user?.fullName?.trim().split(/\s+/)[0] ?? "there";
  const activePurposeCount = roleCards.filter((card) => hasRole(roles, card.role)).length;

  return (
    <>
      <PortalHeader
        eyebrow={today || "SAFEWAY"}
        title={`${greeting}, ${firstName}.`}
        description="Your reports, assistance updates and participation in one secure place."
        action={
          <Link className="btn btn-primary" href="/report">
            <Icon name="alert" /> Report a crisis
          </Link>
        }
      />

      <PortalStats
        items={[
          {
            label: "My reports",
            value: reportLoading ? "—" : reportTotal,
            hint: "Submitted case requests",
            icon: "file",
            tone: "blue"
          },
          {
            label: "Unread notifications",
            value: inboxLoading ? "—" : (inbox?.unreadCount ?? 0),
            hint: "Updates waiting for you",
            icon: "bell",
            tone: "green"
          },
          {
            label: "Recent activity",
            value: activities.length,
            hint: "From your connected records",
            icon: "clock",
            tone: "blue"
          },
          {
            label: "Participation roles",
            value: activePurposeCount,
            hint: "Roles set by the server",
            icon: "shield",
            tone: "green"
          }
        ]}
      />

      <section className="portal-profile-summary">
        <div className="portal-identity">
          <span className="portal-avatar">{initials(user?.fullName)}</span>
          <div>
            <small>ACCOUNT HOLDER</small>
            <h2>{user?.fullName ?? "Your account"}</h2>
            <p>{user?.email ?? "No email on file"}</p>
          </div>
        </div>
        <dl>
          <div>
            <dt>Account roles</dt>
            <dd className="role-chips">
              {roles.length > 0 ? (
                roles.map((role) => (
                  <span className="badge blue" key={role}>
                    {humanizeRole(role)}
                  </span>
                ))
              ) : (
                <span>—</span>
              )}
            </dd>
          </div>
          <div>
            <dt>Account reference</dt>
            <dd>{user?.userId ? user.userId.slice(0, 8).toUpperCase() : "—"}</dd>
          </div>
          <div>
            <dt>Household</dt>
            <dd>
              {!isCitizen
                ? "Not applicable"
                : !householdLoaded
                  ? "Loading…"
                  : household
                    ? `${household.size} ${household.size === 1 ? "person" : "people"}`
                    : "Not added yet"}
            </dd>
          </div>
        </dl>
      </section>

      {enrollError ? <PortalNotice tone="error">{enrollError}</PortalNotice> : null}
      {enrollSuccess ? <PortalNotice>{enrollSuccess}</PortalNotice> : null}

      <PortalSection
        title="Ways to take part"
        description="Each area uses the role on your account. You can add public participation roles here; the server validates every request."
      >
        <div className="portal-role-cards">
          {roleCards.map((card) => {
            const active = hasRole(roles, card.role);
            return (
              <article className={`portal-role-card ${active ? "active" : ""}`} key={card.purpose}>
                <i>
                  <Icon name={card.icon} />
                </i>
                <div>
                  <span className={`badge ${active ? "green" : "amber"}`}>
                    <i /> {active ? "Available" : "Not added"}
                  </span>
                  <h3>{card.title}</h3>
                  <p>{card.copy}</p>
                </div>
                {active ? (
                  <Link className="btn btn-outline" href={card.href}>
                    Open portal <Icon name="arrow" size={16} />
                  </Link>
                ) : (
                  <button
                    className="btn btn-primary"
                    type="button"
                    disabled={enrolling !== null}
                    onClick={() => void onEnroll(card.purpose, card.title)}
                  >
                    {enrolling === card.purpose ? "Adding…" : `Add ${card.title.toLowerCase()}`}
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
          title="Report tracking"
          description="The latest status from your submitted case requests."
          action={
            <Link href="/portal/citizen" className="portal-text-link">
              Citizen portal →
            </Link>
          }
        >
          {reportLoading ? (
            <PortalState kind="loading" title="Loading reports">
              Reading your submitted case requests.
            </PortalState>
          ) : reportError ? (
            <PortalState kind="error" title="Reports could not be loaded">
              {reportError}
            </PortalState>
          ) : reports.length === 0 ? (
            <PortalState kind="empty" title="No reports yet" href="/report" linkLabel="Report a crisis">
              When you submit a report, its reference and review status appear here.
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

        <PortalSection
          title="Notification inbox"
          description="Account messages and updates that need your attention."
          action={
            <Link href="/notifications" className="portal-text-link">
              View all →
            </Link>
          }
        >
          {inboxLoading ? (
            <PortalState kind="loading" title="Loading notifications">
              Checking your inbox.
            </PortalState>
          ) : inboxError ? (
            <PortalState kind="error" title="Notifications could not be loaded">
              {inboxError}
            </PortalState>
          ) : !inbox || inbox.items.length === 0 ? (
            <PortalState kind="empty" title="You’re all caught up">
              New account and response updates will appear here.
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
                    <p>{notification.body ?? "Open the inbox for details."}</p>
                    <small>{formatShortDate(notification.createdAt)}</small>
                  </div>
                  {!notification.isRead ? (
                    <span className="badge green">
                      <i /> New
                    </span>
                  ) : null}
                </article>
              ))}
            </div>
          )}
        </PortalSection>
      </div>

      <PortalSection
        title="Recent activity"
        description="A combined timeline from records connected to your account."
      >
        {activities.length === 0 ? (
          <PortalState kind="empty" title="No recent activity">
            Submit a report, respond to a notification or take part in a portal to build your
            timeline.
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
          <b>Preparedness reminder</b>
          <p>
            Follow verified local instructions, keep essential documents close and use the report
            flow only for information response teams need.
          </p>
        </span>
        <Link href="/find-assistance">
          Find assistance <Icon name="arrow" size={16} />
        </Link>
      </section>
    </>
  );
}
