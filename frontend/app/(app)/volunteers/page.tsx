"use client";

import { AuthGuard } from "@/components/AuthGuard";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useAuth } from "@/lib/auth";
import { useT } from "@/lib/i18n";
import { hasAnyRole, RoleNames } from "@/lib/roles";

// Member 4 / D4-11 volunteer workspace and coordinator queue. Verification,
// assignment and status transitions are server-side rules; this page renders
// their ProblemDetails and never acts as the only guard.

import { useEffect, useState } from "react";
import dynamic from "next/dynamic";
import { api, Paged } from "@/lib/operations-api";

const MapPicker = dynamic(() => import("@/components/map/MapPicker"), { ssr: false });

interface Volunteer {
  id: string;
  userId: string;
  skillsJson: string;
  languagesJson: string;
  verificationStatus: string;
  active: boolean;
  lat?: number | null;
  lng?: number | null;
}
interface VolunteerTask {
  id: string;
  requestId?: string | null;
  title: string;
  requiredSkillsJson: string;
  lat?: number | null;
  lng?: number | null;
  scheduledAt?: string | null;
  status: string;
  volunteerId?: string | null;
  completedAt?: string | null;
}

const EMPTY_TASKS: Paged<VolunteerTask> = { items: [], total: 0, page: 1, pageSize: 50 };

