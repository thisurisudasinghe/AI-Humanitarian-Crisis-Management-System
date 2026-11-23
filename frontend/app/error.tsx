"use client";

import { useEffect } from "react";
import { Brand } from "@/components/safeway/Brand";
import { Icon } from "@/components/safeway/Icon";
import { useT } from "@/lib/i18n";

export default function ErrorPage({
  error,
  reset
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  const t = useT();
  useEffect(() => {
    console.error("Unhandled UI error", error);
  }, [error]);

  return (
    <main className="error-page">
      <div>
        <Brand />
        <span className="badge coral" style={{ marginTop: 26 }}>
          <i /> {t("common.somethingWrong")}
        </span>
        <h1>{t("common.errorTitle")}</h1>
        <p>{t("common.errorCopy")}</p>
        <button className="btn btn-primary" type="button" onClick={reset}>
          {t("common.retry")} <Icon name="arrow" />
        </button>
      </div>
    </main>
  );
}
