# Dev 3 completion report (AI, governance, notifications)

## Scope

This branch completes the Member 3 block from the authoritative workload PDF
(pages 14–15) and the shared contract pages 7–9: outbox/notification delivery,
AI decision support, human governance, post-incident review, FAQ chat, UI,
tests and developer documentation. Existing work by Developers 1, 2 and 4 was
preserved; changes outside Dev 3 modules are limited to compatible integration
fixes for the shared outbox contract.

Branch: `feature/dev3-completion`. No push, PR or merge to `develop` was made,
and no SQL script was applied to a shared database.

## Factual audit

| Required route | Implementation / client coverage | Notes |
|---|---|---|
| `GET /api/v1/recommendations` (+ by id/target, create) | `RecommendationsController`, `frontend/lib/recommendations.ts` | Advisory only; `Status` is mapped in the response. |
| `POST /api/v1/recommendations/match` | `MatchRecommendationsController` -> `MatchRecommendationService` | Calls the three M4 matcher contracts, stores top result and alternatives, then moves `Triaged -> Recommended` for Resource/Shelter matches only. Legacy read-only `/matches` is retained. |
| `POST /api/v1/recommendations/{id}/decision` | `DecisionAliasController` | Exact required route. Plural `/decisions` remains a compatibility alias. Decisions are immutable and role-gated. |
| `GET /api/v1/post-incident-review` | `PostIncidentReviewController`, `frontend/lib/decisions.ts`, `/operations/decisions` | Filters `crisisId`, `from`, `to`, `outcome`, `page`, `pageSize`; rows include decision, confidence, outcome, override flag and `finalRequestStatus`. Legacy `/reviews/decisions` is retained. |
| `GET /api/v1/notifications` (+ read/read-all) | Existing controller and inbox/bell UI | Postman coverage added. |
| `GET /api/v1/announcements` (+ create/approve/reject) | `AnnouncementService` + controller | Draft creates an `Announcement` recommendation; only `EmergencyCoordinator` can approve/reject; approval records a decision before outbox fan-out. |
| `GET/POST /api/v1/admin/dead-letters` | Existing controller | AdminOnly; requires an admin token. |
| `POST /api/v1/ai/chat` | `AiController`, `AiChatService`, `frontend/lib/aiChat.ts`, `/operations/ai/chat` | Fixed FAQ with optional informational LLM upgrade; explicitly decides nothing. |

Role matrix is exact: Classification/Priority/Duplicate/Anomaly/Summary =
`CaseManager, EmergencyCoordinator`; ResourceMatch = `LogisticsManager,
EmergencyCoordinator`; ShelterMatch = `ShelterAdmin, EmergencyCoordinator`;
VolunteerMatch = `VolunteerCoordinator, EmergencyCoordinator`; Announcement and
CloseCase = `EmergencyCoordinator`. `SystemAdmin` has no break-glass decision
right.

## Implemented changes

### Outbox and notifications

- `CaseWriteHelper` now delegates to `IOutbox.EnqueueAsync`, so `EntityId`,
  `IdempotencyKey` and unwrapped payloads are persisted transactionally.
- Required Factory pattern added: `INotificationChannelFactory`,
  `INotificationChannel`, `InAppNotificationChannel`, `EmailNotificationChannel`
  and `NotificationChannelFactory`. `EmailNotificationHandler` resolves channels
  through the factory and skips already-sent rows on redelivery.
- `backend/db/02_notif.sql` idempotently creates `common.notification_templates`,
  seeds English (`en`) and Spanish (`es`) variants, and never overwrites local
  edits. `NotificationTemplates` mirrors both languages in code so rendering
  also works before the SQL seed runs.
- Announcements carry a language, record an immutable decision on approve or
  reject, and fan out only through `Notification.Send` outbox rows with stable
  per-recipient keys.

### AI decision support

- `LlmProvider` uses the shared named `cms-external` client through
  `IHttpClientFactory`, enforces the 5 s timeout, and sends only category,
  urgency, `AreaId` and scrubbed description.
- `AiProviderSelector` falls back to rules on HTTP failure, timeout, empty LLM
  output, or any suggestion below `0.5` confidence; `Source` becomes `Rule`.
- `RuleBasedProvider` duplicate candidates require the same category, the last
  24 hours, within 500 m and a text-similarity threshold; candidates are never
  merged or deleted automatically. Anomaly and crisis-summary rules are advisory
  only, with the summary route restricted to `EmergencyCoordinator`.
