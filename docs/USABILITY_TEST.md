# DEV2 Usability Test Plan — Citizen Portal and Coordinator Dashboard

**Owner:** Developer 2 (Member 2 — Cases & Citizen)  
**Task:** D2-13 — evidence-based usability testing (NFR-USA-01..04)  
**Status:** **Plan only. No participants have been recruited or tested. All results are blank.**

> **Implementation note (not usability evidence):** Source-level accessibility/usability changes were made in DEV2-owned seams: controlled crisis status selection, queue filter debounce/reset and `crisisId`, `ReasonDialog` Escape/focus handling and focus return, public cross-links, mutually exclusive report status buckets, persisted free-text address with optional coordinates, and removal of unsupported contact-field copy. `frontend/` typecheck and build passed using `C:\Program Files\nodejs\npm.cmd`; no usability session, keyboard-only run, 360 px check, screenshot or satisfaction score was collected, so every result cell below remains blank.

---

## 1. Objective

Evaluate whether representative users can complete the core DEV2 flows — submitting a request, tracking status, verifying/triage, finding a public alert and acting on a case — without coaching, and record measurable evidence of:

- task success;
- time on task;
- errors and severity;
- participant satisfaction and confusion points.

Testing must focus on the existing DEV2 implementation and APIs. The active `frontend/` seams are implemented and pass typecheck/build; the separate `web/` pages under the allowed DEV2 paths have not been built here, and M1 navigation/runtime wiring is not confirmed.

---

## 2. Target users

Recruit **3–5 participants** with a mix of technical and non-technical backgrounds:

| Session code | User type | Relevance |
|---|---|---|
| P-01 | Citizen / affected resident | Primary submitter and tracker |
| P-02 | Non-technical community member | Checks clarity of public alerts and request form language |
| P-03 | Emergency coordinator | Verifies and triages requests |
| P-04 | Case manager | Reviews case detail, assignment and status updates |
| P-05 | Partner officer (P2, optional) | Checks partner-workspace expectations where available |

Use anonymised participant codes only. Do not collect names, contact details or real personal data on screen. Use the seeded demo data from `backend/db/90_seed_scenario.sql`. Record each participant’s user type against the session code in the results table.

---

## 3. Current implementation constraints

| Constraint | Evidence | Effect on the session |
|---|---|---|
| DEV2 `web/` pages exist but M1 SAFEWAY navigation and a `web/` build environment are not confirmed. The active `frontend/` seams are implemented and pass typecheck/build. | `web/app/(public)`, `web/app/(citizen)`, `web/app/(coordinator)`, `web/components/cases`, `frontend/lib/crisis.ts`, `frontend/components/safeway/ReportFlow.tsx` | Until M1 confirms navigation and a `web/` build, run tasks against the API plus `postman/dev2-cases.json` or the built `frontend/` seam, and record the UI/API handoff as a finding. |
| Runtime Cases DI is source-wired through `CasesModule` and `ModuleRegistration`, but has not been confirmed in a clean API run. | `backend/src/Cms.Api/Modules/CasesModule.cs:11-21`, `backend/src/Cms.Api/Modules/ModuleRegistration.cs:17-20`, `backend/src/Cms.Infrastructure/Cases/CasesModuleExtensions.cs:21-60` | Verify controller activation before the session. Record a startup failure as an integration issue, not as a participant result. |
| Public alerts endpoint exists and is anonymous. | `backend/src/Cms.Api/Controllers/PublicController.cs:19-30` | Task 4 can be tested through the API or a minimal public page once available. |
| Map components live in Member 4's `web/` app. | `web/components/map/MapPicker.tsx`, `web/components/map/MapView.tsx` | Do not modify them. DEV2 pages import them read-only with a permanent lat/lng fallback. Record any need for shared map access as a cross-member finding. |
| `web/` scaffold and form libraries are Member 1/Member 4 owned; `frontend/` dependencies are installed. | `web/package.json`, `frontend/package.json`, `web/components/cases` | DEV2 `web/` pages use native accessible validation because React Hook Form/Zod are not declared. `frontend/` typecheck/build passed with `C:\Program Files\nodejs\npm.cmd`; no `web/` build claim is made because `web/node_modules` is absent. |

**Environment / build record (complete before the first session)**

