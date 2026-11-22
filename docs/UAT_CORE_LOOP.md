# DEV2 UAT — Core Request-to-Closure Loop

**Owner:** Developer 2 (Member 2 — Cases & Citizen)  
**Scope:** D2-12 User Acceptance Testing for the Cases workflow (D2-01–D2-08).  
**Status:** Test script only. **No UAT has been executed yet.** All “Actual result” and “Pass/Fail” cells are intentionally blank.

> **Implementation and validation note (not UAT evidence):** The coordinator alert POST uses the authenticated `casesApi.createAlert()` client, and the active `frontend/` crisis-reporting, my-reports, incident-queue and crisis-detail screens read/write real DEV2 endpoints. `postman/dev2-cases.json` now contains role-login setup, status assertions and negative role/validation/state-machine cases. The historical DEV2 checkout recorded pre-existing Member 3 compile errors in `EmailNotificationHandler.cs` and `AnnouncementService.cs`; final `develop` (`5f337b2`) records those repairs. During this integration the merged tree passed `dotnet restore`, `dotnet build`, `dotnet test` (504 tests), frontend `npm.cmd run typecheck` and `npm.cmd run build` using `C:\Program Files\nodejs\npm.cmd` (the PowerShell `npm.ps1` shim is blocked by execution policy). No clean-database API run, UAT, usability session, screenshot, Postman run or runtime verification was performed, so all result cells remain blank.

---

## 1. Purpose

This script verifies the complete humanitarian case workflow using the **actual implemented DEV2 API routes**:

1. Submit a citizen assistance request.
2. Verify the request.
3. Classify and prioritise the request.
4. Review the recommendation.
5. Assign the request to a coordinator or responsible party.
6. Update the request status.
7. Notify the relevant user.
8. Complete the request.
9. Verify the timeline and audit history.

It is an **API-level UAT script**. Source-level frontend integrations now exist, but they have not been executed or verified in this document; testers should still use the DEV2 Postman collection at `postman/dev2-cases.json` for DEV2 routes, and the Member 1/Member 3/Member 4 collections or direct calls for the dependency steps below. The DEV2 collection intentionally contains no M3/M4 routes.

---

## 2. Test environment and preconditions

**Base URL:** `http://localhost:5210` (from `backend/src/Cms.Api/Properties/launchSettings.json`; adjust if running HTTPS on `https://localhost:7162`).

**Run metadata (complete at execution time)**

| Field | Value |
|---|---|
| Test date(s) |  |
| Branch / RC tag |  |
| Commit SHA / build |  |
| Tester |  |
| Environment |  |

**Database setup**

1. Run the canonical SQL scripts in this exact order (there is no `02_*.sql` in this repository):
   - `backend/db/00_common.sql`
   - `backend/db/01_cases.sql`
   - `backend/db/03_ai.sql`
   - `backend/db/04_res.sql`
   - `backend/db/05_shelter.sql`
   - `backend/db/06_vol.sql`
   - `backend/db/07_geo.sql`
   - `backend/db/08_don.sql`
   - `backend/db/98_foreign_keys.sql`
   - `backend/db/99_seed_users.sql`
2. **Start the API once** so `DevelopmentDemoSeeder` creates the demo users. `90_seed_scenario.sql` validates that those users exist.
3. Run `backend/db/90_seed_scenario.sql`.
4. Run `backend/db/91_seed_ops.sql` (Member 4 owns the limited inventory row referenced by the shortage group).
5. Confirm the seed scenario is present:
   - Crisis `1a000000-0000-0000-0000-000000000001`
   - Areas `1b000000-0000-0000-0000-000000000001` through `1b000000-0000-0000-0000-000000000003`
   - Requests `1d000000-0000-0000-0000-000000000001` through `1d000000-0000-0000-0000-000000000010`; R10 is the back-dated overdue fixture and R6–R8 are the three-request shortage group
   - Citizen user `0b000000-0000-0000-0000-000000000010` (`citizen@example.org`)
   - Coordinator `0b000000-0000-0000-0000-000000000002` (`coordinator@example.org`)
   - Case manager `0b000000-0000-0000-0000-000000000003` (`casemanager@example.org`)

