"use client";

import Link from "next/link";
import { useState } from "react";
import { Icon } from "./Icon";
import { NetworkArtwork } from "./NetworkArtwork";

export type InfoKind = "about" | "services" | "how" | "contact";

const infoContent: Record<InfoKind, [string, string, string]> = {
  about: [
    "About SAFEWAY",
    "Technology that keeps people at the centre.",
    "SAFEWAY makes access to help clearer for communities and response work more coordinated for authorized teams."
  ],
  services: [
    "Humanitarian services",
    "Practical support, connected in one place.",
    "Public assistance and authorized response tools work together through a single, consistent experience."
  ],
  how: [
    "How it works",
    "From a report to a responsible response.",
    "Every step stays clear for people in need and structured for the teams responsible for acting."
  ],
  contact: [
    "Contact & support",
    "A clear place to ask questions.",
    "For general platform questions, use this support centre. In immediate danger, contact local emergency services first."
  ]
};

export function InfoPageView({ kind }: { kind: InfoKind }) {
  const [eyebrow, title, intro] = infoContent[kind];

  return (
    <main className="public-info">
      <header className="page-hero">
        <div className="page-hero-lines" />
        <div className="eyebrow">
          <span /> {eyebrow}
        </div>
        <h1>{title}</h1>
        <p>{intro}</p>
      </header>

      {kind === "about" ? <AboutBody /> : null}
      {kind === "services" ? <ServicesBody /> : null}
      {kind === "how" ? <HowBody /> : null}
      {kind === "contact" ? <ContactBody /> : null}
    </main>
  );
}

function AboutBody() {
  return (
    <div className="page-body">
      <section className="mission-grid">
        <div>
          <div className="section-kicker">Our purpose</div>
          <h2>Make urgent help easier to reach and responsible action easier to coordinate.</h2>
        </div>
        <div>
          <p>
            A person in crisis should know what to do without technical knowledge. Every workflow
            favors clear language, reassurance and meaningful feedback.
          </p>
          <p>
            For response organizations, SAFEWAY creates shared situational awareness while keeping
            human review in charge of critical decisions.
          </p>
        </div>
      </section>

      <section className="value-grid" aria-label="SAFEWAY values">
        {(
          [
            ["Clarity", "Simple language and focused actions reduce uncertainty.", "eye"],
            ["Responsibility", "Authorized people review and approve critical actions.", "shield"],
            ["Connection", "Reports, resources and teams share one clear picture.", "map"],
            ["Hope", "Progress and positive outcomes stay visible.", "heart"]
          ] as const
        ).map(([title, copy, icon]) => (
          <article key={title}>
            <i>
              <Icon name={icon} />
            </i>
            <h3>{title}</h3>
            <p>{copy}</p>
          </article>
        ))}
      </section>

      <section className="human-oversight">
        <NetworkArtwork />
        <div>
          <div className="section-kicker light">Responsible intelligence</div>
          <h2>AI supports. People decide.</h2>
          <p>
            SAFEWAY can organize information and highlight risk patterns, but trained and
            accountable people remain responsible for verification, approval and action.
          </p>
        </div>
      </section>
    </div>
  );
}

function ServicesBody() {
  const services: [string, string, string, string][] = [
    ["Emergency reporting", "Share urgent needs through a calm, step-by-step report.", "alert", "PUBLIC"],
    ["Verified assistance", "Find shelter, resources and current safety guidance.", "search", "PUBLIC"],
    ["Report tracking", "Follow review, assignment and assistance updates.", "file", "PUBLIC"],
    ["Crisis coordination", "Verify incidents, assign teams and monitor progress.", "grid", "AUTHORIZED"],
    ["Resources & logistics", "Track inventory, allocations and delivery status.", "box", "AUTHORIZED"],
    ["Decision support", "Review risk signals with mandatory human oversight.", "brain", "AUTHORIZED"]
  ];

  return (
    <div className="page-body">
      <section className="service-catalog" aria-label="SAFEWAY services">
        {services.map(([title, copy, icon, access], index) => (
          <article key={title}>
            <span>0{index + 1}</span>
            <i>
              <Icon name={icon} />
            </i>
            <small>{access} ACCESS</small>
            <h2>{title}</h2>
            <p>{copy}</p>
          </article>
        ))}
      </section>

      <section className="service-choice">
        <div>
          <small>NEED SUPPORT?</small>
          <h2>Start with the action that fits your situation.</h2>
        </div>
        <Link className="btn btn-primary" href="/report">
          Report a Crisis <Icon name="arrow" />
        </Link>
      </section>
    </div>
  );
}

