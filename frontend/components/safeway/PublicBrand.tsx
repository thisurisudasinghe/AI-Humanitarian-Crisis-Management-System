import Link from "next/link";
import { Icon } from "./Icon";

/**
 * Public-only brand lockup. It reuses the canonical heart mark and the
 * established rotated brand tile from the existing SAFEWAY identity.
 */
export function PublicBrand({
  light = false,
  compact = false
}: {
  light?: boolean;
  compact?: boolean;
}) {
  return (
    <Link
      href="/"
      className={`public-brand${light ? " public-brand--light" : ""}${compact ? " public-brand--compact" : ""}`}
      aria-label="SAFEWAY home"
    >
      <span className="public-brand__mark">
        <Icon name="heart" size={compact ? 19 : 22} />
      </span>
      <span className="public-brand__text">
        <span className="public-brand__name">SAFEWAY</span>
        <span className="public-brand__subtitle">Connecting People. Delivering Hope.</span>
      </span>
    </Link>
  );
}