**Accounts and tokens**

- Obtain bearer tokens through the existing Member 1 endpoint `POST /api/v1/auth/login` (`backend/src/Cms.Api/Controllers/AuthController.cs:41`). Password for demo users is `Demo1234!`.
- Use:
  - Citizen token for citizen routes.
  - EmergencyCoordinator token for coordinator routes.
  - CaseManager token for triage routes.
  - SystemAdmin token for audit/outbox verification routes.

---

## 3. Known dependencies and blockers

These must be resolved before the full loop can pass end to end. They are **not** changed by this test script.

| ID | Dependency / blocker | Evidence | Impact on UAT |
|---|---|---|---|
| DEP-01 | **Runtime DI is source-wired; runtime confirmation still required.** `CasesModule` delegates to `CasesModuleExtensions.AddCasesModule`, and `ModuleRegistration.AddCmsModules` calls it after the identity module. | `backend/src/Cms.Api/Modules/CasesModule.cs:11-21`, `backend/src/Cms.Api/Modules/ModuleRegistration.cs:17-20`, `backend/src/Cms.Infrastructure/Cases/CasesModuleExtensions.cs:21-60`, `backend/tests/Cms.Tests/Cases/CasesModuleTests.cs:17-44` | Source-level blocker resolved. Verify controller activation on a clean API run before marking UAT rows passed; do not assume stub responses. |
| DEP-02 | **Source-level target-based recommendation reads exist; live M3 data still required.** `GET /api/v1/recommendations?targetId={id}` and `GET /api/v1/recommendations/target/{targetId:guid}` are present. The separate resource-match route expected by UAT-10 has not been confirmed. | `backend/src/Cms.Api/Controllers/RecommendationsController.cs:29-40`, `backend/src/Cms.Api/Controllers/RecommendationsController.cs` | UAT-04 can read recommendations by request/target once M3 data exists. UAT-10 remains a cross-member dependency; do not mark it passed without a live match response. |
| DEP-03 | **Critical close requires an approved CloseCase decision; source composition is wired but runtime is unverified.** DEV2 registers `DecisionGateCloseCaseAdapter` with `TryAddScoped`; M3 registers the real governance adapter after DEV2 in `AiModule`, so M3 wins in composed startup. A clean API run is still needed to prove the effective gate and a positive critical-close path. | `backend/src/Cms.Infrastructure/Cases/CasesModuleExtensions.cs:45-47`, `backend/src/Cms.Api/Modules/AiModule.cs:40-42`, `backend/src/Cms.Api/Modules/ModuleRegistration.cs:17-20`, `backend/tests/Cms.Tests/Cases/CasesModuleTests.cs` | Keep the main loop on a High-urgency request until a live approved `CloseCase` decision is available. Negative `403 CLOSE_DECISION_REQUIRED` remains testable; do not claim positive Critical closure passed without runtime evidence. |
| DEP-04 | **Notification delivery depends on Member 1/Member 3 outbox processing.** DEV2 enqueues `Notification.Send`; delivery into the in-app inbox is not performed by DEV2. | `backend/src/Cms.Infrastructure/Cases/CaseWriteHelper.cs:108-129`, `backend/src/Cms.Api/Controllers/NotificationsController.cs:28-41` | UAT-07 verifies the outbox/inbox only if the shared processor is running. |
| DEP-05 | **Outbox key defect fixed in DEV2 writer.** `CaseWriteHelper` now delegates to the shared `IOutbox`/`OutboxService`, which sets `EntityId` and `IdempotencyKey`, rejects empty values, and deduplicates against tracked and persisted rows. The payload is a direct `Notification.Send` DTO rather than a wrapped envelope. | `backend/src/Cms.Infrastructure/Cases/CaseWriteHelper.cs`, `backend/src/Cms.Infrastructure/Services/OutboxService.cs`, `backend/db/00_common.sql:101-103,267-272` | Regression coverage is in `CaseWriteHelperTests`. No UAT blocker remains from this item. |
| DEP-06 | **Audit viewer is Member 1 owned and AdminOnly.** | `backend/src/Cms.Api/Controllers/AuditEventsController.cs:11-12` | UAT-09 requires a SystemAdmin token. |
| DEP-07 | **No separate classification/prioritisation endpoint exists.** `Category` and `Urgency` are set on `POST /api/v1/requests` and are not editable afterwards. `PATCH /api/v1/requests/{id}` changes description/location/household/accessibility only (or assignment for staff). | `backend/src/Cms.Application/Cases/DTOs/RequestDtos.cs:7-39`, `backend/src/Cms.Infrastructure/Cases/RequestService.cs:194-254` | UAT-03 verifies classification via submission + staff filtering, not via a separate update call. |
| DEP-08 | **DEV2 `web/` pages exist under allowed paths and the active `frontend/` seams are implemented, but M1 navigation/runtime wiring is not confirmed.** Frontend `typecheck` and `build` passed; the separate `web/` app has no installed `node_modules` here, so no web build result is claimed. | `web/app/(public)`, `web/app/(citizen)`, `web/app/(coordinator)`, `web/components/cases`, `frontend/lib/crisis.ts`, `frontend/components/safeway/ReportFlow.tsx` | Run API UAT through the Postman collection until Member 1 confirms navigation and a `web/` build environment. |
| DEP-09 | **Source-level decision-outcome naming is consistent.** `OperationsController.ApprovedDecision` checks `DecisionOutcome.Approved` / `DecisionOutcome.Modified`, matching the shipped enum. | `backend/src/Cms.Api/Controllers/OperationsController.cs:306-310`, `backend/src/Cms.Domain/Enums/DecisionOutcome.cs` | This old blocker is resolved in source. Runtime allocation behaviour still depends on M4 services. |
| DEP-10 | **Historical DEV2 build blocker superseded; merged-tree build/test PASS.** The DEV2 checkout recorded M3 notification/announcement compile errors; final develop records repairs in `docs/DEV3_REPORT.md`. | `docs/DEV3_REPORT.md` on `5f337b2`; integration `dotnet build`/`dotnet test` (504 passed) | Confirm a clean API run before marking any UAT row passed. |
| DEP-11 | **Frontend validation passed; separate `web/` build was not run.** The active `frontend/` project has installed dependencies and passed `npm.cmd run typecheck` and `npm.cmd run build`. The `web/` project exists under allowed DEV2 paths but has no `node_modules` in this checkout. | `frontend/package.json`, `frontend/node_modules`, `web/package.json`, `web/components/cases`, `web/app/(citizen)`, `web/app/(coordinator)` | M1/M4 must confirm navigation and provide a `web/` dependency install before a web build claim. Use `C:\Program Files\nodejs\npm.cmd`; `npm.ps1` is blocked by PowerShell execution policy. |

