import Link from "next/link";
import { Brand } from "./Brand";

export function PublicFooter() {
  return (
    <footer>
      <div className="footer-main">
        <div>
          <Brand light />
          <p>
            Connecting people to help.
            <br />
            Coordinating action. Restoring hope.
          </p>
        </div>
        <div>
          <b>Explore</b>
          <Link href="/about">About SAFEWAY</Link>
          <Link href="/how-it-works">How it works</Link>
          <Link href="/services">Services</Link>
        </div>
        <div>
          <b>Get help</b>
          <Link href="/report">Report a crisis</Link>
          <Link href="/find-assistance">Find assistance</Link>
          <Link href="/my-reports">My reports</Link>
        </div>
        <div>
          <b>Contact</b>
          <Link href="/contact">Support centre</Link>
          <Link href="/contact">Accessibility</Link>
          <Link href="/contact">Privacy &amp; terms</Link>
        </div>
      </div>
      <div className="footer-bottom">
        <span>© 2025 SAFEWAY. Prototype experience.</span>
        <span>Blue for Trust. White for Clarity. Green for Hope.</span>
      </div>
    </footer>
  );
}
