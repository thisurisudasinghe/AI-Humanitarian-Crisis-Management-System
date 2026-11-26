"use client";

import { VerificationCard } from "@/components/safeway/VerificationCard";
import { PublicFooter } from "@/components/safeway/PublicFooter";
import { PublicNav } from "@/components/safeway/PublicNav";
import type { VerificationView } from "@/lib/verification-state";

const pendingView: VerificationView = {
  state: "error",
  tone: "warning",
  title: "Check your inbox",
  message:
    "We sent a Safeway verification link to your email address. Open it within 30 minutes to activate your account. If you cannot find it, request a fresh link below.",
  canResend: true
};

export default function VerifyEmailPage() {
  return (
    <div className="auth-refined">
      <PublicNav />
      <main className="verification-layout">
        <VerificationCard view={pendingView} showResend />
      </main>
      <PublicFooter />
    </div>
  );
}
