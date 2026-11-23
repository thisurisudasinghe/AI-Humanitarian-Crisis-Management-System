"use client";

import Link from "next/link";
import { useEffect, useState, type FormEvent } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import {
  PortalHeader,
  PortalNotice,
  PortalSection,
  PortalState,
  PortalStats
} from "@/components/safeway/PortalPrimitives";
import { errorMessage } from "@/lib/api";
import { formatShortDate } from "@/lib/format";
import { getInbox, type NotificationInboxDto } from "@/lib/notifications";
import {
  addVolunteerAvailability,
  completeTask,
  getMyVolunteerTasks,
  getVolunteerPortal,
  respondToTask,
  saveVolunteerProfile,
  type VolunteerPortalData,
  type VolunteerTask
} from "@/lib/portal";
import { RoleNames } from "@/lib/roles";

function jsonArrayToText(value?: string | null): string {
  if (!value) return "";
  try {
    const parsed: unknown = JSON.parse(value);
    if (Array.isArray(parsed)) {
      return parsed
        .filter((item): item is string => typeof item === "string")
        .join(", ");
    }
  } catch {
    return value;
  }
  return value;
}

function textToJson(value: string): string {
  return JSON.stringify(
    value
      .split(",")
      .map((item) => item.trim())
      .filter(Boolean)
  );
}

function skillList(value: string): string[] {
  try {
    const parsed: unknown = JSON.parse(value);
    if (Array.isArray(parsed)) {
      return parsed.filter((item): item is string => typeof item === "string");
    }
  } catch {
    // Fall through to the raw value for legacy records.
  }
  return value ? [value] : [];
}

function optionalNumber(value: string): number | null {
  if (!value.trim()) return null;
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) {
    throw new Error("Latitude and longitude must be valid numbers.");
  }
  return parsed;
}

function toIso(value: string): string {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    throw new Error("Enter a valid date and time.");
  }
  return parsed.toISOString();
}