function VolunteerWorkspaceContent() {
  const { user } = useAuth();
  const t = useT();
  const roles = user?.roles ?? [];
  const canVolunteer = roles.includes(RoleNames.Volunteer);
  const canCoordinate = hasAnyRole(roles, [
    RoleNames.VolunteerCoordinator,
    RoleNames.EmergencyCoordinator,
    RoleNames.SystemAdmin
  ]);
  const canManageVolunteers = roles.includes(RoleNames.VolunteerCoordinator) || roles.includes(RoleNames.SystemAdmin);
  const canMatchVolunteers = roles.includes(RoleNames.VolunteerCoordinator) || roles.includes(RoleNames.EmergencyCoordinator);
  const [tab, setTab] = useState<"volunteer" | "coordinator">(() => canCoordinate && !canVolunteer ? "coordinator" : "volunteer");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  const [skills, setSkills] = useState('["Food distribution"]');
  const [languages, setLanguages] = useState('["en"]');
  const [lat, setLat] = useState("7.8731");
  const [lng, setLng] = useState("80.7718");
  const [active, setActive] = useState(true);
  const [availabilityStart, setAvailabilityStart] = useState("");
  const [availabilityEnd, setAvailabilityEnd] = useState("");

  const [myTasks, setMyTasks] = useState<Paged<VolunteerTask>>(EMPTY_TASKS);
  const [volunteers, setVolunteers] = useState<Volunteer[]>([]);
  const [allTasks, setAllTasks] = useState<Paged<VolunteerTask>>(EMPTY_TASKS);
  const [matchRequestId, setMatchRequestId] = useState("");
  const [volunteerMatches, setVolunteerMatches] = useState<Array<{ targetId: string; score: number; rationale: string; isApproximate: boolean }> | null>(null);

  const [taskTitle, setTaskTitle] = useState("");
  const [taskSkills, setTaskSkills] = useState("[]");
  const [taskRequest, setTaskRequest] = useState("");
  const [assignTask, setAssignTask] = useState("");
  const [assignVolunteer, setAssignVolunteer] = useState("");
  const [verifyReason, setVerifyReason] = useState("Documents checked");

  async function loadMyTasks() {
    try {
      setMyTasks(await api<Paged<VolunteerTask>>("/volunteer-tasks/mine"));
    } catch (loadError) {
      setError(localizedErrorMessage(loadError, t));
    }
  }

  async function loadCoordinator() {
    try {
      const [people, tasks] = await Promise.all([
        api<Volunteer[]>("/volunteers"),
        api<Paged<VolunteerTask>>("/volunteer-tasks"),
      ]);
      setVolunteers(people);
      setAllTasks(tasks);
    } catch (loadError) {
      setError(localizedErrorMessage(loadError, t));
    }
  }

  useEffect(() => {
    setError("");
    if (tab === "volunteer") void loadMyTasks();
    else void loadCoordinator();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tab]);

  async function run(action: () => Promise<void>, successMessage = "") {
    if (busy) return;
    setBusy(true);
    setError("");
    try {
      await action();
      if (successMessage) setError("");
      if (tab === "volunteer") await loadMyTasks();
      else await loadCoordinator();
    } catch (actionError) {
      setError(localizedErrorMessage(actionError, t));
    } finally {
      setBusy(false);
    }
  }

  async function saveProfile() {
    await run(async () => {
      await api("/volunteers/profile", {
        method: "POST",
        body: JSON.stringify({
          skillsJson: skills,
          languagesJson: languages,
          lat: lat ? Number(lat) : null,
          lng: lng ? Number(lng) : null,
          active,
        }),
      });
    });
  }

  async function addAvailability() {
    await run(async () => {
      await api("/volunteers/availability", {
        method: "POST",
        body: JSON.stringify({ startsAt: availabilityStart, endsAt: availabilityEnd }),
      });
      setAvailabilityStart("");
      setAvailabilityEnd("");
    });
  }

  async function respond(taskId: string, accept: boolean) {
    await run(async () => {
      await api(`/volunteer-tasks/${taskId}/respond`, {
        method: "POST",
        body: JSON.stringify({ accept }),
      });
    });
  }

  async function complete(taskId: string) {
    await run(async () => {
      await api(`/volunteer-tasks/${taskId}/complete`, { method: "POST" });
    });
  }

  async function createTask() {
    await run(async () => {
      await api("/volunteer-tasks", {
        method: "POST",
        body: JSON.stringify({
          requestId: taskRequest || null,
          title: taskTitle,
          requiredSkillsJson: taskSkills,
          lat: lat ? Number(lat) : null,
          lng: lng ? Number(lng) : null,
          scheduledAt: null,
        }),
      });
      setTaskTitle("");
      setTaskRequest("");
    });
  }

  async function assign() {
    if (!assignTask || !assignVolunteer) return;
    await run(async () => {
      await api(`/volunteer-tasks/${assignTask}/assign?volunteerId=${encodeURIComponent(assignVolunteer)}`, { method: "POST" });
    });
  }

  async function findVolunteerMatches() {
    if (!matchRequestId.trim()) return;
    await run(async () => {
      const result = await api<Array<{ targetId: string; score: number; rationale: string; isApproximate: boolean }>>(`/recommendations/match/volunteers/${matchRequestId.trim()}`);
      setVolunteerMatches(result);
    });
  }

  async function verify(volunteerId: string, approved: boolean) {
    await run(async () => {
      await api(`/volunteers/${volunteerId}/verify`, {
        method: "POST",
        body: JSON.stringify({ approved, reason: verifyReason }),
      });
    });
  }

  return (
    <>
      <h1>{t("volunteers.title")}</h1>
      <div className="tabs" role="tablist" aria-label={t("volunteers.workspaceAriaLabel")}>
        {canVolunteer && <button type="button" role="tab" aria-selected={tab === "volunteer"} onClick={() => setTab("volunteer")}>{t("volunteers.myWorkspace")}</button>}
        {canCoordinate && <button type="button" role="tab" aria-selected={tab === "coordinator"} onClick={() => setTab("coordinator")}>{t("volunteers.coordinator")}</button>}
      </div>
      {error && <p className="error" role="alert">{error}</p>}

      {tab === "volunteer" ? (
        <>
          <section className="panel">
            <h2>{t("volunteers.myProfile")}</h2>
            <div className="form-grid">
              <label htmlFor="vol-skills">{t("volunteers.skills")}</label>
              <input id="vol-skills" value={skills} onChange={(event) => setSkills(event.target.value)} />
              <label htmlFor="vol-languages">{t("volunteers.languages")}</label>
              <input id="vol-languages" value={languages} onChange={(event) => setLanguages(event.target.value)} />
              <label htmlFor="vol-lat">{t("volunteers.latitude")}</label>
              <input id="vol-lat" type="number" step="any" value={lat} onChange={(event) => setLat(event.target.value)} />
              <label htmlFor="vol-lng">{t("volunteers.longitude")}</label>
              <input id="vol-lng" type="number" step="any" value={lng} onChange={(event) => setLng(event.target.value)} />
              <label htmlFor="vol-active">{t("volunteers.availableForTasks")}</label>
              <input id="vol-active" type="checkbox" checked={active} onChange={(event) => setActive(event.target.checked)} />
            </div>
            <MapPicker height={230} value={{ lat: Number(lat), lng: Number(lng) }} onChange={(point) => { setLat(String(point.lat)); setLng(String(point.lng)); }} />
            <button type="button" onClick={() => void saveProfile()} disabled={busy}>{t("volunteers.saveProfile")}</button>
          </section>

          <section className="panel">
            <h2>{t("volunteers.addAvailability")}</h2>
            <div className="inline">
              <label htmlFor="avail-start">{t("volunteers.startsAt")}</label>
              <input id="avail-start" type="datetime-local" value={availabilityStart} onChange={(event) => setAvailabilityStart(event.target.value)} />
              <label htmlFor="avail-end">{t("volunteers.endsAt")}</label>
              <input id="avail-end" type="datetime-local" value={availabilityEnd} onChange={(event) => setAvailabilityEnd(event.target.value)} />
              <button type="button" onClick={() => void addAvailability()} disabled={busy || !availabilityStart || !availabilityEnd}>{t("volunteers.addWindow")}</button>
            </div>
          </section>

          <section className="panel">
            <h2>{t("volunteers.myTasks")}</h2>
            {myTasks.items.length === 0 ? <p className="muted">{t("volunteers.noTasks")}</p> : (
              <ul className="task-list">
                {myTasks.items.map((task) => (
                  <li key={task.id}>
                    <strong>{task.title}</strong> <span className="badge">{task.status}</span>
                    <p className="small muted">{t("volunteers.skillsLabel")} {task.requiredSkillsJson}</p>
                    <div className="inline">
                      {task.status === "Assigned" && (
                        <>
                          <button type="button" onClick={() => void respond(task.id, true)} disabled={busy}>{t("volunteers.accept")}</button>
                          <button type="button" className="secondary" onClick={() => void respond(task.id, false)} disabled={busy}>{t("volunteers.decline")}</button>
                        </>
                      )}
                      {task.status === "Accepted" && (
                        <button type="button" onClick={() => void complete(task.id)} disabled={busy}>{t("volunteers.complete")}</button>
                      )}
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      ) : (
        <>
          <section className="panel">
            <h2>{t("volunteers.verificationQueue")}</h2>
            {canManageVolunteers && <div className="inline">
              <label htmlFor="verify-reason">{t("volunteers.reason")}</label>
              <input id="verify-reason" value={verifyReason} onChange={(event) => setVerifyReason(event.target.value)} />
            </div>}
            {volunteers.length === 0 ? <p className="muted">{t("volunteers.noProfiles")}</p> : (
              <table>
                <thead><tr><th>{t("volunteers.volunteer")}</th><th>{t("volunteers.skillsHeader")}</th><th>{t("volunteers.status")}</th><th>{t("volunteers.action")}</th></tr></thead>
                <tbody>
                  {volunteers.map((volunteer) => (
                    <tr key={volunteer.id}>
                      <td>{volunteer.userId.slice(0, 8)}</td>
                      <td className="small">{volunteer.skillsJson}</td>
                      <td>{volunteer.verificationStatus}</td>
                      <td className="inline">
                        {canManageVolunteers ? <><button type="button" onClick={() => void verify(volunteer.id, true)} disabled={busy || !verifyReason.trim()}>{t("volunteers.verify")}</button>
                        <button type="button" className="secondary" onClick={() => void verify(volunteer.id, false)} disabled={busy || !verifyReason.trim()}>{t("volunteers.reject")}</button></> : t("volunteers.viewOnly")}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>

          {canMatchVolunteers && <section className="panel">
            <h2>{t("volunteers.matching")}</h2>
            <p className="muted small">{t("volunteers.matchHint")}</p>
            <div className="inline"><label htmlFor="vol-match-request">{t("volunteers.requestId")}</label><input id="vol-match-request" value={matchRequestId} onChange={(event) => setMatchRequestId(event.target.value)} /><button type="button" onClick={() => void findVolunteerMatches()} disabled={busy || !matchRequestId.trim()}>{t("volunteers.findVolunteers")}</button></div>
            {volunteerMatches && (volunteerMatches.length === 0 ? <p className="muted">{t("volunteers.noEligibleVolunteers")}</p> : <ol className="match-list">{volunteerMatches.map((match) => <li key={match.targetId}><strong>{match.targetId.slice(0, 8)}</strong> <span className="badge">{Math.round(match.score * 100)}%</span> {match.isApproximate && <span className="badge badge-warn">{t("volunteers.approximate")}</span>}<p className="small">{match.rationale}</p></li>)}</ol>)}
          </section>}

          {canManageVolunteers && <section className="panel">
            <h2>{t("volunteers.createTaskTitle")}</h2>
            <div className="form-grid">
              <label htmlFor="task-title">{t("volunteers.titleLabel")}</label>
              <input id="task-title" value={taskTitle} onChange={(event) => setTaskTitle(event.target.value)} />
              <label htmlFor="task-skills">{t("volunteers.requiredSkills")}</label>
              <input id="task-skills" value={taskSkills} onChange={(event) => setTaskSkills(event.target.value)} />
              <label htmlFor="task-request">{t("volunteers.requestIdOptional")}</label>
              <input id="task-request" value={taskRequest} onChange={(event) => setTaskRequest(event.target.value)} />
            </div>
            <button type="button" onClick={() => void createTask()} disabled={busy || !taskTitle.trim()}>{t("volunteers.createTask")}</button>
          </section>}

          {canManageVolunteers && <section className="panel">
            <h2>{t("volunteers.taskAssignment")}</h2>
            <div className="inline">
              <label htmlFor="assign-task">{t("volunteers.task")}</label>
              <select id="assign-task" value={assignTask} onChange={(event) => setAssignTask(event.target.value)}>
                <option value="">{t("volunteers.selectTask")}</option>
                {allTasks.items.filter((task) => task.status === "Open").map((task) => (
                  <option key={task.id} value={task.id}>{task.title}</option>
                ))}
              </select>
              <label htmlFor="assign-volunteer">{t("volunteers.verifiedVolunteer")}</label>
              <select id="assign-volunteer" value={assignVolunteer} onChange={(event) => setAssignVolunteer(event.target.value)}>
                <option value="">{t("volunteers.selectVolunteer")}</option>
                {volunteers.filter((volunteer) => volunteer.verificationStatus === "Verified" && volunteer.active).map((volunteer) => (
                  <option key={volunteer.id} value={volunteer.id}>{volunteer.userId.slice(0, 8)}</option>
                ))}
              </select>
              <button type="button" onClick={() => void assign()} disabled={busy || !assignTask || !assignVolunteer}>{t("volunteers.assign")}</button>
            </div>
            <p className="small muted">{t("volunteers.unverifiedHint")}</p>
            <ul className="task-list">
              {allTasks.items.map((task) => (
                <li key={task.id}><strong>{task.title}</strong> <span className="badge">{task.status}</span></li>
              ))}
            </ul>
          </section>}
        </>
      )}
    </>
  );
}


export default function VolunteersPage() {
  return (
    <AuthGuard allowedRoles={[RoleNames.Volunteer, RoleNames.VolunteerCoordinator, RoleNames.EmergencyCoordinator, RoleNames.SystemAdmin]}>
      <div className="dev4-operations">
        <VolunteerWorkspaceContent />
      </div>
    </AuthGuard>
  );
}