---

## 4. Test script

### 4.1 D2-12 seven-phase core-loop view

The detailed nine-test script below is the evidence format approved for this deliverable. Use this mapping to execute the required D2-12 phases in order; do not run verify/triage before the recommendation phase.

| Phase | Required phase | Script reference | Mandatory assertion |
|---|---|---|---|
| 1 | Submit | UAT-01 | `201 Created`; new request is `Submitted`. |
| 2 | Recommendations appear, status unchanged | UAT-04 | M3 recommendation is readable; `GET /api/v1/requests/{id}` still returns the pre-recommendation status. |
| 3 | Verify | UAT-02 | `204 No Content`; status is `Verified` and `verifiedAt` is set. |
| 4 | Triage | UAT-06, first transition | Status is `Triaged` and `triagedAt` is set; category/urgency remain as submitted. |
| 5 | Request match | UAT-10 | `GET /api/v1/recommendations/match/resources/{requestId}` returns ranked candidates for the R6–R8 shortage group. |
| 6 | Decision | UAT-04, extended | `POST /api/decision` records an `Approved` human decision; capture `decisionId`. |
| 7 | Allocate → dispatch/fulfil → close → notification → audit | UAT-06, UAT-07, UAT-08, UAT-09 | Status reaches `Closed`; exactly one notification is observed; matching audit events are visible. |

