import Link from "next/link";
import { Icon } from "./Icon";

export function Brand({ light = false }: { light?: boolean }) {
  return (
    <Link href="/" className={`brand ${light ? "brand-light" : ""}`} aria-label="SAFEWAY home">
      <span className="brand-mark">
        <Icon name="heart" size={18} />
      </span>
      <span>SAFEWAY</span>
    </Link>
  );
}
