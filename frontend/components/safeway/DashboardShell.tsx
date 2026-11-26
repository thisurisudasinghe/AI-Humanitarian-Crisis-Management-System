"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type ReactNode
} from "react";
import { useAuth } from "@/lib/auth";
import { initials } from "@/lib/format";
import { useT } from "@/lib/i18n";
import { getInbox } from "@/lib/notifications";
import {
  RoleNames,
  StaffRoles,
  hasAnyRole,
  homePathForRoles,
  isStaff
} from "@/lib/roles";
import { Brand } from "./Brand";
import { Icon } from "./Icon";

type NavLink = {
  href: string;
  icon: string;
  labelKey: string;
  roles: readonly string[];
  separated?: boolean;
};

type NavGroup = {
  id: string;
  icon: string;
  labelKey: string;
  links: NavLink[];
};

const caseStaff = [RoleNames.EmergencyCoordinator, RoleNames.CaseManager];
const volunteerStaff = [
  RoleNames.VolunteerCoordinator,
  RoleNames.EmergencyCoordinator,
  RoleNames.SystemAdmin
];
const donationStaff = [
  RoleNames.SystemAdmin,
  RoleNames.DonorOfficer,
  RoleNames.EmergencyCoordinator,
  RoleNames.PartnerOfficer
];
const logisticsStaff = [
  RoleNames.LogisticsManager,
  RoleNames.EmergencyCoordinator,
  RoleNames.SystemAdmin
];
const shelterStaff = [
  RoleNames.ShelterAdmin,
  RoleNames.EmergencyCoordinator,
  RoleNames.SystemAdmin
];
const mapStaff = [
  RoleNames.LogisticsManager,
  RoleNames.ShelterAdmin,
  RoleNames.VolunteerCoordinator,
  RoleNames.EmergencyCoordinator,
  RoleNames.SystemAdmin
];
const kpiStaff = [
  RoleNames.SystemAdmin,
  RoleNames.EmergencyCoordinator,
  RoleNames.PartnerOfficer
];
const deciderStaff = [
  RoleNames.CaseManager,
  RoleNames.EmergencyCoordinator,
  RoleNames.LogisticsManager,
  RoleNames.ShelterAdmin,
  RoleNames.VolunteerCoordinator
];
const adminStaff = [RoleNames.SystemAdmin];

