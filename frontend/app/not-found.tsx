import Link from "next/link";

export default function NotFound() {
  return (
    <main className="auth-page">
      <div className="card auth-card">
        <h1>Page not found</h1>
        <p>The page you requested does not exist or has moved.</p>
        <Link href="/dashboard">Return to dashboard</Link>
      </div>
    </main>
  );
}
