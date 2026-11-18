"use client";

import Link from "next/link";
import { Icon } from "@/components/safeway/Icon";
import { EmptyPanel, IntegrationNotice } from "@/components/safeway/IntegrationNotice";

export default function NotificationsPage() {
  return (
    <>
      <div className="reports-page-head">
        <div>
          <p>NOTIFICATIONS</p>
          <h1>Updates that matter to you</h1>
          <span>Report progress, nearby assistance and account messages in one place.</span>
        </div>
        <Link className="btn btn-outline" href="/contact">
          <Icon name="shield" /> Notification help
        </Link>
      </div>

      <IntegrationNotice title="Notification delivery is not connected yet" owner="Member 3 — M9 notifications">
        The notification module owns delivery, read state and channel preferences. Until it ships,
        this page intentionally shows no unread counts or fabricated messages.
      </IntegrationNotice>

      <section className="ops-card" style={{ padding: 24 }}>
        <div className="card-heading">
          <div>
            <h3>Inbox</h3>
            <p>Messages will be grouped by report, assistance and account updates.</p>
          </div>
        </div>
        <EmptyPanel title="Inbox is empty" owner="Member 3 — M9 notifications">
          Nothing is pre-seeded here. When the notification API is available, unread items and
          their timestamps will render in this card.
        </EmptyPanel>
      </section>
    </>
  );
}
