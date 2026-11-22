# Member 4 file manifest — final working tree

Paths below are relative to the repository root and reflect the **actual** files in the current working tree (checked against `git status --porcelain` on 22 Nov 2026), not the older `Cms.Infrastructure/Services/Operations/...` paths from earlier drafts.

## New or modified in this delivery (Member 4)

### API and application boundary

- `backend/src/Cms.Api/Controllers/OperationsController.cs` — modified; all 40 P1 endpoints, thin HTTP mapping, role attributes, `Idempotency-Key` header binding, anonymous map/shelter reads.
- `backend/src/Cms.Api/Modules/LogisticsModule.cs` — modified; registers `IOperationsService`, named `nominatim`/`osrm` HTTP clients with the required User-Agent, memory cache, real geo/matchers and adapters that replace the fail-closed shared stubs.
- `backend/src/Cms.Application/Operations/OperationsContracts.cs` — new; DTOs, input models and the single `IOperationsService` boundary.

### Domain

- `backend/src/Cms.Domain/Entities/Operations/Dispatch.cs` — modified (confirmation fields/status).
- `backend/src/Cms.Domain/Entities/Operations/Volunteer.cs` — modified (verification reason/status).
- The remaining operations entities are present and unchanged: `ResourceType.cs`, `Inventory.cs`, `Allocation.cs`, `Shelter.cs`, `ShelterAllocation.cs`, `AssistanceRequest.cs`, `VolunteerAvailability.cs`, `VolunteerTask.cs`, `VolunteerActivity.cs` under `backend/src/Cms.Domain/Entities/Operations/`.
- `backend/src/Cms.Domain/Operations/GeoDistance.cs` — pure Haversine helper used by `OperationsGeoTests`.

### Infrastructure (current runtime paths)

- `backend/src/Cms.Infrastructure/Operations/OperationsService.cs` — new; all business rules, transactions, guards, audit/outbox and retry paths.
- `backend/src/Cms.Infrastructure/Operations/OperationsMatchers.cs` — modified; `ResourceMatcher`, `ShelterRecommender`, `VolunteerMatcher` plus shared helper logic.
- `backend/src/Cms.Infrastructure/Operations/GeoService.cs` — modified; Nominatim + OSRM + bounded caches + Haversine fallback.
- `backend/src/Cms.Infrastructure/Operations/FrozenOperationAdapters.cs` — new; bridges operations contracts to the frozen shared contracts used by Member 3 and other modules.
- `backend/src/Cms.Infrastructure/Data/Configurations/Operations/OperationsConfigurations.cs` — modified; EF entity configurations for the operations entities.
- `backend/src/Cms.Infrastructure/Data/Configurations/OperationsConfigurations.cs` — modified; legacy flat configuration file still present in the tree.

### Database scripts

- `backend/db/04_res.sql` — modified; `res` schema, guarded inventory buckets, unique idempotency key, dispatch/assistance tables, indexes.
- `backend/db/05_shelter.sql` — modified; `shelter` schema, capacity/occupancy CHECKs, indexes.
- `backend/db/06_vol.sql` — modified; `vol` schema, one profile per user, availability/tasks/activities, indexes.
- `backend/db/91_seed_ops.sql` — modified; idempotent fixed-GUID demo seed (inventories, 3 shelters, 5 volunteers, availability) matching the 3 urgent Food requests in `backend/db/90_seed_scenario.sql` (owned by Member 2).
- `backend/db/07_geo.sql` — present in the repository but **not used** by the current runtime: caching is `IMemoryCache` in `GeoService`. Confirm with Member 1 whether to keep it in the README run order.

### Tests present for Member 4 (none edited by this documentation pass)

