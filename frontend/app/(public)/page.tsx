import Link from "next/link";
import { Icon } from "@/components/safeway/Icon";
import { NetworkArtwork } from "@/components/safeway/NetworkArtwork";

const trustPoints: [string, string, string][] = [
  ["shield", "Protected reporting", "Information handled responsibly"],
  ["brain", "AI-assisted", "Always under human oversight"],
  ["heart", "Built for people", "Clear support at every step"]
];

const steps: [string, string, string, string][] = [
  ["01", "Share what is happening", "A guided report captures the details teams need.", "alert"],
  ["02", "Authorized teams review", "Reports are verified with transparent decision support.", "shield"],
  ["03", "Response is coordinated", "Resources, shelters and field teams move responsibly.", "users"]
];

export default function HomePage() {
  return (
    <main>
      <section className="centered-hero">
        <div className="hero-orbit hero-orbit-left" />
        <div className="hero-orbit hero-orbit-right" />
        <div className="centered-hero-copy">
          <div className="eyebrow">
            <span /> Humanitarian response, connected
          </div>
          <h1>
            When crisis happens, help should be <em>within reach.</em>
          </h1>
          <p>
            SAFEWAY connects communities with humanitarian assistance and gives response teams a
            clear, responsible way to coordinate support when it matters most.
          </p>
          <div className="centered-actions">
            <Link className="btn btn-white" href="/report">
              Report a crisis <Icon name="arrow" />
            </Link>
            <Link className="btn btn-ghost" href="/find-assistance">
              Find assistance
            </Link>
          </div>
        </div>
        <NetworkArtwork />
        <div className="hero-trust-bar">
          {trustPoints.map(([icon, title, copy]) => (
            <span key={title}>
              <Icon name={icon} />
              <b>{title}</b>
              <small>{copy}</small>
            </span>
          ))}
        </div>
      </section>

      <section className="home-intro">
        <div>
          <div className="section-kicker">One connected response</div>
          <h2>
            A clearer path from urgent need to <span>coordinated help.</span>
          </h2>
        </div>
        <div>
          <p>
            Emergencies are already overwhelming. SAFEWAY creates a calm, accessible bridge between
            people asking for help and authorized teams organizing a response.
          </p>
          <Link className="inline-link" href="/about">
            Discover SAFEWAY <Icon name="arrow" size={17} />
          </Link>
        </div>
      </section>

      <section className="home-pathways">
        <article className="pathway pathway-public">
          <span>FOR COMMUNITIES</span>
          <i>
            <Icon name="heart" />
          </i>
          <h3>Find the right help, without the confusion.</h3>
          <p>Report a crisis, locate verified support, and follow updates in plain language.</p>
          <Link href="/services">
            Explore public services <Icon name="arrow" />
          </Link>
        </article>
        <article className="pathway pathway-operations">
          <span>FOR RESPONSE TEAMS</span>
          <i>
            <Icon name="grid" />
          </i>
          <h3>Turn information into accountable action.</h3>
          <p>Verify needs, coordinate resources, review recommendations and track response.</p>
          <Link href="/login">
            Authorized access <Icon name="arrow" />
          </Link>
        </article>
      </section>

      <section className="home-steps">
        <div className="home-steps-heading">
          <div className="section-kicker light">How help moves</div>
          <h2>
            Three clear steps.
            <br />
            One responsible response.
          </h2>
          <Link href="/how-it-works">
            See the full process <Icon name="arrow" size={16} />
          </Link>
        </div>
        {steps.map(([number, title, copy, icon]) => (
          <article key={number}>
            <span>{number}</span>
            <i>
              <Icon name={icon} />
            </i>
            <h3>{title}</h3>
            <p>{copy}</p>
          </article>
        ))}
      </section>

      <section className="home-cta">
        <span>
          <Icon name="heart" size={30} />
        </span>
        <div>
          <small>WHENEVER YOU ARE READY</small>
          <h2>Help begins with one clear step.</h2>
        </div>
        <Link className="btn btn-white" href="/report">
          Report a crisis <Icon name="arrow" />
        </Link>
      </section>
    </main>
  );
}
