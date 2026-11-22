"use client";

// Member 4 / D4-11 volunteer workspace and coordinator queue. Verification,
// assignment and status transitions are server-side rules; this page renders
// their ProblemDetails and never acts as the only guard.

import { useEffect, useState } from "react";
import { api, errorMessage, Paged } from "../../../lib/api";

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

export default function VolunteerWorkspace() {
  const [tab, setTab] = useState<"volunteer" | "coordinator">("volunteer");
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
      setError(errorMessage(loadError));
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
      setError(errorMessage(loadError));
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
      setError(errorMessage(actionError));
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
      <h1>Volunteer operations</h1>
      <div className="tabs" role="tablist" aria-label="Volunteer workspace">
        <button type="button" role="tab" aria-selected={tab === "volunteer"} onClick={() => setTab("volunteer")}>My workspace</button>
        <button type="button" role="tab" aria-selected={tab === "coordinator"} onClick={() => setTab("coordinator")}>Coordinator</button>
      </div>
      {error && <p className="error" role="alert">{error}</p>}

      {tab === "volunteer" ? (
        <>
          <section className="panel">
            <h2>My profile</h2>
            <div className="form-grid">
              <label htmlFor="vol-skills">Skills (JSON array)</label>
              <input id="vol-skills" value={skills} onChange={(event) => setSkills(event.target.value)} />
              <label htmlFor="vol-languages">Languages (JSON array)</label>
              <input id="vol-languages" value={languages} onChange={(event) => setLanguages(event.target.value)} />
              <label htmlFor="vol-lat">Latitude</label>
              <input id="vol-lat" type="number" step="any" value={lat} onChange={(event) => setLat(event.target.value)} />
              <label htmlFor="vol-lng">Longitude</label>
              <input id="vol-lng" type="number" step="any" value={lng} onChange={(event) => setLng(event.target.value)} />
              <label htmlFor="vol-active">Available for tasks</label>
              <input id="vol-active" type="checkbox" checked={active} onChange={(event) => setActive(event.target.checked)} />
            </div>
            <button type="button" onClick={() => void saveProfile()} disabled={busy}>Save profile</button>
          </section>

          <section className="panel">
            <h2>Add availability</h2>
            <div className="inline">
              <label htmlFor="avail-start">Starts at</label>
              <input id="avail-start" type="datetime-local" value={availabilityStart} onChange={(event) => setAvailabilityStart(event.target.value)} />
              <label htmlFor="avail-end">Ends at</label>
              <input id="avail-end" type="datetime-local" value={availabilityEnd} onChange={(event) => setAvailabilityEnd(event.target.value)} />
              <button type="button" onClick={() => void addAvailability()} disabled={busy || !availabilityStart || !availabilityEnd}>Add window</button>
            </div>
          </section>

          <section className="panel">
            <h2>My tasks</h2>
            {myTasks.items.length === 0 ? <p className="muted">No tasks are assigned to you.</p> : (
              <ul className="task-list">
                {myTasks.items.map((task) => (
                  <li key={task.id}>
                    <strong>{task.title}</strong> <span className="badge">{task.status}</span>
                    <p className="small muted">Skills: {task.requiredSkillsJson}</p>
                    <div className="inline">
                      {task.status === "Assigned" && (
                        <>
                          <button type="button" onClick={() => void respond(task.id, true)} disabled={busy}>Accept</button>
                          <button type="button" className="secondary" onClick={() => void respond(task.id, false)} disabled={busy}>Decline</button>
                        </>
                      )}
                      {task.status === "Accepted" && (
                        <button type="button" onClick={() => void complete(task.id)} disabled={busy}>Complete</button>
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
            <h2>Verification queue</h2>
            <div className="inline">
              <label htmlFor="verify-reason">Reason</label>
              <input id="verify-reason" value={verifyReason} onChange={(event) => setVerifyReason(event.target.value)} />
            </div>
            {volunteers.length === 0 ? <p className="muted">No volunteer profiles.</p> : (
              <table>
                <thead><tr><th>Volunteer</th><th>Skills</th><th>Status</th><th>Action</th></tr></thead>
                <tbody>
                  {volunteers.map((volunteer) => (
                    <tr key={volunteer.id}>
                      <td>{volunteer.userId.slice(0, 8)}</td>
                      <td className="small">{volunteer.skillsJson}</td>
                      <td>{volunteer.verificationStatus}</td>
                      <td className="inline">
                        <button type="button" onClick={() => void verify(volunteer.id, true)} disabled={busy || !verifyReason.trim()}>Verify</button>
                        <button type="button" className="secondary" onClick={() => void verify(volunteer.id, false)} disabled={busy || !verifyReason.trim()}>Reject</button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>

          <section className="panel">
            <h2>Create a task</h2>
            <div className="form-grid">
              <label htmlFor="task-title">Title</label>
              <input id="task-title" value={taskTitle} onChange={(event) => setTaskTitle(event.target.value)} />
              <label htmlFor="task-skills">Required skills (JSON array)</label>
              <input id="task-skills" value={taskSkills} onChange={(event) => setTaskSkills(event.target.value)} />
              <label htmlFor="task-request">Request ID (optional)</label>
              <input id="task-request" value={taskRequest} onChange={(event) => setTaskRequest(event.target.value)} />
            </div>
            <button type="button" onClick={() => void createTask()} disabled={busy || !taskTitle.trim()}>Create task</button>
          </section>

          <section className="panel">
            <h2>Task assignment</h2>
            <div className="inline">
              <label htmlFor="assign-task">Task</label>
              <select id="assign-task" value={assignTask} onChange={(event) => setAssignTask(event.target.value)}>
                <option value="">Select a task</option>
                {allTasks.items.filter((task) => task.status === "Open").map((task) => (
                  <option key={task.id} value={task.id}>{task.title}</option>
                ))}
              </select>
              <label htmlFor="assign-volunteer">Verified volunteer</label>
              <select id="assign-volunteer" value={assignVolunteer} onChange={(event) => setAssignVolunteer(event.target.value)}>
                <option value="">Select a volunteer</option>
                {volunteers.filter((volunteer) => volunteer.verificationStatus === "Verified" && volunteer.active).map((volunteer) => (
                  <option key={volunteer.id} value={volunteer.id}>{volunteer.userId.slice(0, 8)}</option>
                ))}
              </select>
              <button type="button" onClick={() => void assign()} disabled={busy || !assignTask || !assignVolunteer}>Assign</button>
            </div>
            <p className="small muted">Unverified volunteers are rejected by the server (403) and are excluded from matching.</p>
            <ul className="task-list">
              {allTasks.items.map((task) => (
                <li key={task.id}><strong>{task.title}</strong> <span className="badge">{task.status}</span></li>
              ))}
            </ul>
          </section>
        </>
      )}
    </>
  );
}
