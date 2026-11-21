# Dev 3 completion report (AI, governance, notifications)

## Scope

This branch completes the Member 3 block from the authoritative workload PDF
(pages 14–15) and shared contract (pages 7–9): outbox/notification delivery,
AI decision support, human governance, post-incident review, FAQ chat, UI,
tests and developer documentation. Existing Developers 1, 2 and 4 work was
preserved; matcher and geo-provider implementations remain Developer 4-owned.

The original Dev3 work is `origin/feature/dev3-completion` (`db89824`), merged
to `develop` by PR #15 (`5635a7b`). The final live verification and integration
repairs are on `feature/dev3-final-live-verification`, based on
`origin/develop` `8d8267dd9b4876a63e7754f1db695627fc0093b5`. No force push,
reset, clean or other destructive Git operation was performed. No SQL script
was applied to a shared database; all live work used the isolated LocalDB
database `CmsDb_Dev3Live_20261121`.

## Factual audit

| Required route | Implementation / client coverage | Notes |
|---|---|---|
| `GET /api/v1/recommendations` (+ by id/target, create) | `RecommendationsController`, `frontend/lib/recommendations.ts` | Advisory only. Staff RBAC; response maps `Status`. |
| `POST /api/v1/recommendations/match` | `MatchRecommendationsController` -> `MatchRecommendationService` | Calls the three M4 matcher contracts, stores top result and alternatives, then moves `Triaged -> Recommended` for Resource/Shelter matches only. Legacy read-only `/matches` is retained. |
| `POST /api/v1/recommendations/{id}/decision` | `DecisionAliasController` | Exact required route. Controller uses the union of decider roles; plural `/decisions` remains an alias. Governance service applies the exact per-type matrix. |
| `GET /api/v1/post-incident-review` | `PostIncidentReviewController`, `frontend/lib/decisions.ts`, `/operations/decisions` | Filters `crisisId`, `from`, `to`, `outcome`, `page`, `pageSize`; rows include decision, confidence, outcome, override flag and `finalRequestStatus`. |
| `GET /api/v1/notifications` (+ read/read-all) | `NotificationsController`, `NotificationService`, inbox/bell UI | All authenticated roles can read only their own notifications. |
| `GET /api/v1/announcements` (+ create/approve/reject) | `AnnouncementService` + controller | Draft creates an `Announcement` recommendation; only `EmergencyCoordinator` can approve/reject; approval records a decision before outbox fan-out to users, roles **or areas**. |
| `GET/POST /api/v1/admin/dead-letters` | Existing admin controller | AdminOnly; dead-letter GET and retry are integration-tested. |
| `POST /api/v1/ai/chat` | `AiController`, `AiChatService`, `frontend/lib/aiChat.ts`, `/operations/ai/chat` | Fixed FAQ with optional informational LLM upgrade; explicitly decides nothing. |

Role matrix is exact: Classification/Priority/Duplicate/Anomaly/Summary =
`CaseManager, EmergencyCoordinator`; ResourceMatch = `LogisticsManager,
EmergencyCoordinator`; ShelterMatch = `ShelterAdmin, EmergencyCoordinator`;
VolunteerMatch = `VolunteerCoordinator, EmergencyCoordinator`; Announcement and
CloseCase = `EmergencyCoordinator`. `SystemAdmin` has no break-glass decision
right. Controller-level RBAC uses `backend/src/Cms.Api/Constants/AuthorizedRoles.cs`;
service-level enforcement remains authoritative.

## Implemented changes

### Outbox and notifications

- `CaseWriteHelper` delegates to `IOutbox.EnqueueAsync`, so `EntityId`,
  `IdempotencyKey` and unwrapped payloads are persisted transactionally.
- Required Factory pattern: `INotificationChannelFactory`,
  `INotificationChannel`, `InAppNotificationChannel`, `EmailNotificationChannel`
  and `NotificationChannelFactory`. `EmailNotificationHandler` resolves channels
  through the factory and skips already-sent rows on redelivery.
- Successful notification delivery calls `IAuditLogger`; SMTP failure marks the
  email row failed and rethrows so the outbox retries or dead-letters it.
- `backend/db/02_notif.sql` idempotently creates `common.notification_templates`,
  seeds English (`en`) and Spanish (`es`) variants, and never overwrites local
  edits. `NotificationTemplates` mirrors both languages in code.
- Announcements carry a language, record an immutable decision on approve or
  reject, and fan out only through stable per-recipient `Notification.Send`
  outbox keys. Area audiences resolve active requesters with a request in the
  selected area because the frozen user contract has no user-area field.

### AI decision support

- Strategy pattern: `IAiProvider` has a deterministic rule provider and an
  optional LLM provider. `LlmProvider` uses the shared named `cms-external`
  client through `IHttpClientFactory`, enforces the 5-second timeout, and sends
  only category, urgency, `AreaId` and scrubbed description.
