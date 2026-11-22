"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";
import { useAuth } from "@/lib/auth";
import { isStaff } from "@/lib/roles";
import { Icon } from "./Icon";
import { PublicBrand } from "./PublicBrand";

const publicLinks = [
  { label: "Home", href: "/", icon: "home" },
  { label: "About", href: "/about", icon: "info" },
  { label: "Services", href: "/services", icon: "layers" },
  { label: "How It Works", href: "/how-it-works", icon: "route" },
  { label: "Contact", href: "/contact", icon: "mail" },
  { label: "Help", href: "/find-assistance", icon: "help-circle" }
] as const;

export function PublicNav() {
  const pathname = usePathname();
  const { user, ready } = useAuth();
  const [open, setOpen] = useState(false);

  const workspaceHref =
    ready && user ? (isStaff(user.roles) ? "/operations" : "/dashboard") : "/login";
  const workspaceLabel = ready && user ? "Dashboard" : "Log in";

  useEffect(() => {
    setOpen(false);
  }, [pathname]);

  useEffect(() => {
    if (!open) return;

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") setOpen(false);
    }

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [open]);

  function isActive(href: string) {
    if (href === "/") return pathname === "/";
    return pathname === href || pathname.startsWith(`${href}/`);
  }

  return (
    <header className="platform-nav public-nav">
      <PublicBrand compact />

      <button
        className="platform-menu public-menu"
        type="button"
        aria-label={open ? "Close navigation menu" : "Open navigation menu"}
        aria-controls="public-navigation"
        aria-expanded={open}
        onClick={() => setOpen((value) => !value)}
      >
        <Icon name={open ? "close" : "menu"} size={22} />
      </button>

      <div id="public-navigation" className={`public-nav__panel${open ? " is-open" : ""}`}>
        <nav className="platform-links public-nav__links" aria-label="Primary navigation">
          {publicLinks.map((link) => {
            const active = isActive(link.href);
            return (
              <Link
                key={link.href}
                href={link.href}
                className={active ? "active" : ""}
                aria-current={active ? "page" : undefined}
                onClick={() => setOpen(false)}
              >
                <Icon name={link.icon} size={16} />
                <span>{link.label}</span>
              </Link>
            );
          })}
        </nav>

        <div className="platform-actions public-nav__actions">
          <Link className="platform-login public-action public-action--login" href={workspaceHref}>
            <Icon name="user" size={17} />
            <span>{workspaceLabel}</span>
          </Link>
          <Link className="btn btn-primary public-action public-action--report" href="/report">
            <Icon name="alert" size={17} />
            <span>Report a Crisis</span>
          </Link>
        </div>
      </div>
    </header>
  );
}
