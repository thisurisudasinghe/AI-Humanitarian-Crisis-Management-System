import { Brand } from "@/components/safeway/Brand";

export default function Loading() {
  return (
    <main className="loading-screen">
      <div>
        <Brand />
        <div className="skeleton-block" style={{ marginTop: 24 }} />
        <p>Loading SAFEWAY…</p>
      </div>
    </main>
  );
}