- `AiProviderSelector` falls back to rules on HTTP failure, timeout, empty LLM
  output, or any suggestion below `0.5` confidence; `Source` becomes `Rule`.
  Observer/outbox behaviour is provided by transactional enqueue plus
  `IOutboxHandler` consumers.
- `RuleBasedProvider` duplicate candidates require the same category, the last
  24 hours, within 500 m and a text-similarity threshold; candidates are never
  merged or deleted automatically. Anomaly and crisis-summary rules are advisory
  only, with the summary route restricted to `EmergencyCoordinator`.
- `RequestSubmittedHandler` narrows duplicate candidates by category and the
  24-hour window; AI failures are swallowed so submission is never blocked.
  New tests prove triage recommendations are stored and that failure does not
  block the submit path.
- `POST /api/v1/recommendations/match` persists ranked matches plus alternatives
  and only advances `Triaged -> Recommended` where the contract allows it.
  M4 matcher implementations remain the property of Developer 4.

### Governance and review

- `ai.recommendations.Status` is added idempotently and mapped through the API.
  `RecommendationStatuses.Pending`/`Decided` are constants, not a new enum.
- Recommendations and decision records are append-only in EF (`CmsDbContext`)
  and SQL (`TR_recommendations_immutable`, `TR_decision_records_immutable`).
  A decision is represented by a new decision record, never by mutating the
  recommendation; a second decision on the same recommendation is rejected.
- Every recommendation write and every decision calls `IAuditLogger`; all
  relevant write endpoints enforce RBAC in both controller and service.
- Approved/Modified `ResourceMatch` or `ShelterMatch` decisions move a
  `Recommended` request to `Approved` through `IRequestStatusService`.
- `GET /api/v1/post-incident-review` exposes `finalRequestStatus` and supports
  date and outcome filters in the service, not only in the UI fallback. New
  route-level integration tests cover recommendation authorization, the exact
  decision route, the no-`SystemAdmin` rule and the review route.

### Frontend, docs and API surface

- AI page makes no specific role claims; the decisions UI mirrors the exact
  role matrix and uses the required review route and filters.
- FAQ chat page displays: **"Information only - this chat does not make or
  approve decisions."**
- Postman AI, Governance and Notifications folders cover the routes, including
  the exact singular `/decision` route and announcement reject.
- Swagger XML comments were added to the Dev 3 controllers; documentation
  generation and XML inclusion were already configured.
- README and TESTING document the run order: `00_common.sql` → `02_notif.sql` →
  `03_ai.sql` → module scripts → `98_foreign_keys.sql` → `99_seed_users.sql` →
  seed data.
- Required report artifacts are present: `docs/DEV3_REPORT.md`,
  `docs/DEV3_INDIVIDUAL_REFLECTION.md`, `docs/DEV3_DEMO_SCRIPT.md` and
  `docs/demo/Dev3_Demo_Video.zip`.

### Final integration repairs on this branch

The baseline build from current `origin/develop` failed with 14 `CS1061`
errors because the Dev3 `Announcements` DbSet and append-only guards had been
lost in the M4 merge. This branch restores `CmsDbContext.Announcements` and the
`DecisionRecord`/`Recommendation` append-only guards; the exact singular
decision route alias from the original Dev3 contract remains in place.

After M4 added operations contracts, two `MatchCandidate` interfaces caused
ambiguity; the API controller and `MatchRecommendationService` now explicitly
alias `Cms.Application.Contracts.Operations`. `MatchRecommendationService` is
wired to the real M4 matcher contracts and converts the decimal matcher score
explicitly; `MatchRecommendationServiceTests` was updated to the same contract.
No M4 module was rewritten.

## Live end-to-end verification (2026-11-21)

A real API run was performed against the isolated LocalDB database with seeded
test data and the real login endpoint; details and copied responses are in
`docs/DEV3_LIVE_EVIDENCE.md`.

Observed chain:

1. `POST /api/v1/requests` returned HTTP 201; the outbox processor generated
   two rule-based `Priority`/`Classification` recommendations.
2. `POST /api/v1/recommendations/match` for seeded request
   `1d000000-0000-0000-0000-000000000006` returned HTTP 200 with one real
   resource candidate, moved the request `Triaged -> Recommended`, and stored
   the recommendation.
3. A citizen decision attempt returned HTTP 403; an allocation attempt with a
   random decision id returned HTTP 403; the authorized logistics decision
   returned HTTP 201 and moved the request `Recommended -> Approved`; a
   duplicate decision returned HTTP 409.
4. Workflow transitions returned HTTP 204 through `Allocated`, `Dispatched`
   and `Fulfilled`. Closing without a CloseCase decision returned HTTP 403
   (`CLOSE_DECISION_REQUIRED`); an approved CloseCase decision returned HTTP
   201 and the subsequent close transition returned HTTP 204.
5. SQL verification exited 0 and confirmed the request status/history, two
   recommendations with exactly one decision each, two generated AI rows for
   the new request, processed outbox rows, twelve InApp/Email notifications,
   audit events and both append-only SQL triggers.

