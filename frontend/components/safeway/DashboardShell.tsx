"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useCallback, useEffect, useState, type FormEvent, type ReactNode } from "react";
import { useAuth } from "@/lib/auth";
import { useT } from "@/lib/i18n";
import { initials, humanizeRole } from "@/lib/format";
import { getInbox } from "@/lib/notifications";
import { RoleNames, hasAnyRole, hasRole, isStaff } from "@/lib/roles";
import { LanguageSwitcher } from "@/components/i18n/LanguageSwitcher";
import { Brand } from "./Brand";
import { Icon } from "./Icon";

type NavItem = {
  href: string;
  icon: string;
  labelKey: string;
};

const publicNav: NavItem[] = [
  { href: "/dashboard", icon: "grid", labelKey: "dashboard.dashboard" },
  { href: "/portal/citizen", icon: "file", labelKey: "dashboard.citizenPortal" },
  { href: "/my-reports", icon: "file", labelKey: "dashboard.myReports" },
  { href: "/find-assistance", icon: "search", labelKey: "dashboard.findAssistance" },
  { href: "/notifications", icon: "bell", labelKey: "dashboard.notifications" },
  { href: "/account", icon: "user", labelKey: "dashboard.accountSettings" }
];

const portalSectionLabels: Record<string, string> = {
  "/dashboard": "dashboard.dashboard",
  "/portal/citizen": "dashboard.citizenPortal",
  "/portal/volunteer": "dashboard.volunteerPortal",
  "/portal/donor": "dashboard.donorPortal",
  "/my-reports": "dashboard.myReports",
  "/find-assistance": "dashboard.findAssistance",
  "/notifications": "dashboard.notifications",
  "/account": "dashboard.accountSettings"
};

const opsNav: NavItem[] = [
  { href: "/operations", icon: "grid", labelKey: "dashboard.overview" },
  { href: "/operations/crises", icon: "alert", labelKey: "dashboard.crisesCases" },
  { href: "/operations/ai", icon: "brain", labelKey: "dashboard.aiDecisionSupport" },
  { href: "/operations/decisions", icon: "file", labelKey: "dashboard.decisionHistory" },
  { href: "/operations/map", icon: "map", labelKey: "dashboard.liveMap" },
  { href: "/resources", icon: "box", labelKey: "dashboard.resources" },
  { href: "/shelters", icon: "home", labelKey: "dashboard.shelters" },
  { href: "/volunteers", icon: "users", labelKey: "dashboard.volunteers" },
  { href: "/notifications", icon: "bell", labelKey: "dashboard.notifications" },
  { href: "/kpis", icon: "chart", labelKey: "dashboard.reportsKpis" }
];

