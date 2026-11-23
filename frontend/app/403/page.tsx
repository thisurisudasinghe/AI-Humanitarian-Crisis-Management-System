"use client";

import Link from "next/link";
import { Brand } from "@/components/safeway/Brand";
import { Icon } from "@/components/safeway/Icon";
import { useT } from "@/lib/i18n";

export default function ForbiddenPage() {
  const t = useT();
  return (
    <main className="forbidden-page">
      <div>
        <Brand />
        <span className="badge coral" style={{ marginTop: 26 }}>
          <i /> {t("common.forbiddenBadge")}
        </span>
        <h1>{t("common.forbiddenTitle")}</h1>
        <p>{t("common.forbiddenCopy")}</p>
        <Link className="btn btn-primary" href="/dashboard">
          {t("common.backToDashboard")} <Icon name="arrow" />
        </Link>
      </div>
    </main>
  );
}
