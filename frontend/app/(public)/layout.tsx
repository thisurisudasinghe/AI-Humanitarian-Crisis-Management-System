import type { ReactNode } from "react";
import { PublicFooter } from "@/components/safeway/PublicFooter";
import { PublicNav } from "@/components/safeway/PublicNav";
import "./public.css";
import "./public-workflows.css";

export default function PublicLayout({ children }: { children: ReactNode }) {
  return (
    <div className="site public-shell">
      <PublicNav />
      {children}
      <PublicFooter />
    </div>
  );
}
