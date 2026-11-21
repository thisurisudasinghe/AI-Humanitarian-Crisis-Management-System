import type { ReactNode } from "react";
import { Icon } from "./Icon";

/**
 * Explains that a module is intentionally not wired yet.
 * Used instead of inventing endpoints that the backend does not expose.
 */
export function IntegrationNotice({
  title,
  owner,
  children,
  inline = false
}: {
  title: string;
  owner: string;
  children: ReactNode;
  inline?: boolean;
}) {
  return (
    <div
      className={inline ? "integration-notice integration-notice--inline" : "integration-notice"}
      role="note"
    >
      <Icon name="shield" size={18} />
      <div>
        <b>{title}</b>
        <p>{children}</p>
        <small>Owned by {owner} · API not yet available</small>
      </div>
    </div>
  );
}

export function EmptyPanel({
  title,
  children,
  owner
}: {
  title: string;
  children: ReactNode;
  owner?: string;
}) {
  return (
    <div className="empty-panel">
      <b>{title}</b>
      <p>{children}</p>
      {owner ? <small>Pending {owner}</small> : null}
    </div>
  );
}
