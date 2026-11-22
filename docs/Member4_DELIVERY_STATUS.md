# Member 4 delivery status — updated 22 November 2026

This status distinguishes source implementation from execution evidence. The original `DEV4_TASKS_AND_PROMPTS.md` file was not included in the supplied ZIP; the attached `gha.pdf` contains the detailed Member 4 task block and acceptance criteria used for this review.

## Included backend implementation

- `OperationsController.cs` exposes the Member 4 resource, inventory, allocation, dispatch, shelter, volunteer, assistance-request, shortage, matcher and geo endpoints.
- Application contracts, domain entities, EF configurations, operations services, matchers, geo adapter, frozen cross-module adapters and module registration are present.
- Hand-written SQL scripts are present: `04_res.sql`, `05_shelter.sql`, `06_vol.sql`, optional `07_geo.sql`, and `91_seed_ops.sql`. `07_geo.sql` is intentionally unused because runtime mapping uses `IMemoryCache`.
- The operations Postman collection is present and contains 41 requests (40 owned P1 endpoints plus one negative idempotency-key probe).

## Frontend integration changes in this delivery

- Ported the Logistics, Shelter, Volunteer and Map pages from `/web` into the active SAFEWAY routes: `/resources`, `/shelters`, `/volunteers`, and `/operations/map`.
- Added `frontend/lib/operations-api.ts`, which delegates to SAFEWAY's refresh-aware authenticated API client and preserves ProblemDetails messages.
- Added Leaflet / React-Leaflet dependencies and copied `MapPicker` / `MapView` into `frontend/components/map/`; Leaflet CSS is loaded by the root layout.
- Added role-aware route/action visibility, resource inventory creation and correction, allocation/dispatch/delivery controls, shelter occupancy management, shelter-linked assistance requests, volunteer profile/availability/verification/task actions, volunteer matching, and live map data.
- Hardened assistance-request source organisation checks and blocked non-privileged responses when no target organisation is assigned; added regression tests for both cases.
- Existing SAFEWAY navigation and shell are preserved. The standalone `/web` source remains unchanged as the handoff/reference implementation.

These are source-level integration changes. The frontend dependency install, typecheck and production build have **not** been executed in this environment because package installation could not complete; the current container has no installed frontend `node_modules`. Syntax transpilation passed for the changed TypeScript/TSX files, but that is not a substitute for `npm run typecheck` or `npm run build`.

## Test evidence and limitations

- The previous delivery-status text reported `dotnet build Cms.sln` with 0 warnings/errors and `dotnet test Cms.sln --no-build` with 561/561 passing. The referenced `docs/evidence/dev4/build.log` and `test.log` were not present in the supplied ZIP, so those results are **reported by the previous status, not independently verifiable from this archive**.
- This environment does not contain the .NET SDK, SQL Server LocalDB, or `sqlcmd`; backend build/tests and real SQL Server tests could not be executed here.
- Added `OperationsGeoAdapterTests.Geocode_RealClock_SeparatesProviderRequestsByAtLeastOneSecond` to measure the real Nominatim request spacing with a real clock. It has not been executed yet.
- Added `scripts/dev4/Initialize-Dev4LocalDb.ps1`, `Test-Dev4SqlGuards.ps1`, and `Invoke-Dev4EndToEnd.ps1`, with exact instructions in `docs/evidence/dev4/README.md`. These scripts are **not execution evidence** until run on the Windows development machine and their outputs saved.

## Remaining verification before claiming 100%

1. Run the fresh-database SQL scripts in the documented order, including the API-start step before `90_seed_scenario.sql` and `91_seed_ops.sql`; re-run the idempotent scripts and inspect all CHECK constraints.
2. Run direct SQL Server concurrency probes and, separately, at least 20 repeated concurrent HTTP/API allocation and shelter-allocation races through `OperationsService`.
3. Run same-key concurrent allocation API replay; verify one allocation row and correct replay response.
4. Force a Phase-3 request-status failure and verify the committed allocation remains, the audit event is written, and the retry endpoint completes the status transition.
5. Run live Postman/Newman tests with role tokens and a real approved decision; verify audit, outbox, notifications and request status across allocation, dispatch and delivery.
6. Run the end-to-end shelter and volunteer flows with real demo identities and confirm audit/outbox notifications.
7. Run the real-clock Nominatim throttle test and OSRM outage/Haversine fallback tests.
8. Run `dotnet build Cms.slnx`, `dotnet test Cms.slnx`, `npm ci`, `npm run typecheck`, and `npm run build` on the Windows machine; fix all errors.
9. Manually check the integrated SAFEWAY pages at 360, 768 and 1280 px, including 403/409/validation and provider-offline cases.
10. Confirm cross-module organisation scoping and notification routing with the team, then commit on `feature/dev4-logistics` and open a PR to `develop` (do not commit directly to `develop`).

## Verification summary

| Check                                         | Current status                                                     |
| --------------------------------------------- | ------------------------------------------------------------------ |
| Backend implementation source                 | Present; prior status reported 561 tests passing, logs missing     |
| Postman collection JSON / endpoint inventory  | Static collection present; validate/run against live API           |
| Main SAFEWAY frontend integration             | Source changes made; build/typecheck not run                       |
| Fresh SQL Server database + script re-run     | Script added; not run                                              |
| Real SQL guard/concurrency probes             | Script added; not run                                              |
| HTTP/API concurrency and idempotency races    | Not run                                                            |
| Phase-3 recovery + audit/outbox/notifications | Not verified end-to-end                                            |
| Nominatim real-clock throttle                 | Test added; not run                                                |
| OSRM outage fallback                          | Fake-HTTP test exists; prior status reported passing, logs missing |
| Full operational UAT                          | Not run                                                            |
| Feature-branch PR / merge                     | Not completed in this archive                                      |

**Conclusion:** The frontend integration and repeatable verification tooling have now been added to the project. Do not claim 100% functional completion until the live SQL Server, API, build, and UAT evidence above has been captured.
