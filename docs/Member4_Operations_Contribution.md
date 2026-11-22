# Member 4 — Operations contribution (final)

## Ownership

This contribution is the Member 4 vertical slice of the modular monolith: M5 Resources, M6 Shelters, M7 Volunteers and M10 Mapping, plus the D4-13 report/diagram deliverables. It does not replace Member 1's foundation, Member 2's case workflow or Member 3's AI/governance code. All business rules live behind `IOperationsService` (`backend/src/Cms.Application/Operations/OperationsContracts.cs`) implemented in `backend/src/Cms.Infrastructure/Operations/OperationsService.cs`; `OperationsController.cs` only maps HTTP, roles and status codes.

`/web` is Member 4's separate operations web app/handoff area (logistics, shelters, volunteer and map pages with `MapPicker`/`MapView`). The integrated application's active Next.js app is under `frontend/`; merging the two remains a team decision.

## What is implemented

- **Resources (D4-04/05/06/09):** resource types, paged inventories with all six stock buckets and a low-stock flag derived from `ReorderThreshold`, stock corrections, approved-decision allocation with a conditional SQL stock guard, dispatch and delivery confirmation that move `Allocated -> Dispatched -> Utilised`, paged allocation/dispatch reads, read-only shortage proposal and assistance requests.
- **Shelters (D4-07):** staff and anonymous public lists, create/update/approve, and decision-gated household allocation guarded by a conditional occupancy UPDATE.
- **Volunteers (D4-08):** self-service profile and availability, coordinator verification, task creation/assignment/response/completion with activity records, and read-only matching limited to verified, active, available volunteers.
- **Mapping (D4-02):** `GeoService` behind `IGeoService`, using named `nominatim` and `osrm` clients registered with the required `CMS-Humanitarian/1.0 (cms-team@example.org)` User-Agent in `LogisticsModule`. Address is normalised and lower-cased for the cache key; success caches for 24 h, failures for at most 1 minute; Nominatim calls are spaced to 1 req/s with `SemaphoreSlim` + `TimeProvider`. OSRM success returns km + minutes and `IsApproximate=false`; any provider failure returns Haversine km (2 decimals), `Minutes=null`, `IsApproximate=true`, cached for 1 minute. Provider errors never reach the caller. No full address is logged.
- **Matching:** read-only, deterministic (`score desc`, then `TargetId asc`) candidates with score 0..1, readable rationale and approximate flag. Weights are documented in one comment block in `OperationsService.cs`: resource 0.25 type fit / 0.20 available quantity / 0.15 distance / 0.15 urgency / 0.10 expiry / 0.10 accessibility / 0.05 org ownership; shelter 0.30 availability / 0.25 suitability / 0.25 distance / 0.10 services / 0.10 org ownership; volunteer 0.45 skills / 0.25 availability / 0.20 distance / 0.10 request context. The older `WeightedResourceMatchScoringStrategy` (60/25/15) still exists in `Cms.Infrastructure/Services/Operations/` and is covered by a unit test, but is not the runtime matcher.
- **Cross-module contracts (now real, not stubs):** `IDecisionGate.RequireApprovedAsync` (Member 3), `IRequestReader` and `IRequestStatusService` (Member 2), `IOutbox`, `IAuditLogger` and `ICurrentUser` (Member 1). `FrozenOperationAdapters.cs` forwards the real geo/matcher implementations to the frozen shared interfaces so other modules do not resolve the fail-closed stubs.

## Decision gates, transaction boundaries and recovery

`POST /allocations` requires exactly `{"decisionId":"..."}` plus the `Idempotency-Key` header (missing -> 400 `IDEMPOTENCY_KEY_REQUIRED`, >256 chars -> validation error). The service re-reads the decision record, requires an Approved/Modified outcome, calls the decision gate for `ResourceMatch` and takes `requestId`, `inventoryId` and `quantity` **only** from the approved `FinalPayloadJson`. One explicit transaction performs `UPDATE res.inventories SET available = available - @q, allocated = allocated + @q ... WHERE id = @id AND available >= @q`, inserts the allocation with the unique idempotency key and queues the outbox notification; zero updated rows roll back to 409 `INSUFFICIENT_STOCK`. A duplicate key rolls back and returns the already-committed allocation with 200.

Phase 3 (`IRequestStatusService.TransitionAsync(..., Allocated)`) runs **outside** that transaction. A failure is audited as `Allocation.StatusTransitionFailed` with a retry hint; stock and allocation stay committed. The supported recovery is `GET /operations/status-transitions` followed by `POST /operations/status-transitions/{id}/retry` with the allocation or dispatch id — the retry only replays the status transition. Repeating the same idempotency key returns the existing allocation early and does **not** re-run Phase 3, so the explicit recovery endpoint is the reliable path.

