"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import { useAuth } from "@/lib/auth";
import { isStaff } from "@/lib/roles";
import { Icon } from "./Icon";

const publicLinks = [
  { label: "Home", href: "/" },
  { label: "About", href: "/about" },
  { label: "Services", href: "/services" },
  { label: "How It Works", href: "/how-it-works" },
  { label: "Contact", href: "/contact" }
];

export function PublicNav() {
  const pathname = usePathname();
  const { user, ready } = useAuth();
  const [open, setOpen] = useState(false);

  const workspaceHref =
    ready && user ? (isStaff(user.roles) ? "/operations" : "/dashboard") : "/login";
  const workspaceLabel = ready && user ? "Dashboard" : "Log in";

  function isActive(href: string) {
    if (href === "/") {
      return pathname === "/";
    }
    return pathname === href || pathname.startsWith(`${href}/`);
  }

  return (
    <header className="platform-nav">
      <Link href="/" className="platform-brand">
        <span className="platform-brand-mark">
          <Icon name="heart" size={21} />
        </span>
        <span>
          <b>SAFEWAY</b>
          <small>Humanitarian response network</small>
        </span>
      </Link>

      <nav className={open ? "platform-links open" : "platform-links"}>
        {publicLinks.map((link) => (
          <Link
            key={link.href}
            href={link.href}
            className={isActive(link.href) ? "active" : ""}
            onClick={() => setOpen(false)}
          >
            {link.label}
          </Link>
        ))}
        <Link className="mobile-login" href={workspaceHref} onClick={() => setOpen(false)}>
          {workspaceLabel}
        </Link>
      </nav>

      <div className="platform-actions">
        <Link className="platform-login" href={workspaceHref}>
          <Icon name="user" size={17} /> {workspaceLabel}
        </Link>
        <Link className="btn btn-primary" href="/report">
          Get help / Report a crisis <Icon name="arrow" size={17} />
        </Link>
        <button
          className="platform-menu"
          type="button"
          onClick={() => setOpen((value) => !value)}
          aria-label="Toggle navigation"
          aria-expanded={open}
        >
          <Icon name={open ? "close" : "menu"} />
        </button>
      </div>
    </header>
  );
}
