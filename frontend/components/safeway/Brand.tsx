"use client";

import Link from "next/link";
import { useT } from "@/lib/i18n";
import { Icon } from "./Icon";

export function Brand({
  light = false,
  href = "/",
  className = "",
  ariaLabel
}: {
  light?: boolean;
  href?: string;
  className?: string;
  ariaLabel?: string;
}) {
  const t = useT();
  return (
    <Link
      href={href}
      className={`brand ${light ? "brand-light" : ""} ${className}`.trim()}
      aria-label={ariaLabel ?? t("shell.brandHomeAria")}
    >
      <span className="brand-mark">
        <Icon name="heart" size={18} />
      </span>
      <span>SAFEWAY</span>
    </Link>
  );
}