Shelter allocation follows the same shape: approved `ShelterMatch` decision payload supplies shelter/request/household size, one transaction performs `UPDATE shelter.shelters SET occupancy = occupancy + @n ... WHERE id = @id AND approved = 1 AND occupancy + @n <= capacity` and inserts the allocation; zero rows -> rollback and 409 `SHELTER_FULL`. The `Occupancy <= Capacity` CHECK is defence in depth. Dispatch and confirmation never touch `Available` again; they guard bucket transfers with `allocated >= q` / `dispatched >= q` and return 409 `INVALID_TRANSITION` or `INVENTORY_BUCKET_MISMATCH` where appropriate. Repeated dispatch/confirm calls return the existing row.

## Roles

- Staff reads (`resources`, `inventories`, `allocations`, `dispatches`, `shortages/proposal`, `operations/status-transitions`): LogisticsManager, EmergencyCoordinator, SystemAdmin.
- Stock/resource writes: LogisticsManager or SystemAdmin; allocation POST additionally allows EmergencyCoordinator.
- Shelters: ShelterAdmin/EmergencyCoordinator/SystemAdmin for staff list; ShelterAdmin/SystemAdmin for create/edit; EmergencyCoordinator/SystemAdmin for approve; ShelterAdmin/EmergencyCoordinator for allocation; `shelters/public` is anonymous and rate-limited.
- Volunteers: Volunteer for profile/availability/own tasks/respond/complete; VolunteerCoordinator/SystemAdmin for verify and task create/assign; coordinator or emergency coordinator for the task list.
- Assistance: LogisticsManager/ShelterAdmin/PartnerOfficer for create/list; PartnerOfficer/LogisticsManager/SystemAdmin for respond with target-org enforcement in the service.
- Matching: resources (LogisticsManager/EmergencyCoordinator), shelters (ShelterAdmin/EmergencyCoordinator), volunteers (VolunteerCoordinator/EmergencyCoordinator).
- `geo/geocode` and `geo/distance` are anonymous proxies.

## Database

Member 4 scripts are `backend/db/04_res.sql`, `05_shelter.sql`, `06_vol.sql` and `91_seed_ops.sql`. They are idempotent, snake_case, use fixed GUIDs for the 5-unit emergency food pack that the three urgent seeded Food requests compete for, and rely on `90_seed_scenario.sql` (Member 2) for those requests. `07_geo.sql` and the `GeoCache` entity are present but unused because mapping caches in memory; confirm the README run order with Member 1. No EF migration was added.

## Cross-module FK handoff for Member 1 (do not edit 98_foreign_keys.sql)

Member 4 hands off the following foreign-key relationships for Member 1 to add centrally:

- `res.allocations.request_id` -> `cases.requests.id`
- `res.allocations.inventory_id` -> `res.inventories.id` (same module)
- `res.dispatches.allocation_id` -> `res.allocations.id` (same module)
- `shelter.shelter_allocations.request_id` -> `cases.requests.id`
- `vol.tasks.request_id` -> `cases.requests.id` (nullable)
- `res.assistance_requests.shelter_id` -> `shelter.shelters.id`
- `vol.activities.task_id` -> `vol.tasks.id`

Member 4 did **not** modify `backend/db/98_foreign_keys.sql`.

## Verification (honest status — 22 Nov 2026)

- `dotnet build Cms.sln`: **succeeded, 0 warnings, 0 errors** (log: `docs/evidence/dev4/build.log`).
- `dotnet test Cms.sln --no-build`: **561/561 passed** (log: `docs/evidence/dev4/test.log`). The current tree includes 53 tests in the `Cms.Tests.Operations` namespace: 7 `OperationsGeoAdapterTests` (fake HTTP handlers for geocode success/failure caching, normalised key, OSRM success/failure, Haversine fallback, coordinate validation and User-Agent), 6 `OperationsMatcherTests`, 16 `OperationsServiceTests`, 20 `OperationsValidationTests` and 4 `OperationsShortageTests`. The older 3 `OperationsGeoTests`, 1 `OperationsScoringTests` and 5 `HaversineTests` are still present. These are unit/fake-HTTP/in-memory tests; they do **not** prove SQL transaction behaviour or the 1 req/s Nominatim throttle under real timing.
- Not run / not claimed: real-transaction SQL Server LocalDB 20-iteration allocation and shelter races, same-key idempotency under concurrency, simulated Phase 3 failure against a real request store, explicit 1 req/s throttle timing, Postman/Newman live run, and the full shortage UAT demonstration. `sqllocaldb info` does list an `MSSQLLocalDB` instance, but no concurrency harness is checked in; those results must be captured before claiming the LocalDB acceptance items.

`docs/Member4_Report_Section_Draft.md` contains the D4-13 design and testing sections; `docs/Member4_INDIVIDUAL_REFLECTION.md` contains the individual reflection.
