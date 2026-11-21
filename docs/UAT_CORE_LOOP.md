# DEV2 UAT — Core Request-to-Closure Loop

**Owner:** Developer 2 (Member 2 — Cases & Citizen)  
**Scope:** D2-12 User Acceptance Testing for the Cases workflow (D2-01–D2-08).  
**Status:** Test script only. **No UAT has been executed yet.** All “Actual result” and “Pass/Fail” cells are intentionally blank.

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

It is an **API-level UAT script**. The D2-09/D2-10 frontend pages are not yet integrated with these endpoints, so testers should use the DEV2 Postman collection at `postman/dev2-cases.json` for DEV2 routes, and the Member 1/Member 3/Member 4 collections or direct calls for the dependency steps below. The DEV2 collection intentionally contains no M3/M4 routes.

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
| DEP-01 | **Runtime DI is not wired.** The real `Cms.Infrastructure.Cases.CasesModuleExtensions.AddCasesModule(IServiceCollection, IConfiguration)` is not called. The API module hook `backend/src/Cms.Api/Modules/CasesModule.cs:9-12` only returns `services`. | `backend/src/Cms.Api/Modules/CasesModule.cs:9-12`, `backend/src/Cms.Infrastructure/Cases/CasesModuleExtensions.cs:16-54`, `backend/src/Cms.Infrastructure/DependencyInjection.cs:107-115` | **BLOCKING.** DEV2 controller dependencies (`IRequestService`, `ICrisisService`, `IRequestWorkflowService`, `IRequestMergeService`, `IAttachmentService`, `IHouseholdService`) have no fallback registration, so controller activation fails with a DI resolution error. Only three foundation interfaces have fail-closed stubs (`NullRequestReader`, `NoOpRequestStatusService`, `DenyAllDecisionGate`). Member 1 must wire the real extension before UAT; do not expect stub responses. |
| DEP-02 | **AI recommendation review has no target-based read endpoint.** Existing M3 routes are `POST /api/recommendation`, `GET /api/recommendation/{recommendationId:guid}`, `POST /api/recommendation/crisis/{crisisId:guid}/generate` and `GET /api/decision/recommendation/{recommendationId:guid}`. There is no `GET /api/v1/recommendations?targetId=`. | `backend/src/Cms.Api/Controllers/RecommendationController.cs:21-65`, `backend/src/Cms.Api/Controllers/DecisionController.cs:60-77` | Test UAT-04 is **BLOCKED** unless a recommendation/decision ID is supplied externally by Member 3 or a target-based read is agreed. |
| DEP-03 | **Critical close requires an approved CloseCase decision.** DEV2 now has a composition adapter that calls the shared M3 contract `IDecisionGate.RequireApprovedAsync(decisionId, RecommendationType.CloseCase, requestId, ct)`, but Member 1's `DenyAllDecisionGate` is still the registered implementation. The main loop therefore uses the seeded **High** urgency request or creates a new High/Urgency request. | `backend/src/Cms.Infrastructure/Cases/RequestStatusService.cs:76-85`, `backend/src/Cms.Infrastructure/Cases/DecisionGateCloseCaseAdapter.cs`, `backend/src/Cms.Application/Contracts/IDecisionGate.cs` | Closing a Critical request still returns `403 CLOSE_DECISION_REQUIRED`. This is expected until Member 3 implements and registers the real `IDecisionGate`. |
| DEP-04 | **Notification delivery depends on Member 1/Member 3 outbox processing.** DEV2 enqueues `Notification.Send`; delivery into the in-app inbox is not performed by DEV2. | `backend/src/Cms.Infrastructure/Cases/CaseWriteHelper.cs:108-129`, `backend/src/Cms.Api/Controllers/NotificationsController.cs:28-41` | UAT-07 verifies the outbox/inbox only if the shared processor is running. |
| DEP-05 | **Outbox key defect fixed in DEV2 writer.** `CaseWriteHelper` now delegates to the shared `IOutbox`/`OutboxService`, which sets `EntityId` and `IdempotencyKey`, rejects empty values, and deduplicates against tracked and persisted rows. The payload is a direct `Notification.Send` DTO rather than a wrapped envelope. | `backend/src/Cms.Infrastructure/Cases/CaseWriteHelper.cs`, `backend/src/Cms.Infrastructure/Services/OutboxService.cs`, `backend/db/00_common.sql:101-103,267-272` | Regression coverage is in `CaseWriteHelperTests`. No UAT blocker remains from this item. |
| DEP-06 | **Audit viewer is Member 1 owned and AdminOnly.** | `backend/src/Cms.Api/Controllers/AuditEventsController.cs:11-12` | UAT-09 requires a SystemAdmin token. |
| DEP-07 | **No separate classification/prioritisation endpoint exists.** `Category` and `Urgency` are set on `POST /api/v1/requests` and are not editable afterwards. `PATCH /api/v1/requests/{id}` changes description/location/household/accessibility only (or assignment for staff). | `backend/src/Cms.Application/Cases/DTOs/RequestDtos.cs:7-39`, `backend/src/Cms.Infrastructure/Cases/RequestService.cs:194-254` | UAT-03 verifies classification via submission + staff filtering, not via a separate update call. |
| DEP-08 | **DEV2 web pages now exist under the allowed `web/` paths, but are not wired into M1's SAFEWAY navigation or build.** The M1-owned `frontend/` shell seams still throw/notices. | `web/app/(public)`, `web/app/(citizen)`, `web/app/(coordinator)`, `web/components/cases`, `frontend/lib/crisis.ts:42-54` | Run API UAT through the Postman collection until Member 1 approves navigation and the web app is built with the `develop` scaffold. |
| DEP-09 | **Member 4 allocation decision check is inconsistent with the shared enum.** `OperationsController.ApprovedDecision` checks `DecisionOutcome.Approve` / `DecisionOutcome.Modify`, but the shipped enum defines `Approved` / `Modified`. | `backend/src/Cms.Api/Controllers/OperationsController.cs:305-310`, `backend/src/Cms.Domain/Enums/DecisionOutcome.cs:4-10` | After the stale type names in DEP-10 are fixed, this will still fail to compile. UAT-10 cannot pass until Member 4 corrects the enum names. Report to Member 4; do not change the controller here. |
| DEP-10 | **The current `develop` target does not build.** `OperationsController.cs` still references `ApplicationDbContext`, `ICurrentUserService` and `IAuditService`, which no longer exist after the shared-type renames. | `backend/src/Cms.Api/Controllers/OperationsController.cs:17-24`; verified by building an archive of `develop` at `77a7fa2` (6 × `CS0246`) | Member 4/Member 1 must fix these references before any merge can be accepted. The DEV2 feature branch itself builds cleanly; the failure is pre-existing on `develop` and is not caused by these seven files. |
| DEP-11 | **DEV2 web pages cannot be built on this feature branch alone.** The `web/` scaffold (`package.json`, `lib/api.ts`, `app/layout.tsx`, map components) exists only on `develop`, and React Hook Form/Zod are not declared in the M1/M4-owned `web/package.json`. DEV2 pages therefore use native accessible validation and the existing `web/lib/api.ts` client. | `develop:web/package.json`, `develop:web/lib/api.ts`, `web/components/cases`, `web/app/(citizen)`, `web/app/(coordinator)` | M1/M4 must supply the scaffold and decide whether to add React Hook Form/Zod; M1 must add navigation to the DEV2 routes. No web build result can be claimed until those prerequisites are present. |

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