### 4.2 Detailed test cases

### UAT-01 — Submit a citizen assistance request

| Field | Value |
|---|---|
| **Objective** | A citizen can submit a valid request and receive a new request ID. |
| **Preconditions** | DEP-01 resolved. Citizen token available. Seeded crisis and area exist. |
| **Test steps** | 1. `POST {{baseUrl}}/api/v1/requests` with Citizen token and body below. 2. Record returned `id` as `{{requestId}}`. 3. `GET {{baseUrl}}/api/v1/requests/mine?page=1&pageSize=20` with the same token. |
| **Expected result** | Step 1 returns `201 Created` with `{ "id": "<guid>" }`. Step 3 returns `200 OK` and the new request appears with `status: "Submitted"`. |
| **Actual result** |  |
| **Evidence / screenshot ref** |  |
| **Pass/Fail** |  |

Example body:

```json
{
  "category": "Food",
  "urgency": "High",
  "description": "Family of four needs food parcels after floodwater entered the home.",
  "lat": 7.29,
  "lng": 80.64,
  "address": "Harbour Ward, Colombo",
  "householdSize": 4,
  "accessibilityNeeds": null,
  "crisisId": "1a000000-0000-0000-0000-000000000001",
  "areaId": "1b000000-0000-0000-0000-000000000001",
  "evidenceAttachmentIds": []
}
```

> Note: `address` is persisted and returned by request detail. A non-empty `evidenceAttachmentIds` array is rejected with `422 EVIDENCE_ATTACHMENT_IDS_NOT_SUPPORTED`; upload evidence after creation through `POST /api/v1/requests/{id}/attachments`.

---

### UAT-02 — Verify the request

| Field | Value |
|---|---|
| **Objective** | A coordinator can move a Submitted request to Verified through the sanctioned status service. |
| **Preconditions** | UAT-01 passed. `{{requestId}}` known. Coordinator token available. |
| **Test steps** | 1. `POST {{baseUrl}}/api/v1/requests/{{requestId}}/transition` with body `{"to":"Verified","reason":"Verified against call centre record."}`. 2. `GET {{baseUrl}}/api/v1/requests/{{requestId}}`. |
| **Expected result** | Transition returns `204 No Content`. Detail returns `200 OK` with `status: "Verified"` and a non-null `verifiedAt`. |
| **Actual result** |  |
| **Evidence / screenshot ref** |  |
| **Pass/Fail** |  |

---

### UAT-03 — Classify and prioritise the request

| Field | Value |
|---|---|
| **Objective** | Confirm the submitted category/urgency are visible to staff and can be used to prioritise the queue. |
| **Preconditions** | UAT-01 passed. UAT-02 may be completed first. Coordinator or CaseManager token available. See DEP-07. |
| **Test steps** | 1. Confirm `GET {{baseUrl}}/api/v1/requests/{{requestId}}` returns `category: "Food"` and `urgency: "High"`. 2. `GET {{baseUrl}}/api/v1/requests?category=Food&urgency=High&sortBy=urgency&page=1&pageSize=20`. 3. Confirm the request appears. |
| **Expected result** | Detail returns `200 OK` with the submitted classification. Staff list returns `200 OK`; the request is present and sorted by urgency rank (`Critical > High > Medium > Low`). |
| **Actual result** |  |
| **Evidence / screenshot ref** |  |
| **Pass/Fail** |  |

---

### UAT-04 — Review the recommendation