const publicOnlyPaths = ["/dashboard", "/my-reports", "/account"];
const publicOnlyPrefixes = ["/portal"];

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
  if (
    publicOnlyPaths.includes(pathname) ||
    publicOnlyPrefixes.some((prefix) => pathname.startsWith(prefix))
  ) {
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
  const t = useT();
  const [mobileNav, setMobileNav] = useState(false);
  const [search, setSearch] = useState("");
  const [unreadCount, setUnreadCount] = useState(0);

  const loadUnreadCount = useCallback(async () => {
    try {
      const inbox = await getInbox({ limit: 1 });
      setUnreadCount(inbox.unreadCount);
    } catch {
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
    unreadCount > 0 ? `${t("dashboard.notifications")} (${unreadCount})` : t("dashboard.notifications");

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

  const navItems: NavItem[] =
    mode === "ops"
      ? opsNav.filter(
          (item) => item.href !== "/kpis" || hasAnyRole(roles, kpiRoles)
        )
      : [
          publicNav[0],
          ...(hasRole(roles, RoleNames.Volunteer)
            ? [{ href: "/portal/volunteer", icon: "users", labelKey: "dashboard.volunteerPortal" }]
            : []),
          ...(hasRole(roles, RoleNames.Donor)
            ? [{ href: "/portal/donor", icon: "heart", labelKey: "dashboard.donorPortal" }]
            : []),
          ...publicNav.slice(1)
        ];
  const displayName = user?.fullName ?? t("common.loading");
  const roleLabel = mode === "ops" ? humanizeRole(roles[0], t) : t("dashboard.publicAccount");

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

  const sectionLabelKey = portalSectionLabels[pathname] ?? "dashboard.publicAccount";

  return (
    <div className={`dashboard-shell ${mode}`}>
      <a className="skip-link" href="#main-content">
        {t("common.skipToMain")}
      </a>

      <aside className={`sidebar ${mobileNav ? "open" : ""}`}>
        <div className="sidebar-brand">
          <Brand light />
          <button type="button" onClick={() => setMobileNav(false)} aria-label={t("common.closeNavigation")}>
            <Icon name="close" />
          </button>
        </div>

        {mode === "ops" ? (
          <div className="command-label">
            <span>{t("dashboard.commandCentre")}</span>
            <small>{t("dashboard.regionalOps")}</small>
          </div>
        ) : null}

        <nav aria-label={t("dashboard.mainNav")}>
          {navItems.map((item) => (
            <Link
              key={`${item.href}-${item.labelKey}`}
              href={item.href}
              className={isActive(pathname, item.href) ? "active" : ""}
              onClick={() => setMobileNav(false)}
            >
              <Icon name={item.icon} />
              <span>{t(item.labelKey)}</span>
            </Link>
          ))}
          {mode === "ops" && donations ? (
            <Link
              href="/donations"
              className={isActive(pathname, "/donations") ? "active" : ""}
              onClick={() => setMobileNav(false)}
            >
              <Icon name="heart" />
              <span>{t("dashboard.donations")}</span>
            </Link>
          ) : null}
        </nav>

        <div className="side-bottom">
          <div style={{ padding: "8px 12px" }}><LanguageSwitcher variant="nav" /></div>
          <Link href="/" onClick={() => setMobileNav(false)}>
            <Icon name="home" />
            <span>{t("dashboard.home")}</span>
          </Link>
          <Link href="/account" onClick={() => setMobileNav(false)}>
            <Icon name="user" />
            <span>{t("dashboard.profile")}</span>
          </Link>
          {mode === "ops" && admin ? (
            <Link href="/admin/users" onClick={() => setMobileNav(false)}>
              <Icon name="shield" />
              <span>{t("dashboard.administration")}</span>
            </Link>
          ) : null}
          <button type="button" onClick={() => void onLogout()}>
            <Icon name="logout" />
            <span>{t("common.logout")}</span>
          </button>
          <Link className="profile" href="/account" onClick={() => setMobileNav(false)}>
            <span>{initials(displayName)}</span>
            <div>
              <b>{displayName}</b>
              <small>{roleLabel}</small>
            </div>
            <Icon name="chevron" size={16} />
          </Link>
        </div>
      </aside>

      <div className="dash-content">
        <header className="dash-mobile-header">
          <button type="button" onClick={() => setMobileNav(true)} aria-label={t("common.openNavigation")}>
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
              <span className="status-dot" /> {t("dashboard.systemsOperational")}
            </div>
            <form onSubmit={onSearch}>
              <label>
                <Icon name="search" />
                <input
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder={t("dashboard.searchPlaceholder")}
                  aria-label={t("dashboard.searchAria")}
                />
              </label>
            </form>
            <div className="ops-header-links">
              <Link href="/">{t("dashboard.home")}</Link>
              <Link href="/account">{t("dashboard.profile")}</Link>
            </div>
            <LanguageSwitcher variant="nav" className="dash-lang-switcher" />
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

        {mode === "public" ? (
          <header className="public-topbar">
            <div className="public-topbar-title">
              <small>{t("dashboard.publicPortal")}</small>
              <b>{t(sectionLabelKey)}</b>
            </div>
            <nav aria-label={t("dashboard.accountNav")}>
              <Link href="/">{t("dashboard.home")}</Link>
              {pathname !== "/dashboard" ? (
                <Link href="/dashboard">{t("common.backToDashboard")}</Link>
              ) : null}
              <Link href="/account">{t("dashboard.profile")}</Link>
              <LanguageSwitcher variant="light" className="dash-lang-switcher" />
              <Link
                href="/notifications"
                aria-label={bellLabel}
                onClick={() => void loadUnreadCount()}
              >
                {bell}
              </Link>
              <button type="button" onClick={() => void onLogout()}>
                <Icon name="logout" size={16} /> {t("common.logout")}
              </button>
            </nav>
          </header>
        ) : null}

        <main className="dashboard-main" id="main-content">
          {children}
        </main>
      </div>
    </div>
  );
}