function HowBody() {
  const steps: [string, string, string, string, string][] = [
    ["01", "Report", "Share what happened", "Add the category, location and immediate needs.", "alert"],
    ["02", "Verify", "Information is reviewed", "Authorized officers check what is needed for safe action.", "shield"],
    ["03", "Assess", "Decision support clarifies priority", "Transparent recommendations support human reviewers.", "brain"],
    ["04", "Coordinate", "Teams and resources are assigned", "Current logistics and location information guide action.", "users"],
    ["05", "Update", "Progress stays understandable", "People see clear status changes and relevant guidance.", "bell"]
  ];

  return (
    <div className="page-body">
      <section className="process-list" aria-label="Response process">
        {steps.map(([number, label, title, copy, icon]) => (
          <article key={number}>
            <span>{number}</span>
            <i>
              <Icon name={icon} />
            </i>
            <div>
              <small>{label}</small>
              <h2>{title}</h2>
              <p>{copy}</p>
            </div>
          </article>
        ))}
      </section>

      <section className="process-cta">
        <div>
          <Icon name="shield" size={30} />
          <span>
            <small>IMPORTANT PRINCIPLE</small>
            <h2>Critical decisions always remain under human review.</h2>
          </span>
        </div>
        <Link className="btn btn-white" href="/report">
          Report a Crisis <Icon name="arrow" />
        </Link>
      </section>
    </div>
  );
}

function ContactBody() {
  const [sent, setSent] = useState(false);

  return (
    <div className="page-body contact-layout">
      <section className="contact-options">
        {(
          [
            [
              "phone",
              "IMMEDIATE DANGER",
              "Contact emergency services",
              "SAFEWAY does not replace local emergency telephone services."
            ],
            [
              "file",
              "EXISTING REPORT",
              "Have your reference ready",
              "Include your SAFEWAY reference when contacting support."
            ],
            [
              "shield",
              "PRIVACY & ACCESS",
              "Your information is protected",
              "Only authorized teams can access operational details."
            ]
          ] as const
        ).map(([icon, label, title, copy]) => (
          <article key={label}>
            <i>
              <Icon name={icon} />
            </i>
            <span>
              <small>{label}</small>
              <h3>{title}</h3>
              <p>{copy}</p>
            </span>
          </article>
        ))}
      </section>

      <section className="contact-form">
        {sent ? (
          <div className="contact-success" aria-live="polite">
            <i>
              <Icon name="check" size={30} />
            </i>
            <h2>Your message was not sent.</h2>
            <p>
              Support messaging is not connected to a backend yet, so nothing was transmitted. In an
              emergency, contact local emergency services.
            </p>
            <button type="button" onClick={() => setSent(false)}>
              Write another message
            </button>
          </div>
        ) : (
          <form
            onSubmit={(event) => {
              event.preventDefault();
              setSent(true);
            }}
          >
            <div className="section-kicker">Send a message</div>
            <h2>How can we help?</h2>
            <div className="two-fields">
              <label>
                <span className="field-label">First name</span>
                <input placeholder="Your first name" autoComplete="given-name" required />
              </label>
              <label>
                <span className="field-label">Last name</span>
                <input placeholder="Your last name" autoComplete="family-name" required />
              </label>
            </div>
            <label>
              <span className="field-label">Email address</span>
              <input type="email" placeholder="you@example.org" autoComplete="email" required />
            </label>
            <label>
              <span className="field-label">What do you need help with?</span>
              <select defaultValue="General question">
                <option>General question</option>
                <option>Existing report</option>
                <option>Account access</option>
              </select>
            </label>
            <label>
              <span className="field-label">Message</span>
              <textarea rows={5} placeholder="Tell us how we can help..." required />
            </label>
            <button className="btn btn-primary" type="submit">
              Send message <Icon name="arrow" />
            </button>
          </form>
        )}
      </section>
    </div>
  );
}
