# Member 4 delivery status — 22 November 2026

This supersedes the earlier 21 Nov draft. It records what is actually in the working tree and what has been executed, without claiming unrun evidence.

## Included in this delivery

- **API/application:** `OperationsController.cs` with all 40 P1 endpoints owned by Member 4; `Cms.Application/Operations/OperationsContracts.cs`; `Cms.Infrastructure/Operations/OperationsService.cs`, `OperationsMatchers.cs`, `GeoService.cs`, `FrozenOperationAdapters.cs`; updated `LogisticsModule.cs` DI/registration.
- **Domain/EF:** operations entities under `Cms.Domain/Entities/Operations`, pure Haversine helper, EF configurations under `Cms.Infrastructure/Data/Configurations/Operations/`.
- **Database:** `backend/db/04_res.sql`, `05_shelter.sql`, `06_vol.sql`, `91_seed_ops.sql`. `07_geo.sql` and the `GeoCache` entity are present but unused because mapping uses `IMemoryCache`; confirm the README run order with Member 1. `90_seed_scenario.sql` (Member 2) and `99_seed_users.sql` are present in this tree; the seed GUIDs are aligned with the three urgent Food requests R6–R8. No EF migration was added or modified by this pass.
- **UI:** `MapPicker`/`MapView` and logistics/shelter/volunteer/map pages under `/web`. This is **Member 4's separate web app/handoff area**, not the active integrated application under `frontend/`. Moving or merging it is a team decision; do not describe `/web` as the running `frontend/`.
- **Docs/tests collection:** updated `Member4_Operations_Contribution.md`, `Member4_Component_Diagram.mmd`, `Member4_File_Manifest.md`, `Member4_Report_Section_Draft.md`, this status file, new `Member4_INDIVIDUAL_REFLECTION.md`, and `docs/postman/Member4_Operations.postman_collection.json` (41 requests = 40 owned P1 endpoints + one negative idempotency-key probe, with variables, role notes and ProblemDetails/guard tests).
- **Evidence:** `docs/evidence/dev4/build.log` and `docs/evidence/dev4/test.log`, captured from real commands on this machine.

## Verified (actually executed on 22 Nov 2026)

- .NET SDK 11.0.100 available; `dotnet build Cms.sln` from `backend`: **Build succeeded, 0 Warning(s), 0 Error(s)**.
- `dotnet test Cms.sln --no-build` from `backend`: **Test Run Successful — 561 total, 561 passed**. This includes 53 `Cms.Tests.Operations` tests (7 fake-HTTP geo/caching, 6 matcher, 16 service, 20 validation, 4 shortage), the older Haversine/legacy-scoring tests, and the rest of the team's suite.
- `sqllocaldb info` lists an `MSSQLLocalDB` instance. That is an environment check only; it is **not** a concurrency test result.
- Postman collection parses as valid JSON (verified with `ConvertFrom-Json`) and its request list was checked endpoint-by-endpoint against `OperationsController.cs`.

## Still needs team / LocalDB evidence — do not claim as passed

1. Real-transaction SQL Server LocalDB races, each repeated 20 times: last-unit allocation (one 201 / one 409 `INSUFFICIENT_STOCK`, `Available >= 0`), last-place shelter allocation (one success / one 409 `SHELTER_FULL`, `Occupancy <= Capacity`), same-idempotency-key sequential and concurrent replay (one row, both 200), Phase 3 failure plus explicit retry, and full dispatch/delivery bucket consistency. No concurrency harness is checked in and none of these races was run.
2. Real-timer proof of the 1 req/s Nominatim throttle. Fake-HTTP tests for the real `GeoService` already pass for the 24 h success cache, <=1 min failure cache, OSRM-down Haversine fallback (`IsApproximate=true`, `Minutes=null`), configured User-Agent and no Nominatim call from `GetDistanceAsync`; they do not measure elapsed request spacing.
3. Fresh-database run of `00`→`01`→`02`→`03`→`04`→`05`→`06`→(07 optional)→`90`→`91` and `98`, including script re-run and CHECK-constraint rejection probes.
4. Live Postman/Newman execution against a running API with a seeded decision, plus shortage-demo output and screenshots for the report.
5. End-to-end UAT of the operations flows and a decision on whether the `/web` pages are merged into `frontend/`; final ZIP assembly and clean-machine build with Member 1 remain a team task.
6. Final cross-team confirmation that organisation scoping and notification routing behave correctly with the merged identity/outbox contracts, especially the assistance-request target-org check.

## Verification summary

| Check                                        | Status                                                        |
| -------------------------------------------- | ------------------------------------------------------------- |
| `dotnet build Cms.sln`                       | PASS — 0 warnings / 0 errors (`docs/evidence/dev4/build.log`) |
| `dotnet test Cms.sln --no-build`             | PASS — 561/561 (`docs/evidence/dev4/test.log`)                |
| Postman collection JSON + endpoint coverage  | PASS (static validation)                                      |
| LocalDB 20-iteration concurrency races       | PLANNED / NOT RUN                                             |
| Fresh seeded database script run             | PLANNED / NOT RUN                                             |
| Provider-outage and cache fake-HTTP tests    | PASS (no network; 7 `OperationsGeoAdapterTests`)              |
| Real-timer 1 req/s Nominatim throttle proof  | PLANNED / NOT RUN                                             |
| Live Postman/Newman + shortage demo evidence | PLANNED / NOT RUN                                             |
