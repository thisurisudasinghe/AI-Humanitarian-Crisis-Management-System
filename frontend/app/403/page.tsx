import Link from "next/link";

export default function ForbiddenPage() {
  return (
    <main className="auth-page">
      <div className="card auth-card">
        <h1>403 — Forbidden</h1>
        <p>Your account does not have permission to view this page.</p>
        <Link href="/dashboard">Return to dashboard</Link>
      </div>
    </main>
  );
}
