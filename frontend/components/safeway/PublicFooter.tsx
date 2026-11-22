import Link from "next/link";
import { PublicBrand } from "./PublicBrand";

export function PublicFooter() {
  return (
    <footer className="public-footer">
      <div className="public-footer__main">
        <div className="public-footer__brand">
          <PublicBrand light />
          <p>
            SAFEWAY connects people requesting assistance with authorized humanitarian response and
            coordination services.
          </p>
        </div>

        <nav aria-label="Footer explore links">
          <b>Explore</b>
          <Link href="/about">About SAFEWAY</Link>
          <Link href="/services">Services</Link>
          <Link href="/how-it-works">How it works</Link>
        </nav>

        <nav aria-label="Footer help links">
          <b>Get help</b>
          <Link href="/report">Report a crisis</Link>
          <Link href="/find-assistance">Find assistance</Link>
          <Link href="/contact">Contact</Link>
        </nav>
      </div>

      <div className="public-footer__bottom">
        <span>© 2026 SAFEWAY. Prototype experience.</span>
        <Link href="/contact">Contact support</Link>
      </div>
    </footer>
  );
}
