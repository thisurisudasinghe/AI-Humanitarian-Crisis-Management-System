import Link from "next/link";
import { Icon } from "@/components/safeway/Icon";
import { NetworkArtwork } from "@/components/safeway/NetworkArtwork";

const trustPoints: [string, string, string][] = [
  ["shield", "Responsible reporting", "Information handled with care"],
  ["brain", "Human oversight", "AI supports, people decide"],
  ["heart", "Built for people", "Clear support at every step"]
];

const steps: [string, string, string, string][] = [
  ["01", "Share what is happening", "A guided report captures the details teams need.", "alert"],
  ["02", "Authorized teams review", "Reports are verified with transparent decision support.", "shield"],
  ["03", "Response is coordinated", "Resources, shelters and field teams move responsibly.", "users"]
];

export default function HomePage() {
  return (
    <main className="public-home">
      <section className="centered-hero public-hero">
        <div className="hero-orbit hero-orbit-left" />
        <div className="hero-orbit hero-orbit-right" />
        <div className="centered-hero-copy">
          <div className="eyebrow">
            <span /> Humanitarian response, connected
          </div>
          <h1>
            <span className="public-hero__line">When crisis happens,</span>{" "}
            <span className="public-hero__line">
              help should be <em>within reach.</em>
            </span>
          </h1>
          <p>
            SAFEWAY connects communities with humanitarian assistance and gives response teams a
            clear, responsible way to coordinate support when it matters most.
          </p>
          <div className="centered-actions">
            <Link className="btn btn-white public-hero__cta" href="/report">
              <Icon name="alert" size={18} />
              Report a Crisis
              <Icon name="arrow" size={18} />
            </Link>
          </div>
          <p className="public-hero__emergency">
            <Icon name="info" size={15} />
            In immediate danger, contact local emergency services first.
          </p>
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

      <div className="public-hero__fade" aria-hidden="true" />

      <section className="home-intro">
        <div>
          <div className="section-kicker">One connected response</div>
          <h2>
            A clearer path from urgent need to <span>coordinated help.</span>
          </h2>
        </div>
        <div>
          <p>
            Emergencies are overwhelming. SAFEWAY gives people asking for help a calm place to
            start and gives authorized teams a shared picture of what is needed.
          </p>
        </div>
      </section>

      <section className="home-pathways" aria-label="Who SAFEWAY serves">
        <article className="pathway pathway-public">
          <span>FOR COMMUNITIES</span>
          <i>
            <Icon name="heart" />
          </i>
          <h3>Find help when you need it.</h3>
          <p>Report urgent needs, see clear guidance and follow updates in plain language.</p>
        </article>
        <article className="pathway pathway-operations">
          <span>FOR AUTHORIZED TEAMS</span>
          <i>
            <Icon name="grid" />
          </i>
          <h3>Coordinate the right response.</h3>
          <p>Review urgent needs, assign resources and keep every action accountable.</p>
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

      <section className="home-cta public-guidance" aria-label="Emergency guidance">
        <span>
          <Icon name="info" size={26} />
        </span>
        <div>
          <small>IF SOMEONE IS IN IMMEDIATE DANGER</small>
          <h2>Contact local emergency services first.</h2>
          <p>
            SAFEWAY supports coordination and access to assistance; it does not replace emergency
            services.
          </p>
        </div>
      </section>
    </main>
  );
}