> Note: `address` and `evidenceAttachmentIds` are accepted by the DTO but are not persisted by the current implementation (`docs/DEV2_CONTRACT_REQUESTS.md:60`).

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
| **Preconditions** | DEP-02 must be resolved or a valid `recommendationId` supplied externally. M3 services available. Coordinator/CaseManager token available. Record the request status immediately before this test. |
| **Test steps** | 1. If needed, generate M3 data with `POST {{baseUrl}}/api/recommendation/crisis/1a000000-0000-0000-0000-000000000001/generate`. 2. `GET {{baseUrl}}/api/v1/requests/{{requestId}}` and record the status (must be unchanged). 3. Using the M3-supplied ID, `GET {{baseUrl}}/api/recommendation/{{recommendationId}}`. 4. `POST {{baseUrl}}/api/decision` with `{"recommendationId":"{{recommendationId}}","outcome":"Approved","reason":"Human reviewer approves allocation.","finalPayloadJson":"{\"requestId\":\"{{requestId}}\",\"inventoryId\":\"42000000-0000-0000-0000-000000000001\",\"quantity\":1}"}` and record `decisionId`. 5. Repeat step 2 and confirm the status is still unchanged. 6. If a decision exists, `GET {{baseUrl}}/api/decision/recommendation/{{recommendationId}}`. |
| **Expected result** | Steps 2 and 5 return `200 OK` with the same status as before the recommendation phase. Steps 3 and 6 return `200 OK` when the ID exists. Step 4 returns `201 Created` with `{ "decisionId": "<guid>" }`. A `404` in step 3 or 6 indicates the ID was not found; there is no target-based lookup in the current API. Recommendation data is advisory only. |
| **Actual result** |  |
| **Evidence / screenshot ref** |  |
| **Pass/Fail** |  |
| **Dependency status** | **BLOCKED until DEP-02 is resolved or a recommendation ID is supplied externally.** |

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
| **Preconditions** | `backend/db/90_seed_scenario.sql` and `backend/db/91_seed_ops.sql` have both run. R6–R8 exist. DEP-09 noted. EmergencyCoordinator token available. `decisionId` from UAT-04 includes `requestId`, `inventoryId` and `quantity` in `finalPayloadJson`. |
| **Test steps** | 1. `GET {{baseUrl}}/api/v1/recommendations/match/resources/{{requestId}}` with EmergencyCoordinator token; repeat for R6, R7 and R8. 2. Confirm the same limited stock item appears in the ranked candidates. 3. `POST {{baseUrl}}/api/v1/allocations` with header `Idempotency-Key: uat-alloc-{{requestId}}` and body `{"decisionId":"{{decisionId}}"}`. 4. `GET {{baseUrl}}/api/v1/allocations` with the same token and confirm one allocation for the decision. |
| **Expected result** | Match calls return `200 OK` with ranked candidates and explanations. Allocation returns `201 Created` (or `200 OK` on a valid idempotent replay) with `status: "Allocated"`. The allocation list shows one row linked to `decisionId`, and inventory available stock never goes below zero. |
| **Actual result** |  |
| **Evidence / screenshot ref** |  |
| **Pass/Fail** |  |
| **Dependency status** | Match and allocation controllers are Member 4 owned; DEP-09 and DEP-10 block allocation until fixed. |

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
| UAT-02 | Verify request |  | DEP-01 |
| UAT-03 | Classify/prioritise |  | DEP-07 |
| UAT-04 | Review recommendation |  | **BLOCKED — DEP-02** |
| UAT-05 | Assign request |  | DEP-01 |
| UAT-06 | Update status |  | DEP-01, DEP-03 |
| UAT-07 | Notify user |  | DEP-04 |
| UAT-08 | Complete request |  | DEP-03 |
| UAT-09 | Timeline and audit |  | DEP-06 |
| UAT-10 | Request match and allocation handoff |  | DEP-09, DEP-10 (M4 owned) |
| NEG-01 | Invalid transition 409 |  |  |
| NEG-02 | Citizen 403 on staff routes |  | DEP-01 |
| NEG-03 | Critical close blocked |  | DEP-03 |
| NEG-04 | Duplicate outbox → one notification |  | DEP-04 |

**Global merge gate:** DEP-10 must be resolved on `develop` before any UAT can be accepted, even if individual rows above are marked passed on the DEV2 feature branch.

---

## 6. Sign-off

| Role | Name | Date | Signature |
|---|---|---|---|
| Tester |  |  |  |
| Developer 2 (owner) |  |  |  |
| Team leader / Member 1 |  |  |  |

**No real user testing has been performed. Complete this document only after executing the steps above.**
