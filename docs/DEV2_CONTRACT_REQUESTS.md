# Developer 2 — contract requests and documented deviations

Current branch: `feature/dev2-crisis`, fast-forwarded to local `develop` merge `291dfe5` (the integrated DEV2 result).
Scope recorded: D2-01–D2-10, D2-12–D2-14 implementation/evidence seams. D2-11 partner workspace is P2 and must not start before CP2 (Mon 23 Nov 2026); the feature freeze is Tue 24 Nov 23:59.

This document tracks cross-member integration dependencies and documented deviations. No files owned by Members 1, 3 or 4 were modified. The merged baseline at `291dfe5` passed `dotnet restore`, `dotnet build` (0 warnings/0 errors), `dotnet test` (504 tests), frontend typecheck and frontend production build. The current reviewed tree also passes `dotnet build` (0 warnings/0 errors), `dotnet test` (508 tests) and `web` `npm.cmd run build` (type checking included). These are source/build checks only; no runtime API, Postman execution, database execution or UAT pass is claimed.

## Member 1 — foundation, auth, config, audit, errors, wiring

| # | Missing contract | Current local workaround | Request |
|---|---|---|---|
| M1-1 | Shared `RequestStatus`, `Category`, `Urgency` in the agreed shared namespace | Module-local enums in `backend/src/Cms.Domain/Enums/Cases/` | Confirm the shared namespace and file paths; Dev2 will delete the local copies and switch usings. No shared enum values were changed. |
| M1-2 | `ICurrentUser` with `UserId`, `OrgId`, `Roles`, `IsInRole(role)` | Existing `ICurrentUserService` (`UserId`, `Email`, `Roles`, `IsAuthenticated`) is used | Ship the frozen interface or an adapter. Dev2 needs no org scoping in D2-01–D2-08, but the interface name differs from the contract. |
| M1-3 | `IAuditLogger.LogAsync(...)` that enlists in the caller's `SaveChanges` | `CaseWriteHelper.AddAuditAsync` writes `AuditLog` rows into the same `ApplicationDbContext` | Replace with the shared logger once it guarantees one `SaveChanges` and append-only behaviour. |
| M1-4 | `RoleNames` constants | `Cms.Domain.Cases.CasesRoles` now aliases `Cms.Domain.Constants.RoleNames`; `CaseStaff = EmergencyCoordinator,CaseManager` and `CoordinatorOnly = EmergencyCoordinator`. | Confirm the M1 seed roles use the same frozen strings; legacy `ShelterAdministrator`, `PartnerOrganisationOfficer` and `SystemAdministrator` must be corrected before integration. |
| M1-5 | Shared `PagedQuery` / `PagedResult<T>` | Module-local copies under `Cms.Application.Cases.Common` | Confirm namespace; Dev2 will delete local copies. Shape is `{items,total,page,pageSize}`. |
| M1-6 | RFC 7807 middleware adding `code` | Controller-local `CasesProblem.From` converter | Keep or replace with the shared middleware; ensure `code`, `errors` and status mappings remain: 404/403/409/422. |
| M1-7 | `AddCasesModule(IServiceCollection, IConfiguration)` composition | `CasesModule` delegates to `CasesModuleExtensions.AddCasesModule`, and `ModuleRegistration.AddCmsModules` calls it after the identity module. Unit tests assert all DEV2 service registrations and that M3 can override the close gate. | Source-level blocker resolved. Confirm controller activation in a clean API run; do not assume a runtime pass until then. |
| M1-8 | Persisted Data Protection key-ring location | `Cases:DataProtectionKeyRing`, default `App_Data/data-protection-keys`, created by `AddCasesModule` | Confirm the managed path and backup policy; avoid ephemeral keys in production. |
| M1-9 | SLA configuration key | `Cases:SlaHours`, default `24` | Confirm the canonical M1 config key. No per-urgency thresholds or scheduler were added. |
| M1-10 | Approved service types config | `Cases:ApprovedCategories`, default all six frozen values | Confirm the canonical key/source for the citizen form options. |
| M1-11 | SQL run-order filename | `backend/db/01_cases.sql` header says `after 00_common.sql`, matching the shipped foundation script. | Confirm the final foundation script name/rename ownership. Dev2 did not edit M1's script. A historical LocalDB run of the then-current foundation script plus `01_cases.sql` twice created exactly the 11 `cases` tables; that run was not repeated during this integration. |
| M1-12 | `DbSets` / model registration hook | Case configurations are discovered by the existing assembly scan; services use `Set<T>()`, so no foundation `DbContext` edit was needed | Confirm the assembly scan includes `Cms.Infrastructure.Cases.Configurations` in the final integration branch. |
| M1-13 | ProblemDetails-preserving shared `web/lib/api.ts` | `web/components/cases/casesApi.ts` reads `message`/`code`; the shared client flattens RFC 7807 `title`/`detail`/`errors`. | M1 owns `web/lib/api.ts`; preserve `title`, `detail` and `errors`, add token refresh, and let DEV2 remove its local fallback parser. |
| M1-14 | React Hook Form and Zod in `web/package.json` | DEV2 forms use native accessible validation because the libraries are not declared. | Add the dependencies and confirm the agreed schema before DEV2 converts forms; no DEV2 edit to shared `web/package.json`. |
| M1-15 | Role-aware guard/403 and user lookup for `/web` | DEV2 route groups provide role-appropriate navigation, but the shared root layout has no auth guard and assignee filters/assign inputs use raw user IDs. | Provide the guard and a user lookup endpoint/component; do not expect DEV2 to edit the shared root layout. |
| M1-16 | KPI fields for open, overdue, average response, AI override and assignee workload | The dashboard renders only fields present in `KpiReport`; missing fields are shown as explicit dependency placeholders and are not derived client-side. | Extend the M1 KPI contract; DEV2 will bind the cards once the fields exist. |