| Field | Value |
|---|---|
| Test date(s) |  |
| Branch / RC tag |  |
| Commit SHA / build |  |
| API base URL | `http://localhost:5210` |
| Browser / device |  |
| Screen sizes tested | 360 px and desktop |
| Seed file version | `backend/db/90_seed_scenario.sql` |
| Facilitator / note-taker |  |

---

## 4. Testing instructions

1. **Before the session**
   - Prepare the database using `backend/db/90_seed_scenario.sql`.
   - Confirm the API is running at `http://localhost:5210`.
   - Prepare role-specific tokens using `POST /api/v1/auth/login` (demo password `Demo1234!`).
   - Open a blank notes sheet using the participant code only.
   - Explain that the system is a prototype and that the participant may stop at any time.

2. **During the session**
   - Ask the participant to think aloud.
   - Give no coaching unless the participant is completely stopped for more than 2 minutes; record the intervention.
   - Record task success, completion time, errors, confusion points and severity.
   - Do not use real names, addresses, phone numbers or medical information.

3. **Severity scale**
   - **Blocker** — task cannot be completed.
   - **Major** — task completed with significant difficulty or wrong outcome.
   - **Minor** — cosmetic or small confusion; task still completed.

4. **After the session**
   - Ask the standard questions in section 6.
   - Anonymise notes and remove screenshots containing personal data.

---

## 5. Usability tasks

### Task 1 — Submit a citizen assistance request

**Scenario:** You are affected by flooding and need food for your household of four.

**Instructions to participant:** Submit a new request using category `Food`, urgency `High`, the Harbour Ward area, and a short description of the need.

**Related implementation:** `POST /api/v1/requests` via `backend/src/Cms.Api/Controllers/RequestsController.cs:31-46`; UI shell `frontend/components/safeway/ReportFlow.tsx`.

| Measure | Result |
|---|---|
| Completed without coaching? |  |
| Time taken |  |
| Errors observed |  |
| Severity |  |
| Notes |  |

---

### Task 2 — Track the request and read its timeline

**Scenario:** You previously submitted a request and want to know what happened to it.

**Instructions to participant:** Find your request in “my reports”, open its detail view and identify the current status and the most recent timeline entry.

**Related implementation:** `GET /api/v1/requests/mine`, `GET /api/v1/requests/{id}`, `GET /api/v1/requests/{id}/timeline` in `backend/src/Cms.Api/Controllers/RequestsController.cs`; UI shell `frontend/app/(app)/my-reports/page.tsx`.

| Measure | Result |
|---|---|
| Completed without coaching? |  |
| Time taken |  |
| Errors observed |  |
| Severity |  |
| Notes |  |

---

### Task 3 — Verify and triage one case as coordinator

**Scenario:** You are an emergency coordinator reviewing the incoming queue.

**Instructions to participant:** Find a `Submitted` request, verify it, then move it to `Triaged` with a short reason.

**Related implementation:** `GET /api/v1/requests`, `POST /api/v1/requests/{id}/transition` in `backend/src/Cms.Api/Controllers/RequestsController.cs`; UI shell `frontend/app/(app)/operations/crises/page.tsx`.

| Measure | Result |
|---|---|
| Completed without coaching? |  |
| Time taken |  |
| Errors observed |  |
| Severity |  |
| Notes |  |

---

### Task 4 — Find a published alert without logging in

**Scenario:** You are a member of the public and want to know whether there is an active safety notice.

**Instructions to participant:** Without signing in, find the published “Boil-water notice for Harbour Ward” and confirm its publication date.

**Related implementation:** `GET /api/v1/public/alerts` in `backend/src/Cms.Api/Controllers/PublicController.cs:19-30`; seeded alert `21000000-0000-0000-0000-000000000001`.

| Measure | Result |
|---|---|
| Completed without coaching? |  |
| Time taken |  |
| Errors observed |  |
| Severity |  |
| Notes |  |

---

### Task 5 — Assign a case and update its status

**Scenario:** You are a case manager preparing a request for dispatch.

**Instructions to participant:** Assign the request to the case manager account, then move it from `Triaged` to `Recommended` and then to `Approved`.

