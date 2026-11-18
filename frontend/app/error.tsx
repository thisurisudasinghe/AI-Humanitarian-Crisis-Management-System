"use client";

import { useEffect } from "react";
import { Brand } from "@/components/safeway/Brand";
import { Icon } from "@/components/safeway/Icon";

export default function ErrorPage({
  error,
  reset
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error("Unhandled UI error", error);
  }, [error]);

  return (
    <main className="error-page">
      <div>
        <Brand />
        <span className="badge coral" style={{ marginTop: 26 }}>
          <i /> Something went wrong
        </span>
        <h1>We couldn&apos;t finish loading this page.</h1>
        <p>
          Try again. If the problem continues, contact an administrator and include the time it
          happened.
        </p>
        <button className="btn btn-primary" type="button" onClick={reset}>
          Try again <Icon name="arrow" />
        </button>
      </div>
    </main>
  );
}
