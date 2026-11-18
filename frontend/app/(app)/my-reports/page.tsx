"use client";

import Link from "next/link";
import { useState } from "react";
import { Icon } from "@/components/safeway/Icon";
import { EmptyPanel, IntegrationNotice } from "@/components/safeway/IntegrationNotice";

const filters: [string, string][] = [
  ["All reports", "file"],
  ["In progress", "clock"],
  ["Assistance provided", "check"]
];

export default function MyReportsPage() {
  const [filter, setFilter] = useState("All reports");

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

      <IntegrationNotice title="Report tracking is waiting on crisis intake" owner="Dev 2 — crisis & case management">
        The report list, status milestones and filters are ready. They stay empty until the crisis
        and case endpoints can return your submissions.
      </IntegrationNotice>

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
              <b>—</b>
              <small>{label}</small>
            </span>
          </button>
        ))}
      </div>

      <section className="reports-detail-list">
        <EmptyPanel title={`No ${filter.toLowerCase()} to show`} owner="Dev 2 — crisis & case management">
          Once crisis reporting is connected, each submission will appear here with its reference,
          review milestone and assistance status.
        </EmptyPanel>
      </section>

      <div className="report-help-note">
        <Icon name="shield" />
        <span>
          <b>Need to add important information?</b>
          <small>
            Open a report and use “Add an update” rather than creating a duplicate report.
          </small>
        </span>
      </div>
    </>
  );
}
