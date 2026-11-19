import { Skeleton } from "@/components/ui";

export default function Loading() {
  return (
    <main className="page">
      <Skeleton rows={6} />
    </main>
  );
}