/** The eight configured staff navigation groups. Empty groups are not rendered. */
const navGroups: NavGroup[] = [
  {
    id: "overview",
    icon: "grid",
    labelKey: "opsX.nav.overview",
    links: [
      {
        href: "/operations",
        icon: "grid",
        labelKey: "opsX.nav.overview",
        roles: StaffRoles
      }
    ]
  },
  {
    id: "response",
    icon: "alert",
    labelKey: "opsX.nav.response",
    links: [
      {
        href: "/operations/crises",
        icon: "file",
        labelKey: "opsX.nav.crisesCases",
        roles: caseStaff
      },
      {
        href: "/operations/crises?status=Submitted",
        icon: "clipboard",
        labelKey: "opsX.nav.assistanceRequests",
        roles: caseStaff
      },
      {
        href: "/public-alerts",
        icon: "megaphone",
        labelKey: "opsX.nav.publicAlerts",
        roles: [RoleNames.EmergencyCoordinator]
      }
    ]
  },
  {
    id: "aid",
    icon: "handHeart",
    labelKey: "opsX.nav.aid",
    links: [
      {
        href: "/volunteers",
        icon: "users",
        labelKey: "opsX.nav.volunteers",
        roles: volunteerStaff
      },
      {
        href: "/donations",
        icon: "heart",
        labelKey: "opsX.nav.donations",
        roles: donationStaff
      }
    ]
  },
  {
    id: "resources",
    icon: "box",
    labelKey: "opsX.nav.resources",
    links: [
      {
        href: "/resources",
        icon: "box",
        labelKey: "opsX.nav.resourcesLink",
        roles: logisticsStaff
      },
      {
        href: "/shelters",
        icon: "home",
        labelKey: "opsX.nav.shelters",
        roles: shelterStaff
      }
    ]
  },
  {
    id: "map",
    icon: "map",
    labelKey: "opsX.nav.map",
    links: [
      {
        href: "/operations/map",
        icon: "map",
        labelKey: "opsX.nav.liveMap",
        roles: mapStaff
      }
    ]
  },
  {
    id: "ai",
    icon: "brain",
    labelKey: "opsX.nav.ai",
    links: [
      {
        href: "/operations/ai",
        icon: "brain",
        labelKey: "opsX.nav.ai",
        roles: StaffRoles
      },
      {
        href: "/operations/decisions",
        icon: "clipboard",
        labelKey: "opsX.nav.decisions",
        roles: deciderStaff
      }
    ]
  },
  {
    id: "reports",
    icon: "chart",
    labelKey: "opsX.nav.reports",
    links: [
      {
        href: "/reports",
        icon: "chart",
        labelKey: "opsX.nav.reportsKpis",
        roles: kpiStaff
      }
    ]
  },
  {
    id: "administration",
    icon: "shield",
    labelKey: "opsX.nav.administration",
    links: [
      {
        href: "/admin",
        icon: "shield",
        labelKey: "opsX.nav.adminLanding",
        roles: adminStaff
      },
      {
        href: "/admin/users",
        icon: "users",
        labelKey: "common.adminUsers",
        roles: adminStaff
      },
      {
        href: "/admin/organisations",
        icon: "building",
        labelKey: "common.adminOrgs",
        roles: adminStaff
      },
      {
        href: "/admin/announcements",
        icon: "megaphone",
        labelKey: "common.adminAnnouncements",
        roles: adminStaff
      },
      {
        href: "/admin/audit",
        icon: "clipboard",
        labelKey: "common.adminAudit",
        roles: adminStaff
      },
      {
        href: "/admin/notifications",
        icon: "bell",
        labelKey: "common.adminFailedNotifications",
        roles: adminStaff
      },
      {
        href: "/admin/config",
        icon: "shield",
        labelKey: "common.adminConfig",
        roles: adminStaff
      },
      {
        href: "/admin/outbox",
        icon: "mail",
        labelKey: "common.adminOutbox",
        roles: adminStaff,
        separated: true
      }
    ]
  }
];

function isActive(pathname: string, href: string): boolean {
  const [base] = href.split("?");

  // Query-specific links are shortcuts, not canonical active routes.
  if (href.includes("?")) return false;
  if (base === "/reports") {
    return pathname === "/reports" || pathname === "/kpis";
  }
  if (base === "/operations" || base === "/admin") {
    return pathname === base;
  }

  return pathname === base || pathname.startsWith(`${base}/`);
}

