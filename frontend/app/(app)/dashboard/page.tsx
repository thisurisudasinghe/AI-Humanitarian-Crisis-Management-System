"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { Icon } from "@/components/safeway/Icon";
import { EmptyPanel, IntegrationNotice } from "@/components/safeway/IntegrationNotice";
import { useAuth } from "@/lib/auth";
import { isStaff } from "@/lib/roles";

export default function DashboardPage() {
  const router = useRouter();
  const { user, ready } = useAuth();
  const [today, setToday] = useState("");
  const [greeting, setGreeting] = useState("Welcome back");

  const staff = user ? isStaff(user.roles) : false;

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
              <p>Updates about your reports and nearby assistance</p>
            </div>
            <Link href="/notifications" aria-label="Open notifications">
              <Icon name="bell" />
            </Link>
          </div>
          <EmptyPanel title="Notifications are not connected" owner="Member 3 — M9 notifications">
            Delivery, read state and preferences arrive with the notification module.
          </EmptyPanel>
        </aside>
      </div>

      <IntegrationNotice
        title="Your account is live; field data is still being connected"
        owner="Dev 2, Dev 3 and Dev 4"
        inline
      >
        Authentication, profile, donations and KPI reporting are backed by the Dev 1 API. Crisis
        reports, shelters, resources and AI recommendations will fill the remaining cards as their
        owners ship their modules.
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