| Field | Value |
|---|---|
| **Objective** | A human reviewer can review an AI recommendation, record a human decision, and confirm that generating/viewing recommendations does not change the request status. |
| **Preconditions** | M3 recommendation data exists for the seeded crisis or request; alternatively a valid `recommendationId` is supplied externally. Coordinator/CaseManager token available. Record the request status immediately before this test. |
| **Test steps** | 1. If needed, generate M3 data with `POST {{baseUrl}}/api/recommendation/crisis/1a000000-0000-0000-0000-000000000001/generate`. 2. `GET {{baseUrl}}/api/v1/requests/{{requestId}}` and record the status (must be unchanged). 3. `GET {{baseUrl}}/api/v1/recommendations?targetId={{requestId}}` or `GET {{baseUrl}}/api/v1/recommendations/target/{{requestId}}`; capture a `recommendationId`. 4. `POST {{baseUrl}}/api/decision` with `{"recommendationId":"{{recommendationId}}","outcome":"Approved","reason":"Human reviewer approves allocation.","finalPayloadJson":"{\"requestId\":\"{{requestId}}\",\"inventoryId\":\"42000000-0000-0000-0000-000000000001\",\"quantity\":1}"}` and record `decisionId`. 5. Repeat step 2 and confirm the status is still unchanged. 6. If a decision exists, `GET {{baseUrl}}/api/decision/recommendation/{{recommendationId}}`. |
| **Expected result** | Steps 2 and 5 return `200 OK` with the same status as before the recommendation phase. Steps 3 and 6 return `200 OK` when the ID exists. Step 4 returns `201 Created` with `{ "decisionId": "<guid>" }`. A `404` in step 3 or 6 indicates the ID was not found. Recommendation data is advisory only. |
| **Actual result** |  |
| **Evidence / screenshot ref** |  |
| **Pass/Fail** |  |
| **Dependency status** | Requires M3 recommendation/decision data; no DEV2 code blocker is known. |

---

### UAT-05 — Assign the request to a responsible party

| Field | Value |
|---|---|
| **Objective** | A staff member can assign the request to a named coordinator/case manager. |
| **Preconditions** | UAT-02 passed. Coordinator or CaseManager token available. Case manager ID `0b000000-0000-0000-0000-000000000003` exists. |
| **Test steps** | 1. `PATCH {{baseUrl}}/api/v1/requests/{{requestId}}/assign` with body `{"assignedToUserId":"0b000000-0000-0000-0000-000000000003"}`. 2. `GET {{baseUrl}}/api/v1/requests/{{requestId}}`. |
| **Expected result** | Assign returns `204 No Content`. Detail returns `200 OK` with `assignedToUserId` set to the case manager ID. |
| **Actual result** |  |
| **Evidence / screenshot ref** |  |
| **Pass/Fail** |  |

---

### UAT-06 — Update the request status

| Field | Value |
|---|---|
| **Objective** | Staff can move the request through the sanctioned state machine and invalid transitions are rejected. |
| **Preconditions** | UAT-05 passed. UAT-04 reviewed, with human approval recorded. Use the High-urgency request from UAT-01; see DEP-03. |
| **Test steps** | 1. For each value in order, `POST {{baseUrl}}/api/v1/requests/{{requestId}}/transition`: `Triaged`, `Recommended`, `Approved`, `Allocated`, `Dispatched`, `Fulfilled`. 2. `GET {{baseUrl}}/api/v1/requests/{{requestId}}`. 3. Attempt an invalid transition such as `Submitted` from `Fulfilled`. |
| **Expected result** | Valid transitions return `204 No Content`. Detail returns `200 OK` with `status: "Fulfilled"` and non-null `triagedAt`. Invalid transition returns `409 Conflict` with code `INVALID_TRANSITION`. |
| **Actual result** |  |
| **Evidence / screenshot ref** |  |
| **Pass/Fail** |  |

---

### UAT-07 — Notify the relevant user

| Field | Value |
|---|---|
| **Objective** | A status change produces a notification for the requester. |
| **Preconditions** | At least one transition from UAT-06 completed. DEP-04 noted. Citizen and SystemAdmin tokens available. |
| **Test steps** | 1. Repeat a valid transition, for example `Fulfilled` to `Closed` in UAT-08. 2. With the Citizen token, `GET {{baseUrl}}/api/v1/notifications?unreadOnly=false&limit=50`. 3. Optional diagnostic with SystemAdmin token: `GET {{baseUrl}}/api/v1/admin/outbox/dead?page=1&pageSize=20`. |
| **Expected result** | A `Notification.Send` outbox message is created by the status change. If the shared outbox processor is running, the citizen inbox returns `200 OK` with a notification for the request. If processing fails, report the dead-letter result; do not modify DEV2 or M1 code. |
| **Actual result** |  |
| **Evidence / screenshot ref** |  |
| **Pass/Fail** |  |
| **Dependency status** | **Depends on Member 1/Member 3 outbox processing (DEP-04).** |