export default function VolunteerPortalPage() {
  const [portal, setPortal] = useState<VolunteerPortalData | null>(null);
  const [tasks, setTasks] = useState<VolunteerTask[]>([]);
  const [inbox, setInbox] = useState<NotificationInboxDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [skills, setSkills] = useState("");
  const [languages, setLanguages] = useState("");
  const [lat, setLat] = useState("");
  const [lng, setLng] = useState("");
  const [active, setActive] = useState(true);
  const [startsAt, setStartsAt] = useState("");
  const [endsAt, setEndsAt] = useState("");

  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function load(initializeForm = false) {
    const portalRequest = getVolunteerPortal()
      .then((data) => {
        setPortal(data);
        if (initializeForm && data.profile) {
          setSkills(jsonArrayToText(data.profile.skillsJson));
          setLanguages(jsonArrayToText(data.profile.languagesJson));
          setLat(data.profile.lat === null || data.profile.lat === undefined ? "" : String(data.profile.lat));
          setLng(data.profile.lng === null || data.profile.lng === undefined ? "" : String(data.profile.lng));
          setActive(data.profile.active);
        }
      })
      .catch((cause) => {
        setLoadError(errorMessage(cause));
      });

    const taskRequest = getMyVolunteerTasks()
      .then((result) => setTasks(result.items))
      .catch((cause) => {
        setLoadError(errorMessage(cause));
      });

    const inboxRequest = getInbox({ limit: 5 })
      .then((result) => setInbox(result))
      .catch(() => {
        // The notifications section reports its own state below.
      });

    await Promise.all([portalRequest, taskRequest, inboxRequest]);
  }

  useEffect(() => {
    let activeRequest = true;
    setLoading(true);
    void load(true).finally(() => {
      if (activeRequest) setLoading(false);
    });
    return () => {
      activeRequest = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function run(action: () => Promise<unknown>, success: string) {
    setBusy(true);
    setNotice(null);
    setError(null);

    try {
      await action();
      await load(false);
      setNotice(success);
    } catch (cause) {
      setError(errorMessage(cause));
    } finally {
      setBusy(false);
    }
  }

  function onSaveProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void run(async () => {
      const latitude = optionalNumber(lat);
      const longitude = optionalNumber(lng);
      if ((latitude === null) !== (longitude === null)) {
        throw new Error("Enter both latitude and longitude, or leave both blank.");
      }

      await saveVolunteerProfile({
        skillsJson: textToJson(skills),
        languagesJson: textToJson(languages),
        lat: latitude,
        lng: longitude,
        active
      });
    }, "Your volunteer profile was saved.");
  }

  function onAddAvailability(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void run(async () => {
      if (!startsAt || !endsAt) {
        throw new Error("Select a start and end time.");
      }
      const from = toIso(startsAt);
      const to = toIso(endsAt);
      if (Date.parse(to) <= Date.parse(from)) {
        throw new Error("The end time must be after the start time.");
      }
      await addVolunteerAvailability({ startsAt: from, endsAt: to });
      setStartsAt("");
      setEndsAt("");
    }, "Your availability window was added.");
  }

  const verification = portal?.profile?.verificationStatus ?? "Not started";
  const verificationTone =
    verification === "Verified" ? "green" : verification === "Pending" ? "amber" : "coral";

  return (
    <AuthGuard allowedRoles={[RoleNames.Volunteer]}>
      <PortalHeader
        eyebrow="Volunteer portal"
        title="Your volunteer workspace"
        description="Keep your profile current, manage availability and respond to coordination tasks."
        action={
          <Link className="btn btn-outline" href="/notifications">
            <Icon name="bell" /> Notifications
          </Link>
        }
      />

      <PortalStats
        items={[
          {
            label: "Verification",
            value: loading ? "—" : verification,
            hint: verification === "Verified" ? "Eligible for assignment" : "Coordinator review required",
            icon: "shield",
            tone: verificationTone
          },
          {
            label: "Completed tasks",
            value: loading ? "—" : (portal?.completedTasks ?? 0),
            icon: "check",
            tone: "green"
          },
          {
            label: "Open opportunities",
            value: loading ? "—" : (portal?.opportunities.length ?? 0),
            icon: "users",
            tone: "blue"
          },
          {
            label: "Unread messages",
            value: loading ? "—" : (inbox?.unreadCount ?? 0),
            icon: "bell",
            tone: "green"
          }
        ]}
      />

      {loadError ? <PortalNotice tone="error">{loadError}</PortalNotice> : null}
      {notice ? <PortalNotice>{notice}</PortalNotice> : null}
      {error ? <PortalNotice tone="error">{error}</PortalNotice> : null}

      <div className="portal-split-grid">
        <PortalSection
          title="Volunteer profile"
          description="Skills and languages are shared with coordinators for matching."
        >
          {loading ? (
            <PortalState kind="loading" title="Loading profile">
              Reading your volunteer record.
            </PortalState>
          ) : (
            <form className="portal-form" onSubmit={onSaveProfile}>
              <label>
                <span>Skills</span>
                <input
                  value={skills}
                  onChange={(event) => setSkills(event.target.value)}
                  placeholder="Food distribution, First aid"
                />
                <small>Separate each skill with a comma.</small>
              </label>
              <label>
                <span>Languages</span>
                <input
                  value={languages}
                  onChange={(event) => setLanguages(event.target.value)}
                  placeholder="en, si"
                />
                <small>Separate each language with a comma.</small>
              </label>
              <div className="portal-form-row">
                <label>
                  <span>Latitude</span>
                  <input
                    type="number"
                    step="any"
                    value={lat}
                    onChange={(event) => setLat(event.target.value)}
                    placeholder="Optional"
                  />
                </label>
                <label>
                  <span>Longitude</span>
                  <input
                    type="number"
                    step="any"
                    value={lng}
                    onChange={(event) => setLng(event.target.value)}
                    placeholder="Optional"
                  />
                </label>
              </div>
              <label className="portal-checkbox">
                <input
                  type="checkbox"
                  checked={active}
                  onChange={(event) => setActive(event.target.checked)}
                />
                <span>Available for task assignments</span>
              </label>
              <button className="btn btn-primary" type="submit" disabled={busy}>
                {busy ? "Saving…" : "Save profile"}
                {busy ? null : <Icon name="arrow" size={16} />}
              </button>
            </form>
          )}
        </PortalSection>

        <PortalSection
          title="Availability"
          description="Add windows when coordinators can contact you for support."
        >
          {loading ? (
            <PortalState kind="loading" title="Loading availability">
              Checking your saved windows.
            </PortalState>
          ) : (
            <>
              {portal?.availability.length ? (
                <div className="portal-list">
                  {portal.availability.map((window) => (
                    <article className="portal-row" key={window.id}>
                      <i>
                        <Icon name="clock" size={18} />
                      </i>
                      <div>
                        <b>{formatShortDate(window.startsAt)}</b>
                        <p>Until {formatShortDate(window.endsAt)}</p>
                      </div>
                    </article>
                  ))}
                </div>
              ) : (
                <PortalState kind="empty" title="No availability saved">
                  Add a window so coordinators know when you can help.
                </PortalState>
              )}

              <form className="portal-form portal-form--inline" onSubmit={onAddAvailability}>
                <label>
                  <span>Starts</span>
                  <input
                    type="datetime-local"
                    value={startsAt}
                    onChange={(event) => setStartsAt(event.target.value)}
                    required
                  />
                </label>
                <label>
                  <span>Ends</span>
                  <input
                    type="datetime-local"
                    value={endsAt}
                    onChange={(event) => setEndsAt(event.target.value)}
                    required
                  />
                </label>
                <button className="btn btn-outline" type="submit" disabled={busy}>
                  Add window
                </button>
              </form>
            </>
          )}
        </PortalSection>
      </div>

      <div className="portal-split-grid">
        <PortalSection
          title="Assigned tasks"
          description="Accept, decline or complete work assigned by a coordinator."
        >
          {loading ? (
            <PortalState kind="loading" title="Loading tasks">
              Checking your assignments.
            </PortalState>
          ) : tasks.length === 0 ? (
            <PortalState kind="empty" title="No assigned tasks">
              Open opportunities are listed here once a coordinator assigns them to you.
            </PortalState>
          ) : (
            <div className="portal-list">
              {tasks.map((task) => (
                <article className="portal-row portal-row--stacked" key={task.id}>
                  <i>
                    <Icon name="users" size={18} />
                  </i>
                  <div>
                    <b>{task.title}</b>
                    <p>Skills: {skillList(task.requiredSkillsJson).join(", ") || "Not specified"}</p>
                    <small>
                      {task.status}
                      {task.scheduledAt ? ` · ${formatShortDate(task.scheduledAt)}` : ""}
                    </small>
                  </div>
                  <div className="portal-row-actions">
                    {task.status === "Assigned" ? (
                      <>
                        <button
                          className="btn btn-primary btn-small"
                          type="button"
                          disabled={busy}
                          onClick={() => void run(() => respondToTask(task.id, true), "Task accepted.")}
                        >
                          Accept
                        </button>
                        <button
                          className="btn btn-outline btn-small"
                          type="button"
                          disabled={busy}
                          onClick={() => void run(() => respondToTask(task.id, false), "Task declined.")}
                        >
                          Decline
                        </button>
                      </>
                    ) : null}
                    {task.status === "Accepted" ? (
                      <button
                        className="btn btn-primary btn-small"
                        type="button"
                        disabled={busy}
                        onClick={() => void run(() => completeTask(task.id), "Task marked complete.")}
                      >
                        Complete
                      </button>
                    ) : null}
                  </div>
                </article>
              ))}
            </div>
          )}
        </PortalSection>

        <PortalSection
          title="Open opportunities"
          description="Unassigned tasks published by response coordinators."
        >
          {loading ? (
            <PortalState kind="loading" title="Loading opportunities">
              Checking available tasks.
            </PortalState>
          ) : !portal || portal.opportunities.length === 0 ? (
            <PortalState kind="empty" title="No open opportunities">
              New tasks will appear here when coordinators publish them.
            </PortalState>
          ) : (
            <div className="portal-list">
              {portal.opportunities.map((task) => (
                <article className="portal-row" key={task.id}>
                  <i className="green">
                    <Icon name="box" size={18} />
                  </i>
                  <div>
                    <b>{task.title}</b>
                    <p>Skills: {skillList(task.requiredSkillsJson).join(", ") || "Not specified"}</p>
                    <small>
                      {task.scheduledAt ? formatShortDate(task.scheduledAt) : "Schedule to be confirmed"}
                    </small>
                  </div>
                  <span className="badge green">
                    <i /> Open
                  </span>
                </article>
              ))}
            </div>
          )}
          <p className="portal-subtle">
            Assignment is controlled by coordinators. This page only shows tasks available for
            review.
          </p>
        </PortalSection>
      </div>

      <PortalSection
        title="Activity and notifications"
        description="Your completed work and the latest account messages."
        action={
          <Link className="portal-text-link" href="/notifications">
            Open inbox →
          </Link>
        }
      >
        <div className="portal-activity-summary">
          <article>
            <Icon name="check" />
            <span>
              <b>{loading ? "—" : (portal?.completedTasks ?? 0)}</b>
              <small>Completed tasks recorded</small>
            </span>
          </article>
          <article>
            <Icon name="clock" />
            <span>
              <b>{loading ? "—" : (portal?.availability.length ?? 0)}</b>
              <small>Availability windows</small>
            </span>
          </article>
          <article>
            <Icon name="bell" />
            <span>
              <b>{loading ? "—" : (inbox?.totalCount ?? 0)}</b>
              <small>Messages received</small>
            </span>
          </article>
        </div>

        {!inbox || inbox.items.length === 0 ? (
          <PortalState kind="empty" title="No messages yet">
            Updates about assignments and verification will appear here.
          </PortalState>
        ) : (
          <div className="portal-list">
            {inbox.items.slice(0, 4).map((notification) => (
              <article className="portal-row" key={notification.id}>
                <i className={notification.isRead ? "muted" : ""}>
                  <Icon name={notification.isRead ? "check" : "bell"} size={18} />
                </i>
                <div>
                  <b>{notification.subject ?? notification.templateKey}</b>
                  <p>{notification.body ?? "Open the inbox for details."}</p>
                  <small>{formatShortDate(notification.createdAt)}</small>
                </div>
              </article>
            ))}
          </div>
        )}
      </PortalSection>
    </AuthGuard>
  );
}
