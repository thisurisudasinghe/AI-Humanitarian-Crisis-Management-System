"use client";

import { Suspense, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { VerificationCard } from "@/components/safeway/VerificationCard";
import { PublicFooter } from "@/components/safeway/PublicFooter";
import { PublicNav } from "@/components/safeway/PublicNav";
import { verifyEmailToken } from "@/lib/verification";
import {
  verificationView,
  type VerificationView
} from "@/lib/verification-state";

function ConfirmVerification() {
  const searchParams = useSearchParams();
  const token = searchParams.get("token") ?? "";
  const [view, setView] = useState<VerificationView>(verificationView("loading"));
  const [busy, setBusy] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function confirm() {
      if (!token) {
        if (!cancelled) {
          setView(verificationView("invalid"));
          setBusy(false);
        }
        return;
      }

      const result = await verifyEmailToken(token);
      if (!cancelled) {
        setView(result);
        setBusy(false);
      }
    }

    void confirm();
    return () => {
      cancelled = true;
    };
  }, [token]);

  return (
    <div className="auth-refined">
      <PublicNav />
      <main className="verification-layout">
        <VerificationCard view={view} showResend={!busy} busy={busy} />
      </main>
      <PublicFooter />
    </div>
  );
}

export default function ConfirmVerificationPage() {
  return (
    <Suspense
      fallback={
        <div className="auth-refined">
          <PublicNav />
          <main className="verification-layout">
            <VerificationCard view={verificationView("loading")} busy />
          </main>
          <PublicFooter />
        </div>
      }
    >
      <ConfirmVerification />
    </Suspense>
  );
}