---

### UAT-08 — Complete the request

| Field | Value |
|---|---|
| **Objective** | A staff member can close a completed request and the citizen can provide feedback. |
| **Preconditions** | UAT-06 passed; request status is `Fulfilled`. Request urgency is High (see DEP-03). |
| **Test steps** | 1. `POST {{baseUrl}}/api/v1/requests/{{requestId}}/transition` with body `{"to":"Closed","reason":"Delivery confirmed and case complete."}`. 2. `GET {{baseUrl}}/api/v1/requests/{{requestId}}`. 3. `POST {{baseUrl}}/api/v1/requests/{{requestId}}/feedback` with Citizen token and body `{"comment":"Assistance received; thank you."}`. |
| **Expected result** | Transition returns `204 No Content`. Detail returns `200 OK` with `status: "Closed"` and non-null `closedAt`. Feedback returns `200 OK` with a new `{ "id": "<guid>" }`. |
| **Actual result** |  |
| **Evidence / screenshot ref** |  |
| **Pass/Fail** |  |

---

### UAT-09 — Verify the timeline and audit history

| Field | Value |
|---|---|
| **Objective** | Every status change is visible in the case timeline and the shared audit trail. |
| **Preconditions** | UAT-01–UAT-08 completed. Citizen/Coordinator token and SystemAdmin token available. DEP-06 noted. |
| **Test steps** | 1. `GET {{baseUrl}}/api/v1/requests/{{requestId}}/timeline`. 2. Confirm entries include `Submitted → Submitted`, `Submitted → Verified`, each transition from UAT-06, and `Fulfilled → Closed`. 3. With SystemAdmin token, `GET {{baseUrl}}/api/v1/audit-events?page=1&pageSize=50`. 4. Confirm audit actions include `Request.Submitted`, `Request.StatusChanged`, `Request.Assigned` and `Request.FeedbackSubmitted`. |
| **Expected result** | Timeline returns `200 OK` in chronological order with actor display names and reasons. Audit endpoint returns `200 OK` with matching events for the request. |
| **Actual result** |  |
| **Evidence / screenshot ref** |  |
| **Pass/Fail** |  |
| **Dependency status** | Audit viewer requires Member 1's AdminOnly endpoint (DEP-06). |

---

### UAT-10 — Request match and allocation handoff

| Field | Value |
|---|---|
| **Objective** | The three urgent Food requests can be ranked against the single limited-resource bucket, and an approved decision can be handed to Member 4 allocation. |
| **Preconditions** | `backend/db/90_seed_scenario.sql` and `backend/db/91_seed_ops.sql` have both run. R6–R8 exist. EmergencyCoordinator token available. `decisionId` from UAT-04 includes `requestId`, `inventoryId` and `quantity` in `finalPayloadJson`. Confirm the M4 match/allocations routes are deployed. |
| **Test steps** | 1. `GET {{baseUrl}}/api/v1/recommendations/match/resources/{{requestId}}` with EmergencyCoordinator token; repeat for R6, R7 and R8. 2. Confirm the same limited stock item appears in the ranked candidates. 3. `POST {{baseUrl}}/api/v1/allocations` with header `Idempotency-Key: uat-alloc-{{requestId}}` and body `{"decisionId":"{{decisionId}}"}`. 4. `GET {{baseUrl}}/api/v1/allocations` with the same token and confirm one allocation for the decision. |
| **Expected result** | Match calls return `200 OK` with ranked candidates and explanations. Allocation returns `201 Created` (or `200 OK` on a valid idempotent replay) with `status: "Allocated"`. The allocation list shows one row linked to `decisionId`, and inventory available stock never goes below zero. |
| **Actual result** |  |
| **Evidence / screenshot ref** |  |
| **Pass/Fail** |  |
| **Dependency status** | Match and allocation controllers are Member 4/Member 3 owned. The `GET /api/v1/recommendations/match/resources/{requestId}` route expected here was not found during source audit; confirm with the owning member before execution. |

