import Link from "next/link";
import type { ReactNode } from "react";
import { Icon } from "./Icon";

export function PortalHeader({
  eyebrow,
  title,
  description,
  action
}: {
  eyebrow: string;
  title: string;
  description: string;
  action?: ReactNode;
}) {
  return (
    <header className="portal-header">
      <div>
        <p className="portal-eyebrow">
          <span /> {eyebrow}
        </p>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      {action ? <div className="portal-header-action">{action}</div> : null}
    </header>
  );
}

export function PortalStats({
  items
}: {
  items: { label: string; value: string | number; hint?: string; icon: string; tone?: string }[];
}) {
  return (
    <div className="portal-stats">
      {items.map((item) => (
        <article className={`portal-stat ${item.tone ?? ""}`} key={item.label}>
          <i>
            <Icon name={item.icon} size={18} />
          </i>
          <span>
            <b>{item.value}</b>
            <small>{item.label}</small>
            {item.hint ? <em>{item.hint}</em> : null}
          </span>
        </article>
      ))}
    </div>
  );
}

export function PortalSection({
  title,
  description,
  action,
  children,
  className = ""
}: {
  title: string;
  description?: string;
  action?: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  return (
    <section className={`portal-section ${className}`.trim()}>
      <div className="portal-section-head">
        <div>
          <h2>{title}</h2>
          {description ? <p>{description}</p> : null}
        </div>
        {action ? <div className="portal-section-action">{action}</div> : null}
      </div>
      {children}
    </section>
  );
}

export function PortalState({
  kind,
  title,
  children,
  href,
  linkLabel
}: {
  kind: "loading" | "empty" | "error";
  title: string;
  children: ReactNode;
  href?: string;
  linkLabel?: string;
}) {
  return (
    <div className={`portal-state portal-state--${kind}`} role={kind === "error" ? "alert" : "status"}>
      <i>
        <Icon name={kind === "error" ? "info" : kind === "loading" ? "clock" : "check"} size={18} />
      </i>
      <div>
        <b>{title}</b>
        <p>{children}</p>
        {href && linkLabel ? <Link href={href}>{linkLabel} →</Link> : null}
      </div>
    </div>
  );
}

export function PortalNotice({
  children,
  tone = "success"
}: {
  children: ReactNode;
  tone?: "success" | "error";
}) {
  return (
    <div className={`portal-notice portal-notice--${tone}`} role={tone === "error" ? "alert" : "status"}>
      <Icon name={tone === "error" ? "info" : "check"} size={16} />
      <span>{children}</span>
    </div>
  );
}
