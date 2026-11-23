"use client";

import Link from "next/link";
import { Brand } from "@/components/safeway/Brand";
import { Icon } from "@/components/safeway/Icon";
import { useT } from "@/lib/i18n";

export default function NotFound() {
  const t = useT();
  return (
    <main className="error-page">
      <div>
        <Brand />
        <span className="badge blue" style={{ marginTop: 26 }}>
          <i /> {t("common.notFoundBadge")}
        </span>
        <h1>{t("common.notFoundTitle")}</h1>
        <p>{t("common.notFoundCopy")}</p>
        <div className="account-actions" style={{ justifyContent: "center" }}>
          <Link className="btn btn-primary" href="/">
            {t("common.returnHome")} <Icon name="arrow" />
          </Link>
          <Link className="btn btn-outline" href="/dashboard">
            {t("common.openDashboard")}
          </Link>
        </div>
      </div>
    </main>
  );
}
