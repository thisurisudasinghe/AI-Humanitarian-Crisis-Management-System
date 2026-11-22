import Link from "next/link";
import { Brand } from "@/components/safeway/Brand";
import { Icon } from "@/components/safeway/Icon";

export default function ForbiddenPage() {
  return (
    <main className="forbidden-page">
      <div>
        <Brand />
        <span className="badge coral" style={{ marginTop: 26 }}>
          <i /> 403 · Forbidden
        </span>
        <h1>This workspace is not available for your role.</h1>
        <p>
          Your account is signed in, but it does not hold one of the roles this page requires. Ask a
          system administrator if you believe you should have access.
        </p>
        <Link className="btn btn-primary" href="/dashboard">
          Back to dashboard <Icon name="arrow" />
        </Link>
      </div>
    </main>
  );
}
