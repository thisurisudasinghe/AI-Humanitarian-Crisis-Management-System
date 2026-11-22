import type { Metadata } from "next";
import Link from "next/link";
import { Icon } from "@/components/safeway/Icon";
import { PublicCaseUpdates } from "@/components/safeway/PublicCaseUpdates";

export const metadata: Metadata = {
  title: "Find assistance · SAFEWAY",
  description: "Explore shelter, food, healthcare and transport support through SAFEWAY."
};

const assistance: [string, string, string][] = [
  ["Emergency shelter", "Verified safe spaces, occupancy and accessibility information.", "home"],
  ["Food & water", "Distribution points, opening hours and current availability.", "box"],
  ["Medical support", "Clinics, first-aid points and urgent care guidance.", "heart"],
  ["Transport", "Safe routes and transport support for evacuation or appointments.", "map"]
];

export default function FindAssistancePage() {
  return (
    <main className="public-info">
      <header className="page-hero">
        <div className="page-hero-lines" />
        <div className="eyebrow">
          <span /> Nearby support
        </div>
        <h1>Find verified assistance near you.</h1>
        <p>
          Shelters, food, healthcare and essential resources will appear here as live availability
          becomes connected.
        </p>
      </header>

      <div className="page-body">
        <aside className="public-availability-note" role="note">
          <span>
            <Icon name="info" size={20} />
          </span>
          <div>
            <b>Verified public information</b>
            <p>
              Published alerts and active service centres are shown below when available. For
              individual shelter allocation or case status, use the signed-in citizen portal.
            </p>
          </div>
        </aside>

        <section className="value-grid" aria-label="Assistance categories">
          {assistance.map(([title, copy, icon]) => (
            <article key={title}>
              <i>
                <Icon name={icon} />
              </i>
              <h3>{title}</h3>
              <p>{copy}</p>
            </article>
          ))}
        </section>

        <PublicCaseUpdates />

        <section className="service-choice public-single-choice">
          <div>
            <small>NEED URGENT HELP?</small>
            <h2>Report what is happening so a response team can review it.</h2>
          </div>
          <Link className="btn btn-primary" href="/report">
            Report a Crisis <Icon name="arrow" />
          </Link>
        </section>
      </div>
    </main>
  );
}
