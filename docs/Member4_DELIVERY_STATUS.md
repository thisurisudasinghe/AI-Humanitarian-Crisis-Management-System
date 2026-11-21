# Member 4 delivery status — 21 November 2026

This file records what is included in this archive and what still requires team integration or execution evidence. It intentionally does not claim tests passed when they have not been run.

## Member 4 ownership

- M5 Resources: resource types, inventories, matching, allocation, dispatch/delivery, shortage proposal and assistance requests.
- M6 Shelters: shelter records, capacity/occupancy and recommendations.
- M7 Volunteers: profiles, verification, availability, tasks and activity records.
- M10 Mapping: geocoding, routing, cache/fallback and map UI.
- Member 4 database scripts, seed data, Postman collection, report sections, component diagram and tests.

## Included in this archive

- Operations domain entities and SQL scripts: `backend/db/04_res.sql`, `05_shelter.sql`, `06_vol.sql`, `07_geo.sql`, `91_seed_ops.sql`.
- Operations API controller and initial operations frontend pages/components under `web/`.
- Operations EF DbSets and EF Core configurations for the `res`, `shelter` and `vol` schemas.
- `GeoService` implementation using named `nominatim` and `osrm` clients, success/failure cache durations, rate limiting for Nominatim and Haversine fallback.
- Deterministic, read-only resource, shelter and volunteer matcher implementations.
- Logistics module DI registration for the concrete geo and matching implementations.
- Existing Member 4 Postman collection, diagram and report drafts.

## Corrections made in this delivery

- Operations controller now uses the shared `CmsDbContext`, `ICurrentUser` and `IAuditLogger` contracts rather than nonexistent `ApplicationDbContext`, `ICurrentUserService` and `IAuditService` types.
- The shared context exposes the Member 4 entities and the request set used by the operations endpoints.
- The operations module replaces the fail-closed geo/matcher stubs with concrete services.
- The seed file contains five volunteer rows (not three).

## Still not certified as complete

1. A .NET SDK/runtime was not available in the file-editing environment, so `dotnet build` and `dotnet test` could not be run. Do not report a green build until it is run on a machine with the required .NET SDK 10.0.400 and SQL Server LocalDB.
2. The checked-in ZIP does not contain `02_*` and `90_seed_scenario.sql` scripts referenced by the team's run order. Member 1/2 must supply/merge those scripts and confirm the fixed GUIDs used by `91_seed_ops.sql` match the three urgent requests in the scenario seed.
3. `OperationsController` still needs a final contract-level review against Member 2's request reader/status service and Member 3's `IDecisionGate` contract. In particular, allocation/dispatch/delivery endpoints must re-verify the decision at each operation and the status transition must be retry-safe after the stock transaction commits.
4. Organisation scoping, target-organisation checks on assistance responses, and outbox notifications must be verified against the team's final identity and notification contracts. Do not treat role attributes alone as organisation-level authorization.
5. Resource matcher scoring currently uses the request category text as a resource-name filter because the shared request model in this archive has no requested resource quantity/type summary. Replace this heuristic with Member 2's agreed request-summary contract before claiming full matching accuracy.
6. Volunteer matching uses current availability and profile data; required-skill intersection with task/request requirements needs the agreed request/task contract.
7. Required LocalDB concurrency tests (20 repeated races), idempotency tests, dispatch/delivery bucket tests, provider-outage tests, Postman execution evidence and the shortage demonstration still need to be run and attached to the report.
8. The `web/` pages are an earlier operations UI area. The integrated app's active Next.js application is under `frontend/`; confirm with the team whether the `web/` pages should be merged into `frontend/` or retained as the Member 4 handoff area. The active frontend pages under `frontend/app/(app)/resources`, `shelters`, `volunteers` and `operations/map` need end-to-end verification against the API.
9. Final component diagram and report claims must be reconciled with the merged code after the above integration work.

## Verification status

- Archive integrity: created from the supplied repository with the Member 4 changes listed above.
- Build: NOT RUN (required SDK unavailable in this environment).
- Unit/integration/concurrency tests: NOT RUN in this environment.
- SQL Server fresh-database script run: NOT RUN.
- Postman live endpoint verification: NOT RUN.

This delivery is an updated Member 4 implementation package, not evidence that every acceptance criterion has passed. Complete the outstanding checks with the group before submission.
