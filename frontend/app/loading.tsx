"use client";

import { Brand } from "@/components/safeway/Brand";
import { useT } from "@/lib/i18n";

export default function Loading() {
  const t = useT();
  return (
    <main className="loading-screen">
      <div>
        <Brand />
        <div className="skeleton-block" style={{ marginTop: 24 }} />
        <p>{t("common.loadingSafeway")}</p>
      </div>
    </main>
  );
}