- `backend/tests/Cms.Tests/OperationsGeoTests.cs` — 3 tests against the pure `GeoDistance` helper.
- `backend/tests/Cms.Tests/OperationsScoringTests.cs` — 1 test against the legacy `WeightedResourceMatchScoringStrategy`.
- `backend/tests/Cms.Tests/Infrastructure/HaversineTests.cs` — 5 fallback-shape tests against `HaversineOnlyGeoService` stub.
- `backend/tests/Cms.Tests/Operations/OperationsTestSupport.cs` — shared in-memory EF harness and fixed seeds; no test cases.
- `backend/tests/Cms.Tests/Operations/OperationsGeoAdapterTests.cs` — 7 tests: fake HTTP handlers for geocode success/failure caching, normalised cache key, OSRM success/failure, Haversine fallback, coordinate validation and configured User-Agent.
- `backend/tests/Cms.Tests/Operations/OperationsMatcherTests.cs` — 6 deterministic, read-only ranking tests.
- `backend/tests/Cms.Tests/Operations/OperationsServiceTests.cs` — 16 decision-gate, payload-guard, allocation/shelter/transition tests.
- `backend/tests/Cms.Tests/Operations/OperationsValidationTests.cs` — 20 service-level validation/guard tests.
- `backend/tests/Cms.Tests/Operations/OperationsShortageTests.cs` — 4 side-effect-free shortage ranking tests.
- No real-transaction LocalDB concurrency test file is checked in; the 20-iteration races remain planned, not passed.

### Member 4 UI handoff (separate web app under `/web`)

- `web/package.json`, `web/tsconfig.json`, `web/next-env.d.ts`, `web/README.md`, `web/lib/api.ts`
- `web/app/layout.tsx`, `web/app/style.css`
- `web/app/(logistics)/logistics/page.tsx`, `web/app/(logistics)/shelters/page.tsx`
- `web/app/(volunteer)/volunteer/page.tsx`, `web/app/maps/page.tsx`
- `web/components/map/MapPicker.tsx`, `web/components/map/MapPickerInner.tsx`, `web/components/map/MapView.tsx`, `web/components/map/MapViewInner.tsx`

`/web` is Member 4's separate operations web app/handoff area. The integrated application's active Next.js app is under `frontend/`; the two are not the same app.

### Documentation and API test collection

- `docs/postman/Member4_Operations.postman_collection.json` — modified; 41 requests covering all 40 owned P1 endpoints plus one negative idempotency-key probe; collection bearer auth, variables and ProblemDetails tests.
- `docs/Member4_Operations_Contribution.md` — modified.
- `docs/Member4_Component_Diagram.mmd` — modified.
- `docs/Member4_File_Manifest.md` — modified.
- `docs/Member4_Report_Section_Draft.md` — modified.
- `docs/Member4_DELIVERY_STATUS.md` — modified.
- `docs/Member4_INDIVIDUAL_REFLECTION.md` — new.
- `docs/evidence/dev4/build.log`, `docs/evidence/dev4/test.log` — new, captured from real commands (see delivery status).

## Files present but not on the current runtime path (do not describe as active)

- `backend/src/Cms.Infrastructure/Services/Operations/WeightedResourceMatchScoringStrategy.cs` — legacy 60/25/15 scorer still referenced by `OperationsScoringTests.cs`; the runtime matcher is `Cms.Infrastructure/Operations/OperationsMatchers.cs` with the 0.25/0.20/0.15/0.15/0.10/0.10/0.05 weights documented in `OperationsService.cs`.
- `backend/src/Cms.Domain/Entities/Mapping/GeoCache.cs` — entity exists for the skipped database-cache option; `GeoService` uses IMemoryCache.
- `backend/src/Cms.Application/Contracts/Operations/GeoContracts.cs` — frozen operations-shaped contracts still present; the current service uses `Cms.Application/Operations/OperationsContracts.cs` and the adapters in `FrozenOperationAdapters.cs` to bridge to shared contracts.

## Not touched by this pass

Member 1 foundation, Member 2 cases/requests, Member 3 AI/governance, `web/` UI source, `frontend/`, `README.md`, `docs/postman/Cms.postman_collection.json`, all other members' docs/evidence, and all files under `backend/tests/`, `backend/db/` (other than the four Member 4 scripts listed above), and `backend/src/` (other than the Member 4 files listed above). No file under `backend/tests/` was edited by this documentation pass; the test files listed above were already present in the working tree when the docs were finalised. Build artefacts (`bin/`, `obj/`) are generated output, not authored deliverables.