### Cross-module FK handoff for `db/98_foreign_keys.sql`

Member 1 owns this file. Dev2 intentionally declared **no cross-module FK** in `db/01_cases.sql`.

```sql
cases.requests.requester_user_id        -> identity.users.id
cases.requests.assigned_to_user_id      -> identity.users.id
cases.households.user_id                -> identity.users.id
cases.partner_shares.organisation_id    -> common.organisations.id
```

`cases.requests.crisis_id`, `cases.requests.area_id`, `cases.areas.crisis_id`, `cases.alerts.crisis_id` and `cases.service_centres.crisis_id` are intra-schema and are already declared locally.

## Member 3 — AI, decisions, outbox, notifications

| # | Missing contract | Current local workaround | Request |
|---|---|---|---|
| M3-1 | `IOutbox.EnqueueAsync(type, entityId, payload, idempotencyKey)` | `CaseWriteHelper.AddOutboxAsync` now delegates to the shared `IOutbox`; it does not write outbox rows directly. | Confirm at runtime that duplicate idempotency keys are ignored and that the caller's single `SaveChangesAsync` commits the business row, audit row and outbox row together. `common.outbox_messages.idempotency_key` has unique index `UX_outbox_idempotency_key`. |
| M3-2 | `IOutboxHandler.Type` / event constants | String literals `"Request.Submitted"` and `"Notification.Send"` | Confirm constants or keep the strings frozen. |
| M3-3 | `IDecisionGate.RequireApprovedAsync(decisionId, expectedType, targetId)` and `RecommendationType.CloseCase` | DEV2 registers `DecisionGateCloseCaseAdapter` with `TryAddScoped`; M3 registers the real governance adapter later in `AiModule`, so M3 wins in composed startup. Negative path is unit-tested; positive path has a test double only. | Confirm in a clean API run with an approved `CloseCase` decision. Runtime composition is source-wired but unverified. |
| M3-4 | `Notification.Send` template keys | DEV2 emits `{RecipientUserId,RequestId,TemplateKey,Language,Data}` with `TemplateKey = request.status.<Status>` and `Language` from `Cases:DefaultLanguage`, as frozen in D2-04. | M3's notification registry currently seeds `Request.StatusChanged` (and `Request.Assigned`), not the status-specific keys. Unknown keys fall back to a humanised subject and generic data body, so delivery is not blocked, but localised templates are not selected. M3 must add/map `request.status.*` keys; DEV2 must not change the frozen key format. |
| M3-5 | Decisions read API for the case timeline | Backend detail returns status history; no AI recommendation data is queried | Source audit found `GET /api/v1/recommendations?targetId=` and `GET /api/v1/recommendations/target/{targetId:guid}`. Frontend D2-10 can use these; no direct `ai.*` query was added. Runtime data still requires M3. |
| M3-6 | Exported M3 decision/recommendation UI component and decisionId discovery | DEV2 keeps `RecommendationPanel` read-only and does not duplicate the M3 decision form; critical close currently requires a manually supplied approved `CloseCase` decision ID. | Export/import the M3 component and provide an in-app way to obtain the approved decision ID. |
| M3-7 | Decision records in the per-case timeline | DEV2 `Timeline.tsx` renders status history only. | Supply decision records through the M3 read API; DEV2 will merge them into the same case timeline without building a second decision store. |

