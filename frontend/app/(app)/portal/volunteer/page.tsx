"use client";

import Link from "next/link";
import { localizedErrorMessage } from "@/lib/i18n/errors";
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

import { formatShortDate } from "@/lib/format";
import { useT } from "@/lib/i18n";
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

function optionalNumber(value: string, message: string): number | null {
  if (!value.trim()) return null;
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) {
    throw new Error(message);
  }
  return parsed;
}

function toIso(value: string, message: string): string {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    throw new Error(message);
  }
  return parsed.toISOString();
}

export default function VolunteerPortalPage() {
  const t = useT();
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
        setLoadError(localizedErrorMessage(cause, t));
      });

    const taskRequest = getMyVolunteerTasks()
      .then((result) => setTasks(result.items))
      .catch((cause) => {
        setLoadError(localizedErrorMessage(cause, t));
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
      setError(localizedErrorMessage(cause, t));
    } finally {
      setBusy(false);
    }
  }

  function onSaveProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void run(async () => {
      const latitude = optionalNumber(lat, t("portal.volunteer.validation.coordinates"));
      const longitude = optionalNumber(lng, t("portal.volunteer.validation.coordinates"));
      if ((latitude === null) !== (longitude === null)) {
        throw new Error(t("portal.volunteer.validation.coordinatesPair"));
      }

      await saveVolunteerProfile({
        skillsJson: textToJson(skills),
        languagesJson: textToJson(languages),
        lat: latitude,
        lng: longitude,
        active
      });
    }, t("portal.volunteer.validation.profileSaved"));
  }

  function onAddAvailability(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void run(async () => {
      if (!startsAt || !endsAt) {
        throw new Error(t("portal.volunteer.validation.timeRange"));
      }
      const from = toIso(startsAt, t("portal.volunteer.validation.validDateTime"));
      const to = toIso(endsAt, t("portal.volunteer.validation.validDateTime"));
      if (Date.parse(to) <= Date.parse(from)) {
        throw new Error(t("portal.volunteer.validation.endAfterStart"));
      }
      await addVolunteerAvailability({ startsAt: from, endsAt: to });
      setStartsAt("");
      setEndsAt("");
    }, t("portal.volunteer.validation.availabilityAdded"));
  }

  const verification = portal?.profile?.verificationStatus ?? "Not started";
  const verificationTone =
    verification === "Verified" ? "green" : verification === "Pending" ? "amber" : "coral";

  return (
    <AuthGuard allowedRoles={[RoleNames.Volunteer]}>
      <PortalHeader
        eyebrow={t("portal.volunteer.eyebrow")}
        title={t("portal.volunteer.title")}
        description={t("portal.volunteer.description")}
        action={
          <Link className="btn btn-outline" href="/notifications">
            <Icon name="bell" /> {t("portal.volunteer.notifications")}
          </Link>
        }
      />

      <PortalStats
        items={[
          {
            label: t("portal.volunteer.stats.verification"),
            value: loading ? "—" : verification === "Not started" ? t("portal.volunteer.verification.notStarted") : verification,
            hint: verification === "Verified" ? t("portal.volunteer.verification.eligible") : t("portal.volunteer.verification.reviewRequired"),
            icon: "shield",
            tone: verificationTone
          },
          {
            label: t("portal.volunteer.stats.completedTasks"),
            value: loading ? "—" : (portal?.completedTasks ?? 0),
            icon: "check",
            tone: "green"
          },
          {
            label: t("portal.volunteer.stats.openOpportunities"),
            value: loading ? "—" : (portal?.opportunities.length ?? 0),
            icon: "users",
            tone: "blue"
          },
          {
            label: t("portal.volunteer.stats.unreadMessages"),
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
          title={t("portal.volunteer.profile.title")}
          description={t("portal.volunteer.profile.description")}
        >
          {loading ? (
            <PortalState kind="loading" title={t("portal.volunteer.profile.loadingTitle")}>
              {t("portal.volunteer.profile.loadingBody")}
            </PortalState>
          ) : (
            <form className="portal-form" onSubmit={onSaveProfile}>
              <label>
                <span>{t("portal.volunteer.profile.skills")}</span>
                <input
                  value={skills}
                  onChange={(event) => setSkills(event.target.value)}
                  placeholder={t("portal.volunteer.profile.skillsPlaceholder")}
                />
                <small>{t("portal.volunteer.profile.skillsHelp")}</small>
              </label>
              <label>
                <span>{t("portal.volunteer.profile.languages")}</span>
                <input
                  value={languages}
                  onChange={(event) => setLanguages(event.target.value)}
                  placeholder="en, si"
                />
                <small>{t("portal.volunteer.profile.languagesHelp")}</small>
              </label>
              <div className="portal-form-row">
                <label>
                  <span>{t("portal.volunteer.profile.latitude")}</span>
                  <input
                    type="number"
                    step="any"
                    value={lat}
                    onChange={(event) => setLat(event.target.value)}
                    placeholder={t("portal.volunteer.profile.optional")}
                  />
                </label>
                <label>
                  <span>{t("portal.volunteer.profile.longitude")}</span>
                  <input
                    type="number"
                    step="any"
                    value={lng}
                    onChange={(event) => setLng(event.target.value)}
                    placeholder={t("portal.volunteer.profile.optional")}
                  />
                </label>
              </div>
              <label className="portal-checkbox">
                <input
                  type="checkbox"
                  checked={active}
                  onChange={(event) => setActive(event.target.checked)}
                />
                <span>{t("portal.volunteer.profile.available")}</span>
              </label>
              <button className="btn btn-primary" type="submit" disabled={busy}>
                {busy ? t("portal.volunteer.profile.saving") : t("portal.volunteer.profile.save")}
                {busy ? null : <Icon name="arrow" size={16} />}
              </button>
            </form>
          )}
        </PortalSection>

        <PortalSection
          title={t("portal.volunteer.availability.title")}
          description={t("portal.volunteer.availability.description")}
        >
          {loading ? (
            <PortalState kind="loading" title={t("portal.volunteer.availability.loadingTitle")}>
              {t("portal.volunteer.availability.loadingBody")}
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
                        <p>{t("portal.volunteer.availability.until", { date: formatShortDate(window.endsAt) })}</p>
                      </div>
                    </article>
                  ))}
                </div>
              ) : (
                <PortalState kind="empty" title={t("portal.volunteer.availability.emptyTitle")}>
                  {t("portal.volunteer.availability.emptyBody")}
                </PortalState>
              )}

              <form className="portal-form portal-form--inline" onSubmit={onAddAvailability}>
                <label>
                  <span>{t("portal.volunteer.availability.starts")}</span>
                  <input
                    type="datetime-local"
                    value={startsAt}
                    onChange={(event) => setStartsAt(event.target.value)}
                    required
                  />
                </label>
                <label>
                  <span>{t("portal.volunteer.availability.ends")}</span>
                  <input
                    type="datetime-local"
                    value={endsAt}
                    onChange={(event) => setEndsAt(event.target.value)}
                    required
                  />
                </label>
                <button className="btn btn-outline" type="submit" disabled={busy}>
                  {t("portal.volunteer.availability.addWindow")}
                </button>
              </form>
            </>
          )}
        </PortalSection>
      </div>

      <div className="portal-split-grid">
        <PortalSection
          title={t("portal.volunteer.tasks.title")}
          description={t("portal.volunteer.tasks.description")}
        >
          {loading ? (
            <PortalState kind="loading" title={t("portal.volunteer.tasks.loadingTitle")}>
              {t("portal.volunteer.tasks.loadingBody")}
            </PortalState>
          ) : tasks.length === 0 ? (
            <PortalState kind="empty" title={t("portal.volunteer.tasks.emptyTitle")}>
              {t("portal.volunteer.tasks.emptyBody")}
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
                    <p>{t("portal.volunteer.tasks.skills", { skills: skillList(task.requiredSkillsJson).join(", ") || t("portal.volunteer.tasks.notSpecified") })}</p>
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
                          onClick={() => void run(() => respondToTask(task.id, true), t("portal.volunteer.validation.taskAccepted"))}
                        >
                          {t("portal.volunteer.tasks.accept")}
                        </button>
                        <button
                          className="btn btn-outline btn-small"
                          type="button"
                          disabled={busy}
                          onClick={() => void run(() => respondToTask(task.id, false), t("portal.volunteer.validation.taskDeclined"))}
                        >
                          {t("portal.volunteer.tasks.decline")}
                        </button>
                      </>
                    ) : null}
                    {task.status === "Accepted" ? (
                      <button
                        className="btn btn-primary btn-small"
                        type="button"
                        disabled={busy}
                        onClick={() => void run(() => completeTask(task.id), t("portal.volunteer.validation.taskComplete"))}
                      >
                        {t("portal.volunteer.tasks.complete")}
                      </button>
                    ) : null}
                  </div>
                </article>
              ))}
            </div>
          )}
        </PortalSection>

        <PortalSection
          title={t("portal.volunteer.opportunities.title")}
          description={t("portal.volunteer.opportunities.description")}
        >
          {loading ? (
            <PortalState kind="loading" title={t("portal.volunteer.opportunities.loadingTitle")}>
              {t("portal.volunteer.opportunities.loadingBody")}
            </PortalState>
          ) : !portal || portal.opportunities.length === 0 ? (
            <PortalState kind="empty" title={t("portal.volunteer.opportunities.emptyTitle")}>
              {t("portal.volunteer.opportunities.emptyBody")}
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
                    <p>{t("portal.volunteer.tasks.skills", { skills: skillList(task.requiredSkillsJson).join(", ") || t("portal.volunteer.tasks.notSpecified") })}</p>
                    <small>
                      {task.scheduledAt ? formatShortDate(task.scheduledAt) : t("portal.volunteer.opportunities.scheduleTbc")}
                    </small>
                  </div>
                  <span className="badge green">
                    <i /> {t("portal.volunteer.opportunities.open")}
                  </span>
                </article>
              ))}
            </div>
          )}
          <p className="portal-subtle">
            {t("portal.volunteer.opportunities.note")}
          </p>
        </PortalSection>
      </div>

      <PortalSection
        title={t("portal.volunteer.activity.title")}
        description={t("portal.volunteer.activity.description")}
        action={
          <Link className="portal-text-link" href="/notifications">
            {t("portal.volunteer.activity.openInbox")}
          </Link>
        }
      >
        <div className="portal-activity-summary">
          <article>
            <Icon name="check" />
            <span>
              <b>{loading ? "—" : (portal?.completedTasks ?? 0)}</b>
              <small>{t("portal.volunteer.activity.completedTasks")}</small>
            </span>
          </article>
          <article>
            <Icon name="clock" />
            <span>
              <b>{loading ? "—" : (portal?.availability.length ?? 0)}</b>
              <small>{t("portal.volunteer.activity.availabilityWindows")}</small>
            </span>
          </article>
          <article>
            <Icon name="bell" />
            <span>
              <b>{loading ? "—" : (inbox?.totalCount ?? 0)}</b>
              <small>{t("portal.volunteer.activity.messagesReceived")}</small>
            </span>
          </article>
        </div>

        {!inbox || inbox.items.length === 0 ? (
          <PortalState kind="empty" title={t("portal.volunteer.activity.emptyTitle")}>
            {t("portal.volunteer.activity.emptyBody")}
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
                  <p>{notification.body ?? t("portal.volunteer.activity.inboxDetails")}</p>
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
