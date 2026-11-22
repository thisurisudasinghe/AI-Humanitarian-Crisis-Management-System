"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useCallback, useEffect, useState, type FormEvent, type ReactNode } from "react";
import { useAuth } from "@/lib/auth";
import { initials, humanizeRole } from "@/lib/format";
import { getInbox } from "@/lib/notifications";
import { RoleNames, hasAnyRole, isStaff } from "@/lib/roles";
import { Brand } from "./Brand";
import { Icon } from "./Icon";

type NavItem = {
  href: string;
  icon: string;
  label: string;
};

const publicNav: NavItem[] = [
  { href: "/dashboard", icon: "grid", label: "Dashboard" },
  { href: "/my-reports", icon: "file", label: "My reports" },
  { href: "/find-assistance", icon: "search", label: "Find assistance" },
  { href: "/notifications", icon: "bell", label: "Notifications" },
  { href: "/account", icon: "user", label: "Account settings" }
];

const opsNav: NavItem[] = [
  { href: "/operations", icon: "grid", label: "Overview" },
  { href: "/operations/crises", icon: "alert", label: "Crises & cases" },
  { href: "/operations/ai", icon: "brain", label: "AI decision support" },
  { href: "/operations/decisions", icon: "file", label: "Decision history" },
  { href: "/operations/map", icon: "map", label: "Live map" },
  { href: "/resources", icon: "box", label: "Resources" },
  { href: "/shelters", icon: "home", label: "Shelters" },
  { href: "/volunteers", icon: "users", label: "Volunteers" },
  { href: "/notifications", icon: "bell", label: "Notifications" },
  { href: "/kpis", icon: "chart", label: "Reports & KPIs" }
];

const publicOnlyPaths = ["/dashboard", "/my-reports", "/account"];

const kpiRoles = [
  RoleNames.SystemAdmin,
  RoleNames.EmergencyCoordinator,
  RoleNames.PartnerOfficer
];

const donationRoles = [
  RoleNames.SystemAdmin,
  RoleNames.DonorOfficer,
  RoleNames.EmergencyCoordinator,
  RoleNames.PartnerOfficer
];

function isOpsPath(pathname: string, roles: readonly string[]): boolean {
  if (publicOnlyPaths.includes(pathname)) {
    return false;
  }

  if (pathname.startsWith("/operations") || pathname.startsWith("/resources")) {
    return true;
  }

  if (
    pathname.startsWith("/shelters") ||
    pathname.startsWith("/volunteers") ||
    pathname.startsWith("/kpis") ||
    pathname.startsWith("/donations") ||
    pathname.startsWith("/admin")
  ) {
    return true;
  }

  return isStaff(roles);
}

function isActive(pathname: string, href: string): boolean {
  if (href === "/operations" || href === "/dashboard") {
    return pathname === href;
  }

  return pathname === href || pathname.startsWith(`${href}/`);
}

