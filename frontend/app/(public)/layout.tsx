import type { ReactNode } from "react";
import { PublicFooter } from "@/components/safeway/PublicFooter";
import { PublicNav } from "@/components/safeway/PublicNav";

export default function PublicLayout({ children }: { children: ReactNode }) {
  return (
    <div className="site">
      <PublicNav />
      {children}
      <PublicFooter />
    </div>
  );
}
