# Developer 2 — contract requests and documented deviations

Branch: `feature/dev2-crisis`
Scope recorded: backend D2-01–D2-08 only. Frontend (D2-09/D2-10), partner workspace (D2-11), seed/UAT (D2-12/D2-13) and final report diagrams (D2-14) are not part of this change.

This branch predates several shared Block S contracts owned by Members 1, 3 and 4. No files owned by those members were modified. The backend compiles and is tested using module-local adapters/constants listed below. Each item needs an owner decision before integration.

## Member 1 — foundation, auth, config, audit, errors, wiring

| # | Missing contract | Current local workaround | Request |
|---|---|---|---|
| M1-1 | Shared `RequestStatus`, `Category`, `Urgency` in the agreed shared namespace | Module-local enums in `backend/src/Cms.Domain/Enums/Cases/` | Confirm the shared namespace and file paths; Dev2 will delete the local copies and switch usings. No shared enum values were changed. |
| M1-2 | `ICurrentUser` with `UserId`, `OrgId`, `Roles`, `IsInRole(role)` | Existing `ICurrentUserService` (`UserId`, `Email`, `Roles`, `IsAuthenticated`) is used | Ship the frozen interface or an adapter. Dev2 needs no org scoping in D2-01–D2-08, but the interface name differs from the contract. |
| M1-3 | `IAuditLogger.LogAsync(...)` that enlists in the caller's `SaveChanges` | `CaseWriteHelper.AddAuditAsync` writes `AuditLog` rows into the same `ApplicationDbContext` | Replace with the shared logger once it guarantees one `SaveChanges` and append-only behaviour. |
| M1-4 | `RoleNames` constants | Module-local `Cms.Domain.Cases.CasesRoles` using the exact frozen strings | Point Dev2 at the shared constants; replace all `[Authorize(Roles = ...)]` and service role checks. |
| M1-5 | Shared `PagedQuery` / `PagedResult<T>` | Module-local copies under `Cms.Application.Cases.Common` | Confirm namespace; Dev2 will delete local copies. Shape is `{items,total,page,pageSize}`. |
| M1-6 | RFC 7807 middleware adding `code` | Controller-local `CasesProblem.From` converter | Keep or replace with the shared middleware; ensure `code`, `errors` and status mappings remain: 404/403/409/422. |
| M1-7 | `AddCasesModule(IServiceCollection)` call in `Cms.Api/Program.cs` | Extension method exists at `Cms.Infrastructure.Cases.CasesModuleExtensions.AddCasesModule`, but is not called | **Required for runtime DI.** Member 1 must call it after the shared `TryAdd*` stubs so the real `IRequestReader` and `IRequestStatusService` win. |
| M1-8 | Persisted Data Protection key-ring location | `Cases:DataProtectionKeyRing`, default `App_Data/data-protection-keys`, created by `AddCasesModule` | Confirm the managed path and backup policy; avoid ephemeral keys in production. |
| M1-9 | SLA configuration key | `Cases:SlaHours`, default `24` | Confirm the canonical M1 config key. No per-urgency thresholds or scheduler were added. |
| M1-10 | Approved service types config | `Cases:ApprovedCategories`, default all six frozen values | Confirm the canonical key/source for the citizen form options. |
| M1-11 | SQL run-order filename | `db/01_cases.sql` header says `after 00_common.sql` as in the assigned document; this branch contains `backend/db/00_foundation.sql` | Confirm the final foundation script name/rename ownership. Dev2 did not edit M1's script. |
| M1-12 | `DbSets` / model registration hook | Case configurations are discovered by the existing assembly scan; services use `Set<T>()`, so no foundation `DbContext` edit was needed | Confirm the assembly scan includes `Cms.Infrastructure.Cases.Configurations` in the final integration branch. |

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
| M3-1 | `IOutbox.EnqueueAsync(type, entityId, payload, idempotencyKey)` | `CaseWriteHelper` inserts `OutboxMessage` rows with statuses `Pending` and the frozen key format | Replace with the shared outbox once available, preserving the single-`SaveChanges` rule and duplicate-key check. |
| M3-2 | `IOutboxHandler.Type` / event constants | String literals `"Request.Submitted"` and `"Notification.Send"` | Confirm constants or keep the strings frozen. |
| M3-3 | `IDecisionGate.RequireApprovedAsync(decisionId, expectedType, targetId)` and `RecommendationType.CloseCase` | Module-local `ICloseCaseDecisionGate`, registered as `UnavailableCloseCaseDecisionGate` that denies by default | Ship the real adapter. Until then the negative Critical-close path works; the positive path is proven with a test double only. |
| M3-4 | `Notification.Send` payload support in the notification handler | JSON envelope `{payload:{RecipientUserId,TemplateKey,Language,Data},idempotencyKey}` | Confirm the handler expects this envelope. Template key is `request.status.<Status>`; language comes from `Cases:DefaultLanguage`. |
| M3-5 | Decisions read API for the case timeline | Backend detail returns status history; no AI recommendation data is queried | Frontend D2-10 must call `GET /api/v1/recommendations?targetId=`; no direct `ai.*` query was added. |

## Member 4 — geo, matchers, public shelters

| # | Missing contract | Current local workaround | Request |
|---|---|---|---|
| M4-1 | `IGeoService` and `MapPicker` | Backend accepts validated `Lat`/`Lng` only | Frontend D2-09 will use the plain lat/lng fallback until M4 ships. |
| M4-2 | `IResourceMatcher`, `IShelterRecommender`, `IVolunteerMatcher` | Not referenced by Dev2 backend | Confirm the match endpoint contract used by the coordinator flow. |
| M4-3 | `GET /api/v1/shelters/public` | Dev2 exposes only its own public alerts and service centres | Frontend D2-09 consumes M4's endpoint directly; nothing is proxied. |
| M4-4 | Seed inventory for the shortage demo | Not inserted by Dev2 in this backend-only change | Reserve the inventory GUID/resource type/quantity with M4 before D2-12 adds `db/90_seed_scenario.sql`. |

## Documented implementation deviations

1. **Staff assignment route:** D2-05 defines `PATCH /api/v1/requests/{id}` for citizen edits and D2-07 defines the same path for staff assignment. Both are supported: the route authorizes `CasesRoles.RequestReaders`, and `RequestService.UpdateMineAsync` applies `AssignedToUserId` only for staff. A dedicated `PATCH /api/v1/requests/{id}/assign` alias is also provided.
2. **`Address` and `EvidenceAttachmentIds`:** accepted on submit for forward compatibility but not persisted by the D2-01 entity shape. Evidence is uploaded after the request exists via the attachment endpoint.
3. **`IRequestStatusService` overload:** the frozen signature is untouched. A module-local `IRequestDecisionTransitionService` carries an already-checked `decisionId` into the same `RequestStatusService`, so the Critical-close rule remains in the single status-write service.
4. **Cross-module reader decryption:** `RequestReader` decrypts `AccessibilityNeeds` through Data Protection before returning the frozen DTO. It remains `AsNoTracking` and read-only. Confirm whether M3/M4 should receive plaintext via this internal contract or a masked value.
5. **Plain SQL, no EF migration:** D-01 is still pending sign-off. No migration was added or deleted. SQL Server integration verification (run-twice idempotency and `HasPendingModelChanges`) could not be executed in this environment; the script and EF configurations were kept name-for-name aligned by inspection.
6. **Role checks:** all endpoints have explicit `Authorize(Roles=...)` or `AllowAnonymous`; service-level scoping and staff checks are also present.
