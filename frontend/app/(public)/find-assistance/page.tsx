import type { Metadata } from "next";
import Link from "next/link";
import { Icon } from "@/components/safeway/Icon";
import { IntegrationNotice } from "@/components/safeway/IntegrationNotice";

export const metadata: Metadata = {
  title: "Find assistance · SAFEWAY",
  description: "Explore shelters, food, healthcare and verified resources near you."
};

const assistance: [string, string, string][] = [
  ["Emergency shelter", "Verified safe spaces, occupancy and accessibility information.", "home"],
  ["Food & water", "Distribution points, opening hours and current availability.", "box"],
  ["Medical support", "Clinics, first-aid points and urgent care guidance.", "heart"],
  ["Transport", "Safe routes and transport support for evacuation or appointments.", "map"]
];

export default function FindAssistancePage() {
  return (
    <main className="public-page">
      <header className="page-hero">
        <div className="page-hero-lines" />
        <div className="eyebrow">
          <span /> Nearby support
        </div>
        <h1>Find verified assistance near you.</h1>
        <p>
          Shelters, food, healthcare and essential resources will appear here as soon as the
          logistics and shelter modules publish live availability.
        </p>
      </header>

      <div className="page-body">
        <IntegrationNotice title="Live assistance data is not connected yet" owner="Dev 4 — logistics & shelter network">
          The public directory is ready for the Shared Contract v3/v4 shelter and resource
          endpoints. Until those controllers ship, no availability figures are shown, so the page
          never presents invented capacity or stock levels.
        </IntegrationNotice>

        <section className="value-grid" aria-label="Assistance categories">
          {assistance.map(([title, copy, icon]) => (
            <article key={title}>
              <i>
                <Icon name={icon} />
              </i>
              <h3>{title}</h3>
              <p>{copy}</p>
              <span className="badge amber" style={{ marginTop: 14 }}>
                <i /> Awaiting live data
              </span>
            </article>
          ))}
        </section>

        <section className="service-choice" style={{ marginTop: 40 }}>
          <div>
            <small>NEED URGENT HELP?</small>
            <h2>Report what is happening so a response team can review it.</h2>
          </div>
          <Link className="btn btn-primary" href="/report">
            Report a crisis <Icon name="arrow" />
          </Link>
          <Link className="btn btn-outline" href="/contact">
            Contact support
          </Link>
        </section>
      </div>
    </main>
  );
}