export function DashboardShell({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const { user, logout } = useAuth();
  const [mobileNav, setMobileNav] = useState(false);
  const [search, setSearch] = useState("");
  const [unreadCount, setUnreadCount] = useState(0);

  const loadUnreadCount = useCallback(async () => {
    try {
      const inbox = await getInbox({ limit: 1 });
      setUnreadCount(inbox.unreadCount);
    } catch {
      // The shell stays quiet when the inbox cannot be loaded; the page handles errors.
      setUnreadCount(0);
    }
  }, []);

  useEffect(() => {
    void loadUnreadCount();
  }, [loadUnreadCount, pathname]);

  useEffect(() => {
    const refresh = () => void loadUnreadCount();
    window.addEventListener("cms:notifications-changed", refresh);
    window.addEventListener("focus", refresh);

    return () => {
      window.removeEventListener("cms:notifications-changed", refresh);
      window.removeEventListener("focus", refresh);
    };
  }, [loadUnreadCount]);

  const bellLabel =
    unreadCount > 0 ? `Notifications (${unreadCount} unread)` : "Notifications";

  const bell = (
    <span className="notification-bell">
      <Icon name="bell" />
      {unreadCount > 0 ? (
        <span className="notification-badge" aria-hidden="true">
          {unreadCount > 9 ? "9+" : unreadCount}
        </span>
      ) : null}
    </span>
  );

  const roles = user?.roles ?? [];
  const mode = isOpsPath(pathname, roles) ? "ops" : "public";
  const admin = roles.includes(RoleNames.SystemAdmin);
  const donations = hasAnyRole(roles, donationRoles);

  const navItems =
    mode === "ops"
      ? opsNav.filter(
          (item) => item.href !== "/kpis" || hasAnyRole(roles, kpiRoles)
        )
      : publicNav;
  const displayName = user?.fullName ?? "Signed in";
  const roleLabel = mode === "ops" ? humanizeRole(roles[0]) : "Public account";

  async function onLogout() {
    await logout();
    router.replace("/login");
  }

  function onSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const query = search.trim();
    router.push(query ? `/operations/crises?q=${encodeURIComponent(query)}` : "/operations/crises");
    setMobileNav(false);
  }

  return (
    <div className={`dashboard-shell ${mode}`}>
      <a className="skip-link" href="#main-content">
        Skip to main content
      </a>

      <aside className={`sidebar ${mobileNav ? "open" : ""}`}>
        <div className="sidebar-brand">
          <Brand light />
          <button type="button" onClick={() => setMobileNav(false)} aria-label="Close navigation">
            <Icon name="close" />
          </button>
        </div>

        {mode === "ops" ? (
          <div className="command-label">
            <span>COMMAND CENTRE</span>
            <small>Regional Operations</small>
          </div>
        ) : null}

        <nav aria-label="Main navigation">
          {navItems.map((item) => (
            <Link
              key={`${item.href}-${item.label}`}
              href={item.href}
              className={isActive(pathname, item.href) ? "active" : ""}
              onClick={() => setMobileNav(false)}
            >
              <Icon name={item.icon} />
              <span>{item.label}</span>
            </Link>
          ))}
          {mode === "ops" && donations ? (
            <Link
              href="/donations"
              className={isActive(pathname, "/donations") ? "active" : ""}
              onClick={() => setMobileNav(false)}
            >
              <Icon name="heart" />
              <span>Donations</span>
            </Link>
          ) : null}
        </nav>

        <div className="side-bottom">
          {mode === "ops" && admin ? (
            <Link href="/admin/users" onClick={() => setMobileNav(false)}>
              <Icon name="shield" />
              <span>Administration</span>
            </Link>
          ) : null}
          <button type="button" onClick={() => void onLogout()}>
            <Icon name="logout" />
            <span>Sign out</span>
          </button>
          <div className="profile">
            <span>{initials(displayName)}</span>
            <div>
              <b>{displayName}</b>
              <small>{roleLabel}</small>
            </div>
            <Icon name="chevron" size={16} />
          </div>
        </div>
      </aside>

      <div className="dash-content">
        <header className="dash-mobile-header">
          <button type="button" onClick={() => setMobileNav(true)} aria-label="Open navigation">
            <Icon name="menu" />
          </button>
          <Brand />
          <Link
            href="/notifications"
            aria-label={bellLabel}
            onClick={() => void loadUnreadCount()}
          >
            {bell}
          </Link>
        </header>

        {mode === "ops" ? (
          <header className="ops-header">
            <div>
              <span className="status-dot" /> Systems operational
            </div>
            <form onSubmit={onSearch}>
              <label>
                <Icon name="search" />
                <input
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Search cases, locations, resources..."
                  aria-label="Search cases, locations and resources"
                />
              </label>
            </form>
            <Link
              href="/notifications"
              aria-label={bellLabel}
              onClick={() => void loadUnreadCount()}
            >
              {bell}
            </Link>
            <div className="profile-mini">{initials(displayName)}</div>
          </header>
        ) : null}

        <main className="dashboard-main" id="main-content">
          {children}
        </main>
      </div>
    </div>
  );
}