**Related implementation:** `PATCH /api/v1/requests/{id}/assign`, `POST /api/v1/requests/{id}/transition` in `backend/src/Cms.Api/Controllers/RequestsController.cs:222-256`; UI shell `frontend/app/(app)/operations/crises/[id]/page.tsx`.

| Measure | Result |
|---|---|
| Completed without coaching? |  |
| Time taken |  |
| Errors observed |  |
| Severity |  |
| Notes |  |

---

### Task 6 — Accessibility checks (keyboard only, 360 px)

**Scenario:** You must complete the same critical actions without a mouse and on a narrow mobile viewport.

**Instructions to participant:** Repeat Task 1 (submit a request) using only the keyboard, then repeat Task 2 (track the request and read its timeline) with the browser viewport set to **360 px** wide. Do not use a mouse or touch input.

**Related implementation:** UI shells under `frontend/app/(app)` and `frontend/components/safeway`; map components remain Member 4 owned and must not be modified here.

| Check | Result |
|---|---|
| Completed Task 1 keyboard only? |  |
| Completed Task 2 at 360 px? |  |
| All controls reachable by Tab / Shift+Tab? |  |
| Visible focus indicator on every control? |  |
| Status labels and timeline readable at 360 px? |  |
| Errors observed |  |
| Severity |  |
| Screenshot refs |  |

---

## 6. Participant questions

Ask after the tasks and record answers verbatim where possible.

1. Which part of the request form was easiest to understand? Why?
2. Which part of the request form was confusing or worrying?
3. How did you decide which category and urgency to choose?
4. Was the request status easy to understand? Which status labels were unclear?
5. Did the timeline help you understand what happened to the request?
6. Was the published alert easy to find without logging in?
7. Did you feel confident that a human reviewed the case before approval?
8. What would you change before using this in a real emergency?

---

## 7. Feedback recording table

| Participant code | User type | Session date | Tasks completed | Total time | Blocker | Major | Minor | Overall satisfaction (1–5) | Key comment |
|---|---|---|---|---|---|---|---|---|---|
| P-01 |  |  |  |  |  |  |  |  |  |
| P-02 |  |  |  |  |  |  |  |  |  |
| P-03 |  |  |  |  |  |  |  |  |  |
| P-04 |  |  |  |  |  |  |  |  |  |
| P-05 |  |  |  |  |  |  |  |  |  |

---

## 8. Problems identified

Complete after testing. Leave blank if no problem was observed.

| ID | Task | Problem description | Severity | Evidence / screenshot reference | Affected component |
|---|---|---|---|---|---|
| U-01 |  |  |  |  |  |
| U-02 |  |  |  |  |  |
| U-03 |  |  |  |  |  |
| U-04 |  |  |  |  |  |
| U-05 |  |  |  |  |  |

---

## 9. Recommendations

Complete after testing. Only recommend changes within approved scope; route cross-member needs to the owning member.

| ID | Recommendation | Priority | Owner | Accepted? |
|---|---|---|---|---|
| R-01 |  |  |  |  |
| R-02 |  |  |  |  |
| R-03 |  |  |  |  |

---

## 10. Change log and scope decisions

### 10.1 Changes made after testing

Complete only for fixes actually applied inside the approved DEV2 scope. Keep before/after evidence references.

| Change ID | Linked issue | Change made | Owner | Before screenshot/ref | After screenshot/ref | Re-tested? |
|---|---|---|---|---|---|---|
| C-01 |  |  |  |  |  |  |
| C-02 |  |  |  |  |  |  |
| C-03 |  |  |  |  |  |  |

### 10.2 Deliberately not changed

Record cross-member needs here with the reason; do not edit files owned by Member 1, Member 3 or Member 4.

| Item | Reason not changed | Owner to notify | Status |
|---|---|---|---|
|  |  |  |  |

---

## 11. Report findings summary

Complete with 3–5 bullets after the sessions; do not fill this in before real testing.

- 
- 
- 
- 
- 

---

## 12. Conclusion

**To be completed after real testing.**

- Overall task success rate: 
- Average time on task: 
- Most severe issue: 
- Whether the top fixes were applied and retested: 
- Remaining P1/P2 usability risks: 

---

**Reminder:** No real users have completed these tests. Do not fill in results, satisfaction scores or conclusions until sessions are actually run.