---

## 4.3 Required negative checks

### NEG-01 — Invalid transition is rejected

| Field | Value |
|---|---|
| **Objective** | The state machine rejects an unsupported transition. |
| **Preconditions** | UAT-08 has moved `{{requestId}}` to `Fulfilled`. Coordinator or CaseManager token available. |
| **Test steps** | `POST {{baseUrl}}/api/v1/requests/{{requestId}}/transition` with body `{"to":"Submitted","reason":"Negative check"}`. |
| **Expected result** | `409 Conflict` with `code: "INVALID_TRANSITION"`. Request detail still reports `Fulfilled`. |
| **Actual result** |  |
| **Evidence / screenshot ref** |  |
| **Pass/Fail** |  |

### NEG-02 — Citizen cannot use staff routes

| Field | Value |
|---|---|
| **Objective** | Citizen role is denied access to coordinator/case-manager routes. |
| **Preconditions** | Citizen token available. DEP-01 resolved. |
| **Test steps** | 1. `GET {{baseUrl}}/api/v1/requests` with the Citizen token. 2. `POST {{baseUrl}}/api/v1/requests/{{requestId}}/transition` with body `{"to":"Verified","reason":"Negative check"}` with the same token. |
| **Expected result** | Both calls return `403 Forbidden`; no status history entry is added. |
| **Actual result** |  |
| **Evidence / screenshot ref** |  |
| **Pass/Fail** |  |

### NEG-03 — Critical close without a decision is blocked

| Field | Value |
|---|---|
| **Objective** | A Critical request cannot be closed without an approved `CloseCase` decision. |
| **Preconditions** | Seeded Critical request `1d000000-0000-0000-0000-000000000002` is `Dispatched`. Coordinator token available. No approved `CloseCase` decision exists for it. DEP-03 noted. |
| **Test steps** | 1. Transition the request `Dispatched → Fulfilled`. 2. `POST {{baseUrl}}/api/v1/requests/1d000000-0000-0000-0000-000000000002/transition` with body `{"to":"Closed","reason":"Negative check","decisionId":null}`. 3. `GET` the request detail. |
| **Expected result** | Step 2 returns `403 Forbidden` with `code: "CLOSE_DECISION_REQUIRED"`. Step 3 still reports `Fulfilled`, not `Closed`. |
| **Actual result** |  |
| **Evidence / screenshot ref** |  |
| **Pass/Fail** |  |
| **Dependency status** | If Member 3 wires an approving `IDecisionGate`, supply a non-approving decision context for this negative check instead. |

### NEG-04 — Duplicate outbox processing produces one notification

| Field | Value |
|---|---|
| **Objective** | Replaying the same `Notification.Send` outbox message does not create duplicate inbox notifications. |
| **Preconditions** | At least one status transition has produced a `Notification.Send` message with a non-empty `EntityId` and `IdempotencyKey`. SystemAdmin token for outbox/dead-letter diagnostics; Citizen token for inbox. DEP-04 noted; DEP-05 regression is covered by unit tests. |
| **Test steps** | 1. With SystemAdmin token, `GET {{baseUrl}}/api/v1/admin/outbox/dead?page=1&pageSize=20` and capture the relevant message payload. 2. Replay that same payload twice using the Member 1/Member 3 outbox processor tooling. 3. With the Citizen token, `GET {{baseUrl}}/api/v1/notifications?unreadOnly=false&limit=50`. 4. Count notifications for the request. |
| **Expected result** | Both attempts use the same idempotency key; the writer deduplicates them and the citizen inbox contains exactly one notification for the request. |
| **Actual result** |  |
| **Evidence / screenshot ref** |  |
| **Pass/Fail** |  |
| **Dependency status** | BLOCKED if no outbox replay tooling is available; report as a Member 1/Member 3 dependency, do not alter DEV2 code. |