## Member 4 — geo, matchers, public shelters

| # | Missing contract | Current local workaround | Request |
|---|---|---|---|
| M4-1 | `IGeoService` and `MapPicker` | Backend accepts validated `Lat`/`Lng` only; DEV2 pages consume M4's `MapPicker`/`MapView` read-only and keep a permanent lat/lng fallback. | Confirm M4 maintains the map components and that the fallback remains acceptable when map assets are unavailable. |
| M4-2 | `IResourceMatcher`, `IShelterRecommender`, `IVolunteerMatcher` | Not referenced by Dev2 backend | Confirm the match endpoint contract used by the coordinator flow. |
| M4-3 | `GET /api/v1/shelters/public` | Dev2 exposes only its own public alerts and service centres | Frontend D2-09 consumes M4's endpoint directly; nothing is proxied. |
| M4-4 | Seed inventory for the shortage demo | `backend/db/91_seed_ops.sql` now reserves resource type `41000000-...0001`, inventory `42000000-...0001` and quantity 5; DEV2 seed only creates R6–R8. | No action for DEV2. Confirm M4 still owns that fixture and that `91_seed_ops.sql` runs after `90_seed_scenario.sql`. |
| M4-5 | Match and allocation route contract | `GET /api/v1/recommendations/match/resources/{requestId}` is present in M4's `OperationsController`; allocation still requires an approved `ResourceMatch` decision. | Confirm route behaviour in a clean run and keep M4 ownership of match/allocation services. |

## Documented implementation deviations

1. **Staff assignment route:** D2-05 defines `PATCH /api/v1/requests/{id}` for citizen edits and D2-07 defines the same path for staff assignment. Both are supported: the route authorizes `CasesRoles.RequestReaders`, and `RequestService.UpdateMineAsync` applies `AssignedToUserId` only for staff. A dedicated `PATCH /api/v1/requests/{id}/assign` alias is also provided.
2. **`Address` and `EvidenceAttachmentIds`:** `address` is now persisted on `cases.requests`, mapped in EF, returned on request detail, and accepted by the updated seed script. A non-empty `evidenceAttachmentIds` array is rejected with `422 EVIDENCE_ATTACHMENT_IDS_NOT_SUPPORTED` instead of being silently discarded; evidence must be uploaded after the request exists via `POST /api/v1/requests/{id}/attachments`. The DTO field remains only to produce an explicit error for stale clients.
3. **`IRequestStatusService` overload:** the frozen signature is untouched. A module-local `IRequestDecisionTransitionService` carries an already-checked `decisionId` into the same `RequestStatusService`, so the Critical-close rule remains in the single status-write service.
4. **Cross-module reader decryption:** `RequestReader` decrypts `AccessibilityNeeds` through Data Protection before returning the frozen DTO. It remains `AsNoTracking` and read-only. Confirm whether M3/M4 should receive plaintext via this internal contract or a masked value.
5. **Plain SQL, no EF migration:** D-01 is still pending sign-off. No migration was added or deleted. A historical LocalDB run of the then-current foundation script followed by `01_cases.sql` twice succeeded with `sqlcmd -I`; exactly the 11 `cases` tables were created. `dotnet ef migrations has-pending-model-changes` reports pending changes because the M1-owned snapshot does not include the cases model; resolving this requires an owner decision and must not be done by adding a DEV2 EF migration.
6. **Role checks:** all endpoints have explicit `Authorize(Roles=...)` or `AllowAnonymous`; service-level scoping and staff checks are also present. D2-07/D2-08 write roles are exactly `EmergencyCoordinator,CaseManager` and `EmergencyCoordinator`; `SystemAdmin` is not included.
7. **Overdue and urgency semantics:** `/requests/overdue` and crisis overdue totals now count only open requests older than the configured SLA; urgency sorting uses `Critical > High > Medium > Low` instead of database string order.
8. **Outbox idempotency scope:** `CaseWriteHelper` delegates to the shared `IOutbox`; duplicate keys are ignored by the shared service and constrained by `UX_outbox_idempotency_key` on `common.outbox_messages.idempotency_key`. Runtime replay still needs a clean API check.
9. **Coordinator queue route:** route groups do not change URLs, so the coordinator queue was moved from `/requests` to `/coordinator/requests` to avoid a duplicate Next.js route with the citizen portal. The citizen routes remain `/requests`, `/requests/{id}` and `/requests/new`; coordinator links were updated with the new queue paths.
