import Link from "next/link";
import { Brand } from "@/components/safeway/Brand";
import { Icon } from "@/components/safeway/Icon";

export default function NotFound() {
  return (
    <main className="error-page">
      <div>
        <Brand />
        <span className="badge blue" style={{ marginTop: 26 }}>
          <i /> 404 · Not found
        </span>
        <h1>We couldn&apos;t find that page.</h1>
        <p>
          The page you requested does not exist or has moved. Return to the homepage, or open your
          workspace to continue where you left off.
        </p>
        <div className="account-actions" style={{ justifyContent: "center" }}>
          <Link className="btn btn-primary" href="/">
            Return to homepage <Icon name="arrow" />
          </Link>
          <Link className="btn btn-outline" href="/dashboard">
            Open dashboard
          </Link>
        </div>
      </div>
    </main>
  );
}