---

## 5. Result summary

| Test ID | Scenario | Pass/Fail | Dependency status |
|---|---|---|---|
| UAT-01 | Submit citizen request |  |  |
| UAT-02 | Verify request |  | DEP-01 runtime confirmation |
| UAT-03 | Classify/prioritise |  | DEP-07 |
| UAT-04 | Review recommendation |  | Requires M3 data |
| UAT-05 | Assign request |  | DEP-01 runtime confirmation |
| UAT-06 | Update status |  | DEP-01 runtime, DEP-03 runtime |
| UAT-07 | Notify user |  | DEP-04 |
| UAT-08 | Complete request |  | DEP-03 runtime |
| UAT-09 | Timeline and audit |  | DEP-06 |
| UAT-10 | Request match and allocation handoff |  | M4/M3 route confirmation |
| NEG-01 | Invalid transition 409 |  |  |
| NEG-02 | Citizen 403 on staff routes |  | DEP-01 runtime confirmation |
| NEG-03 | Critical close blocked |  | DEP-03 runtime |
| NEG-04 | Duplicate outbox → one notification |  | DEP-04 |

**Global runtime gate:** final `develop` records the historical DEP-10 compile errors as repaired, and the merged tree passed build and tests during integration; a clean API run must still be completed before any UAT result can be accepted. All actual/evidence/pass cells in this document remain blank and must not be filled from source-level or unit-test evidence.

---

## 6. Development seed verification (not UAT evidence)

The following development check was run on 21 Nov 2026 in a temporary SQL Server LocalDB database named `dev2_seed_verify_20261121`. It verifies schema idempotency and the fixed DEV2 seed fixtures only; it is **not** a core-loop UAT result and does not replace the blank result cells above. It was executed on the DEV2 branch and was not rerun during this integration.

Commands (run from the repository root, with `sqlcmd -S "(localdb)\MSSQLLocalDB" -E -I -b -d dev2_seed_verify_20261121`):

```text
-i backend\db\00_common.sql
-i backend\db\01_cases.sql
-i backend\db\01_cases.sql
-i backend\db\99_seed_users.sql
-Q "INSERT required demo identity.users rows ..."
-i backend\db\90_seed_scenario.sql
-i backend\db\90_seed_scenario.sql
-i backend\db\90_seed_scenario.verify.sql
```

Result: both schema runs and both seed runs completed with exit code 0. All 19 checks in `backend/db/90_seed_scenario.verify.sql` reported `PASS`:

```text
back-dated Submitted request R10              1           1 PASS
cases.alerts draft only                       1           1 PASS
cases.alerts fixtures                         2           2 PASS
cases.alerts published only                   1           1 PASS
cases.areas fixed fixtures                    3           3 PASS
cases.attachments metadata fixtures           2           2 PASS
cases.crises fixed fixture                    1           1 PASS
cases.feedback fixture                        1           1 PASS
cases.households fixed fixture                1           1 PASS
cases.partner_shares fixture                  1           1 PASS
cases.request_links merge fixture             1           1 PASS
cases.request_status_history rows            36          36 PASS
cases.requests address column                 1           1 PASS
cases.requests fixed fixtures                10          10 PASS
cases.service_centres active only             2           2 PASS
cases.service_centres fixtures                3           3 PASS
plaintext sensitive seed values               0           0 PASS
requests with Critical urgency                2           2 PASS
shortage group R6-R8 Food                     3           3 PASS
```

Observed distributions: status `Closed 1, Dispatched 1, InProgress 1, Merged 1, Submitted 3, Triaged 1, Verified 2`; urgency `Critical 2, High 3, Low 2, Medium 3`. The full canonical order (M3/M4 schemas, `98_foreign_keys.sql`, `91_seed_ops.sql`) and the API core loop were not executed here.

---

## 7. Sign-off

| Role | Name | Date | Signature |
|---|---|---|---|
| Tester |  |  |  |
| Developer 2 (owner) |  |  |  |
| Team leader / Member 1 |  |  |  |

**No real user testing has been performed. Complete this document only after executing the steps above.**