- `RequestSubmittedHandler` narrows duplicate candidates by category and the
  24-hour window; AI failures are swallowed so submission is never blocked.
- `POST /api/v1/recommendations/match` persists ranked matches plus alternatives
  and only advances `Triaged -> Recommended` where the contract allows it.
  M4 matcher implementations remain the property of Developer 4; Dev 3 only
  calls their contracts.

### Governance and review

- `ai.recommendations.Status` is added idempotently and mapped through the API.
  `RecommendationStatuses.Pending`/`Decided` are constants, not a new enum.
- `DecisionRecords` are append-only in both EF (`CmsDbContext`) and SQL
  (`TR_decision_records_immutable`). A second decision on the same recommendation
  is rejected.
- Approved/Modified `ResourceMatch` or `ShelterMatch` decisions move a
  `Recommended` request to `Approved` through `IRequestStatusService`.
- `GET /api/v1/post-incident-review` exposes `finalRequestStatus` and supports
  date and outcome filters in the service, not only in the UI fallback.

### Frontend, docs and API surface

- AI page makes no specific role claims; the decisions UI mirrors the exact
  role matrix and uses the required review route and filters.
- New FAQ chat page displays: **"Information only - this chat does not make or
  approve decisions."**
- Postman AI, Governance and Notifications folders cover the new routes; the
  decision request uses the exact singular `/decision` route.
- Swagger XML comments were added to the Dev 3 controllers; documentation
  generation and XML inclusion were already configured.
- README and TESTING document the corrected run order: `00_common.sql` →
  `02_notif.sql` → `03_ai.sql` → module scripts → `98_foreign_keys.sql` →
  `99_seed_users.sql` → seed data.

## Failure-injection coverage

| Scenario | Evidence |
|---|---|
| AI HTTP failure | `AiProviderSelectorTests.HttpFailure_FallsBackToRuleProvider` |
| AI timeout | `AiProviderSelectorTests.Timeout_FallsBackToRuleProvider`; `AiChatServiceTests.LlmFailure_FallsBackToFixedFaq` |
| Low-confidence LLM output | `AiProviderSelectorTests.LowConfidence_FallsBackToRuleProvider` |
| Map provider down | `HaversineTests.Map_provider_down_FallsBackToHaversine_WithoutBlockingOrClaimingPrecision` |
| Email down | `EmailNotificationHandlerTests.Handler_marks_the_email_row_failed_and_rethrows_when_delivery_fails`; `OutboxProcessorTests` retry/dead-letter coverage |
| Same outbox event twice | `EmailNotificationHandlerTests.Handler_is_idempotent_when_the_same_event_is_retried` |
| Dead-letter visibility/retry | `OutboxEndpointTests` |
| Role matrix | `DecisionRolePolicyTests.CanDecide_MatchesTheFrozenRoleMatrix` |
| Match governance | `MatchRecommendationServiceTests`, `DecisionServiceTests` |
| Review projection | `PostIncidentReviewServiceTests` |
| Templates and chat | `NotificationTemplateTests`, `AiChatServiceTests` |

## Verification

| Check | Result | Evidence |
|---|---|---|
| `dotnet restore backend/Cms.sln` | PASS | All projects up to date. |
| `dotnet build backend/Cms.sln --no-restore` | PASS | 0 warnings, 0 errors. |
| `dotnet test backend/Cms.sln --no-build` | PASS | 484 passed, 0 failed, 0 skipped. |
| `npm.cmd run typecheck` (in `frontend/`) | PASS | `tsc --noEmit` exited 0. |
| `npm.cmd run build` (in `frontend/`) | PASS | Next.js 16.4.6 compiled; 34/34 static pages generated. |
| Postman collection JSON | PASS | Parsed successfully after route updates. |

## Unresolved issues / assumptions

- M4 matcher implementations (`EmptyResourceMatcher`, `EmptyShelterRecommender`,
  `EmptyVolunteerMatcher`) are still stubs in this checkout. Dev 3 calls the
  contracts and does not overwrite Developer 4's modules.
- In this checkout the registered geo fallback is `HaversineOnlyGeoService`;
  the real provider/cache chain is Developer 4's work and was not modified.
- `backend/db/90_seed_scenario.sql` is referenced in the documented run order
  but is absent from this checkout.
- No reconciliation against a live database was performed; all SQL changes are
  idempotent additions and were reviewed only, not applied.
