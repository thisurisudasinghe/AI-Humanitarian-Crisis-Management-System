"use client";

import type { ReactNode } from "react";
import { useT } from "@/lib/i18n";
import { Icon } from "./Icon";

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
  const t = useT();
  return (
    <div
      className={inline ? "integration-notice integration-notice--inline" : "integration-notice"}
      role="note"
    >
      <Icon name="shield" size={18} />
      <div>
        <b>{title}</b>
        <p>{children}</p>
        <small>{t("common.ownedBy")} {owner} — {t("common.apiNotAvailable")}</small>
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
  const t = useT();
  return (
    <div className="empty-panel">
      <b>{title}</b>
      <p>{children}</p>
      {owner ? <small>{t("common.pending")} {owner}</small> : null}
    </div>
  );
}
