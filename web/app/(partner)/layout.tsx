"use client";

import { useEffect, useState } from "react";

function isPartnerOfficer(token: string | null): boolean {
  if (!token) {
    return false;
  }

  try {
    const payload = token.split(".")[1];
    if (!payload) {
      return false;
    }
    const base64 = payload.replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64 + "=".repeat((4 - (base64.length % 4)) % 4);
    const decoded = atob(padded);
    const claims = JSON.parse(decoded) as { role?: string | string[] };
    const roles = Array.isArray(claims.role) ? claims.role : [claims.role];
    return roles.includes("PartnerOfficer");
  } catch {
    return false;
  }
}

export default function PartnerLayout({ children }: { children: React.ReactNode }) {
  const [allowed, setAllowed] = useState<boolean | null>(null);

  useEffect(() => {
    const token = typeof window !== "undefined" ? localStorage.getItem("accessToken") : null;
    setAllowed(isPartnerOfficer(token));
  }, []);

  return (
    <div>
      <nav aria-label="Partner workspace">
        <a href="/partner/cases">Shared cases</a>
        <a href="/alerts">Public alerts</a>
      </nav>
      {allowed === null ? <p role="status">Checking partner access…</p> : null}
      {allowed === false ? (
        <section className="panel" aria-labelledby="partner-access-heading">
          <h1 id="partner-access-heading">Partner access required</h1>
          <p>Sign in with a PartnerOfficer account to view cases shared with your organisation.</p>
          <p className="muted">All case data is also protected by the API organisation-scope check.</p>
        </section>
      ) : null}
      {allowed ? children : null}
    </div>
  );
}
