"use client";

import Link from "next/link";
import { useT } from "@/lib/i18n";
import { Icon } from "./Icon";

export function Brand({ light = false }: { light?: boolean }) {
  const t = useT();
  return (
    <Link href="/" className={`brand ${light ? "brand-light" : ""}`} aria-label={t("shell.brandHomeAria")}>
      <span className="brand-mark">
        <Icon name="heart" size={18} />
      </span>
      <span>SAFEWAY</span>
    </Link>
  );
}
