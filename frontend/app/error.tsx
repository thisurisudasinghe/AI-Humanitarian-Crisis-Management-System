"use client";

import { useEffect } from "react";
import { Button } from "@/components/ui";

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
    <main className="auth-page">
      <div className="card auth-card">
        <h1>Something went wrong</h1>
        <p>Please try again. If the problem continues, contact an administrator.</p>
        <Button onClick={reset}>Try again</Button>
      </div>
    </main>
  );
}
