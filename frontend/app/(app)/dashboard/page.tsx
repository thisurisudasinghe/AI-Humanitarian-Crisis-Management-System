"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { Skeleton } from "@/components/ui";
import { Icon } from "@/components/safeway/Icon";
import { EmptyPanel, IntegrationNotice } from "@/components/safeway/IntegrationNotice";
import { errorMessage } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { getInbox, type NotificationInboxDto } from "@/lib/notifications";
import { isStaff } from "@/lib/roles";

export default function DashboardPage() {
  const router = useRouter();
  const { user, ready } = useAuth();
  const [today, setToday] = useState("");
  const [greeting, setGreeting] = useState("Welcome back");
  const [inbox, setInbox] = useState<NotificationInboxDto | null>(null);
  const [inboxLoading, setInboxLoading] = useState(true);
  const [inboxError, setInboxError] = useState<string | null>(null);

  const staff = user ? isStaff(user.roles) : false;
  const userId = user?.userId;

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
    if (!ready || !userId) {
      return;
    }

    let active = true;
    setInboxLoading(true);

    getInbox({ limit: 3 })
      .then((result) => {
        if (active) {
          setInbox(result);
          setInboxError(null);
        }
      })
      .catch((error) => {
        if (active) {
          setInboxError(errorMessage(error));
        }
      })
      .finally(() => {
        if (active) {
          setInboxLoading(false);
        }
      });

    return () => {
      active = false;
    };
  }, [ready, userId]);

  const firstName = user?.fullName?.trim().split(/\s+/)[0] ?? "there";

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

  return (
    <>
      <div className="dash-top">
        <div>
          <p className="dash-date">{today || "SAFEWAY"}</p>
          <h1>
            {greeting}, {firstName}.
          </h1>
          <p>Here&apos;s the latest on your reports and assistance near you.</p>
        </div>
        <Link className="btn btn-primary" href="/report">
          <Icon name="alert" /> Report a crisis
        </Link>
      </div>

      <div className="public-action-grid">
        <article className="action-card primary">
          <div className="action-icon">
            <Icon name="alert" />
          </div>
          <div>
            <small>URGENT SUPPORT</small>
            <h2>Report a crisis</h2>
            <p>Tell response teams what&apos;s happening and where help is needed.</p>
          </div>
          <Link href="/report" aria-label="Report a crisis">
            <Icon name="arrow" />
          </Link>
        </article>
        <article className="action-card">
          <div className="action-icon green">
            <Icon name="search" />
          </div>
          <div>
            <small>NEARBY SUPPORT</small>
            <h2>Find assistance</h2>
            <p>Explore shelters, food, healthcare and verified resources.</p>
          </div>
          <Link href="/find-assistance" aria-label="Find assistance">
            <Icon name="arrow" />
          </Link>
        </article>
      </div>

      <div className="dashboard-grid">
        <section className="dash-card reports-card">
          <div className="card-heading">
            <div>
              <h3>My submitted reports</h3>
              <p>Track your requests and latest updates.</p>
            </div>
            <Link href="/my-reports">View all</Link>
          </div>
          <div className="report-list">
            <EmptyPanel title="No reports to display yet" owner="Dev 2 — crisis & case management">
              Your submitted reports will appear here once the crisis intake and case APIs are
              available. Nothing is mocked or pre-filled.
            </EmptyPanel>
          </div>
        </section>

        <aside className="dash-card notification-card">
          <div className="card-heading">
            <div>
              <h3>Notifications</h3>
              <p>
                {inbox
                  ? `${inbox.unreadCount} unread of ${inbox.totalCount}`
                  : "Updates about your reports and nearby assistance"}
              </p>
            </div>
            <Link href="/notifications" aria-label="Open notifications">
              <Icon name="bell" />
            </Link>
          </div>
          {inboxLoading ? (
            <div style={{ padding: "18px 20px" }}>
              <Skeleton rows={2} />
            </div>
          ) : inboxError ? (
            <EmptyPanel title="Notifications could not be loaded">{inboxError}</EmptyPanel>
          ) : !inbox || inbox.items.length === 0 ? (
            <EmptyPanel title="No notifications yet">
              Account messages, report progress and nearby assistance updates will appear here.
            </EmptyPanel>
          ) : (
            <>
              {inbox.items.map((notification) => (
                <div className="notice" key={notification.id}>
                  <i style={notification.isRead ? { background: "#b6c6d2" } : undefined} />
                  <span>
                    <b>{notification.subject ?? notification.templateKey}</b>
                    <p>{notification.body ?? "Open your inbox for details."}</p>
                  </span>
                </div>
              ))}
              <div style={{ padding: "12px 20px" }}>
                <Link className="btn btn-outline" href="/notifications">
                  View all notifications
                </Link>
              </div>
            </>
          )}
        </aside>
      </div>

      <IntegrationNotice
        title="Your account is live; field data is still being connected"
        owner="Dev 2, Dev 3 and Dev 4"
        inline
      >
        Authentication, profile, notifications, donations and KPI reporting are backed by the Dev 1
        API. Crisis reports, shelters, resources and AI recommendations will fill the remaining
        cards as their owners ship their modules.
      </IntegrationNotice>

      <section className="safety-banner">
        <div>
          <Icon name="shield" size={27} />
        </div>
        <span>
          <b>Heavy rainfall guidance</b>
          <p>
            Avoid moving water, keep essential documents dry, and follow verified local
            instructions.
          </p>
        </span>
        <Link href="/contact">
          Contact support <Icon name="arrow" size={16} />
        </Link>
      </section>
    </>
  );
}