export function DashboardShell({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const { user, logout } = useAuth();
  const t = useT();
  const [mobileNav, setMobileNav] = useState(false);
  const [collapsed, setCollapsed] = useState(false);
  const [openGroups, setOpenGroups] = useState<string[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const mobileTriggerRef = useRef<HTMLButtonElement>(null);
  const drawerRef = useRef<HTMLElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);

  const roles = user?.roles ?? [];
  const rolesKey = roles.join("|");
  const staffMode = isStaff(roles);
  const homeHref = homePathForRoles(roles);
  const displayName = user?.fullName ?? t("common.loading");
  const roleKeys: string[] = [
    RoleNames.EmergencyCoordinator,
    RoleNames.CaseManager,
    RoleNames.LogisticsManager,
    RoleNames.ShelterAdmin,
    RoleNames.VolunteerCoordinator,
    RoleNames.DonorOfficer,
    RoleNames.PartnerOfficer,
    RoleNames.SystemAdmin
  ];
  const activeRole = roles.find((role) => roleKeys.includes(role as never)) ?? roles[0];
  const roleLabel = activeRole ? t(`roleLabels.${activeRole}`) : t("dashboard.commandCentre");

  const visibleGroups = navGroups
    .map((group) => ({
      ...group,
      links: group.links.filter((link) => hasAnyRole(roles, link.roles))
    }))
    .filter((group) => group.links.length > 0);

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

  useEffect(() => {
    try {
      setCollapsed(window.localStorage.getItem("safeway.sidebarCollapsed") === "1");
    } catch {
      // Storage may be disabled; the expanded layout remains usable.
    }
  }, []);

  useEffect(() => {
    setMobileNav(false);
    const activeGroup = navGroups.find((group) =>
      group.links.some((link) => isActive(pathname, link.href))
    );
    if (activeGroup && hasAnyRole(roles, activeGroup.links.flatMap((link) => [...link.roles]))) {
      setOpenGroups((current) =>
        current.includes(activeGroup.id) ? current : [...current, activeGroup.id]
      );
    }
    // rolesKey changes only when the authenticated user's roles change.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pathname, rolesKey]);

  useEffect(() => {
    if (!mobileNav || !staffMode) return;

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    closeButtonRef.current?.focus();

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        setMobileNav(false);
        return;
      }

      if (event.key !== "Tab" || !drawerRef.current) return;
      const focusable = Array.from(
        drawerRef.current.querySelectorAll<HTMLElement>(
          'a[href], button:not([disabled]), [tabindex]:not([tabindex="-1"])'
        )
      );
      if (focusable.length === 0) return;
      const first = focusable[0];
      const last = focusable[focusable.length - 1];

      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };

    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("keydown", onKeyDown);
      document.body.style.overflow = previousOverflow;
      mobileTriggerRef.current?.focus();
    };
  }, [mobileNav, staffMode]);

  const bellLabel =
    unreadCount > 0
      ? `${t("dashboard.notifications")} (${unreadCount})`
      : t("dashboard.notifications");

  const bell = (
    <span className="notification-bell">
      <Icon name="bell" size={18} />
      {unreadCount > 0 ? (
        <span className="notification-badge" aria-hidden="true">
          {unreadCount > 9 ? "9+" : unreadCount}
        </span>
      ) : null}
    </span>
  );

  function toggleCollapsed() {
    setCollapsed((current) => {
      const next = !current;
      try {
        window.localStorage.setItem("safeway.sidebarCollapsed", next ? "1" : "0");
      } catch {
        // Storage may be disabled; the current session still updates.
      }
      return next;
    });
  }

  function toggleGroup(id: string) {
    if (collapsed && !mobileNav) {
      setCollapsed(false);
      setOpenGroups((current) => (current.includes(id) ? current : [...current, id]));
      return;
    }
    setOpenGroups((current) =>
      current.includes(id) ? current.filter((item) => item !== id) : [...current, id]
    );
  }

  function closeMobileNav() {
    setMobileNav(false);
  }

  async function onLogout() {
    await logout();
    router.replace("/login");
  }

  const sidebar = (
    <aside
      ref={drawerRef}
      className={`sidebar ${mobileNav ? "sidebar--open" : ""} ${
        collapsed && !mobileNav ? "sidebar--collapsed" : ""
      }`}
      aria-label={t("dashboard.mainNav")}
    >
      <div className="sidebar-brand">
        <Brand light href={homeHref} />
        <button
          ref={closeButtonRef}
          type="button"
          className="sidebar-mobile-close"
          onClick={closeMobileNav}
          aria-label={t("common.closeNavigation")}
        >
          <Icon name="close" size={18} />
        </button>
        <button
          type="button"
          className="sidebar-collapse"
          onClick={toggleCollapsed}
          aria-label={t(collapsed ? "opsX.nav.expandSidebar" : "opsX.nav.collapseSidebar")}
          aria-expanded={!collapsed}
        >
          <Icon name="chevron" size={16} />
        </button>
      </div>

      <nav aria-label={t("dashboard.mainNav")} className="sidebar-groups">
        {visibleGroups.map((group) => {
          const groupActive = group.links.some((link) => isActive(pathname, link.href));

          // Single-destination groups are rendered as a plain navigation item;
          // they do not need an expandable accordion trigger.
          if (group.links.length === 1) {
            const link = group.links[0];
            const active = isActive(pathname, link.href);
            return (
              <div className={`sidebar-group ${groupActive ? "sidebar-group--active" : ""}`} key={group.id}>
                <Link
                  href={link.href}
                  className={`sidebar-group__link ${active ? "sidebar-group__link--active" : ""}`}
                  aria-current={active ? "page" : undefined}
                  onClick={closeMobileNav}
                  title={t(link.labelKey)}
                >
                  <Icon name={link.icon} size={15} />
                  <span>{t(link.labelKey)}</span>
                </Link>
              </div>
            );
          }

          const expanded = openGroups.includes(group.id) || groupActive;
          return (
            <div className={`sidebar-group ${groupActive ? "sidebar-group--active" : ""}`} key={group.id}>
              <button
                type="button"
                className="sidebar-group__trigger"
                onClick={() => toggleGroup(group.id)}
                aria-expanded={expanded}
                aria-controls={`sidebar-group-${group.id}`}
              >
                <Icon name={group.icon} size={18} />
                <span>{t(group.labelKey)}</span>
                <span className="sidebar-group__chevron">
                  <Icon name="chevron" size={14} />
                </span>
              </button>
              {expanded ? (
                <div className="sidebar-group__panel" id={`sidebar-group-${group.id}`}>
                  {group.links.map((link) => (
                    <span key={link.href}>
                      {link.separated ? (
                        <span className="sidebar-group__divider" aria-hidden="true" />
                      ) : null}
                      <Link
                        href={link.href}
                        className={`sidebar-group__link ${
                          isActive(pathname, link.href) ? "sidebar-group__link--active" : ""
                        }`}
                        aria-current={isActive(pathname, link.href) ? "page" : undefined}
                        onClick={closeMobileNav}
                        title={t(link.labelKey)}
                      >
                        <Icon name={link.icon} size={15} />
                        <span>{t(link.labelKey)}</span>
                      </Link>
                    </span>
                  ))}
                </div>
              ) : null}
            </div>
          );
        })}
      </nav>

      <div className="sidebar-footer">
        <span className="sidebar-footer__avatar" title={displayName}>
          {initials(displayName)}
        </span>
        <span className="sidebar-footer__identity">
          <b>{displayName}</b>
          <small>{roleLabel}</small>
        </span>
      </div>
    </aside>
  );

  return (
    <div className={`dashboard-shell ${staffMode ? "ops" : "public"}`}>
      <a className="skip-link" href="#main-content">
        {t("common.skipToMain")}
      </a>

      <header className="app-topbar">
        <div className="app-topbar__inner">
          {staffMode ? (
            <button
              ref={mobileTriggerRef}
              type="button"
              className="app-topbar__menu"
              onClick={() => setMobileNav(true)}
              aria-label={t("common.openNavigation")}
              aria-expanded={mobileNav}
            >
              <Icon name="menu" size={20} />
            </button>
          ) : null}

          <Brand href={homeHref} className="app-topbar__brand" />

          <nav className="app-topbar__nav" aria-label={t("dashboard.accountNav")}>
            <Link href={homeHref} className="app-topbar__link" aria-label={t("dashboard.home")}>
              <Icon name="home" size={17} />
              <span>{t("dashboard.home")}</span>
            </Link>
            <Link href="/account" className="app-topbar__link" aria-label={t("dashboard.profile")}>
              <Icon name="user" size={17} />
              <span>{t("dashboard.profile")}</span>
            </Link>
            <Link
              href="/notifications"
              className="app-topbar__link"
              aria-label={bellLabel}
              onClick={() => void loadUnreadCount()}
            >
              {bell}
              <span>{t("dashboard.notifications")}</span>
            </Link>
            <button
              type="button"
              className="app-topbar__link"
              onClick={() => void onLogout()}
              aria-label={t("common.logout")}
            >
              <Icon name="logout" size={17} />
              <span>{t("common.logout")}</span>
            </button>
          </nav>
        </div>
      </header>

      {staffMode ? (
        <>
          {mobileNav ? (
            <button
              type="button"
              className="sidebar-scrim"
              onClick={closeMobileNav}
              aria-label={t("common.closeNavigation")}
            />
          ) : null}
          {sidebar}
        </>
      ) : null}

      <div className={`dashboard-main-wrap ${staffMode ? "dashboard-main-wrap--ops" : ""}`}>
        <main
          className={`dashboard-main ${staffMode ? "" : "dashboard-main--public"}`}
          id="main-content"
        >
          {children}
        </main>
      </div>
    </div>
  );
}