The positive decision-gated case action in this run is the Dev3-owned Critical
close gate. The M4 allocation write path could not be used because of an
unrelated execution-strategy defect recorded below; the no-decision allocation
attempt did prove the gate denies unauthorized case actions.

## Failure-injection coverage

| Scenario | Evidence |
|---|---|
| AI HTTP failure / fallback | `AiProviderSelectorTests.HttpFailure_FallsBackToRuleProvider`; `RequestSubmittedHandlerTests.HandleAsync_WhenLlmFails_StoresRuleRecommendations` |
| AI timeout | `AiProviderSelectorTests.Timeout_FallsBackToRuleProvider`; `AiChatServiceTests.LlmFailure_FallsBackToFixedFaq` |
| Low-confidence LLM output | `AiProviderSelectorTests.LowConfidence_FallsBackToRuleProvider` |
| AI provider throws at handler boundary | `RequestSubmittedHandlerTests.HandleAsync_WhenAiProviderThrows_DoesNotThrowOrBlock` |
| Map provider down | `HaversineTests.Map_provider_down_FallsBackToHaversine_WithoutBlockingOrClaimingPrecision` (Developer 4 stub; not modified) |
| Email down | `EmailNotificationHandlerTests.Handler_marks_the_email_row_failed_and_rethrows_when_delivery_fails`; `OutboxProcessorTests` retry/dead-letter coverage |
| Same outbox event twice | `EmailNotificationHandlerTests.Handler_is_idempotent_when_the_same_event_is_retried` |
| Dead-letter visibility/retry | `OutboxEndpointTests` |
| Role matrix | `DecisionRolePolicyTests`, `GovernanceEndpointTests` |
| Match governance | `MatchRecommendationServiceTests`, `DecisionServiceTests` |
| Review projection | `PostIncidentReviewServiceTests` |
| Templates and chat | `NotificationTemplateTests`, `AiChatServiceTests` |
| Append-only/audit rules | `RecommendationServiceTests`, `AnnouncementServiceTests` |

## Verification

| Check | Result | Evidence |
|---|---|---|
| Baseline `dotnet build backend/Cms.sln` before repairs | FAIL | 14 `CS1061` errors from the post-merge missing Dev3 DbSet/guards; this was the starting defect, not a pass. |
| `dotnet restore backend/Cms.sln` | PASS | All projects restored. |
| `dotnet build backend/Cms.sln --no-restore` | PASS | 0 warnings, 0 errors. |
| `dotnet test backend/Cms.sln --no-build` | PASS | 499 passed, 0 failed, 0 skipped. |
| `npm.cmd run typecheck` (in `frontend/`) | PASS | `tsc --noEmit` exited 0. |
| `npm.cmd run build` (in `frontend/`) | PASS | Next.js compiled and generated the static route set. |
| Postman collection JSON | PASS | Parsed successfully after route updates. |
| Live API + LocalDB E2E | PASS | 19 asserted HTTP checks and SQL verification exited 0; see `docs/DEV3_LIVE_EVIDENCE.md`. |
| `git diff --check` | PASS | No whitespace errors. |

## Unresolved issues / assumptions

- The real M4 resource matcher was exercised successfully through
  `POST /api/v1/recommendations/match`; shelter and volunteer matchers were not
  part of this scenario and remain Developer 4-owned. No M4 module was
  rewritten.
- The registered geo provider is `HaversineOnlyGeoService`. A resource fixture
  with coordinates caused the M4 resource matcher to call external OSRM and
  stall; the verified fixture intentionally omitted coordinates and the matcher
  returned `isApproximate: true`. No geo code was changed.
- Existing EF migrations from earlier integration commits remain in history.
  No new migration was added; Dev3 schema deliverables are the idempotent SQL
  scripts under `backend/db/`.
- `backend/db/90_seed_scenario.sql` exists in this checkout and seeded the
  request used for the live run. `backend/db/91_seed_ops.sql` fails with
  `Msg 208, Invalid object name 'res.resource_types'` because M4 EF mappings
  expect snake_case resource objects while `04_res.sql` creates PascalCase
  tables. This is M4-owned; the isolated fixture used test-only views/triggers,
  not a production schema change.
- The M4 `POST /api/v1/allocations` endpoint fails even after a valid decision
  with `InvalidOperationException: The configured execution strategy
  'SqlServerRetryingExecutionStrategy' does not support user-initiated
  transactions` from `OperationsController.Allocate`. It is outside Dev3 scope
  and was not changed. The no-decision path was still verified as denied (403),
  and the Dev3-owned close gate was verified end to end.
- `/swagger/v1/swagger.json` returns HTTP 500 because Swashbuckle cannot read
  `FilesController.Upload` `[FromForm]` with `IFormFile`. Direct authenticated
  API calls were used instead; this Swagger defect is outside Dev3 scope.
- SMTP was not configured, so the email notification sender recorded delivery
  in the log. Twelve email/in-app notification rows were verified in the
  isolated database; no external email provider was contacted.
