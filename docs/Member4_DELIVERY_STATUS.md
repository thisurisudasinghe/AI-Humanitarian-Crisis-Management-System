# Member 4 delivery status — 22 November 2026

Status: **Dev 4 modules 100% functionally integrated and verified locally** on `fix/dev4-duplicate-operations-controller` (base `86b9bea`, `develop`).

Scope is unchanged: M5 Resources, M6 Shelters, M7 Volunteers, M10 Mapping and D4-01–D4-13. No other member's source was overwritten.

## What was fixed

### Backend

- Removed the post-merge `CS0101`/`CS0111` duplicate controller by restoring the original `OrganisationsController.cs` from `e753172` and keeping the real Member 4 `OperationsController.cs`. The three newer `SystemAdmin` allowances were ported onto the restored organisation API.
- `OperationsService.AllocateAsync` rolls back the failed SQL transaction before looking up the winning idempotency row.
- Added `UQ_shelter_allocations_decision` and replay handling so one approved shelter decision can never create two occupancy rows, including under concurrent requests.
- Status-transition recovery now uses `RequestStateMachine.CanTransition`: already-at-target retries are idempotent, invalid seeded rows are hidden from the pending list, and an illegal direct retry returns `409 STATUS_TRANSITION_NOT_RETRYABLE` instead of `500`.
- Added the dedicated `GET /api/v1/operations/areas` read so all roles that can open the map see area markers without widening the Dev 2 case-reader policy.
- Widened only the read-only `GET /resources/types` endpoint to `ShelterAdmin`; creation remains restricted to Logistics/SystemAdmin.
- Corrected Nominatim throttling to hold the gate for the whole provider call.
- Kept EF `EnableRetryOnFailure` removed because `OperationsService` uses explicit SQL transactions.

### Frontend

- Map components load with `next/dynamic({ ssr: false })` and use `/operations/areas`.
- `MapPickerInner` ignores empty coordinate input, so clearing a field never relocates the pin to `(0, 0)`.
- Failed refresh dispatches `cms:auth-invalidated`; `AuthProvider` clears `user` and `AuthGuard` redirects to `/login`.
- ProblemDetails error text no longer repeats a code already present in `title`.
- Resources allocation dialog explains that the Idempotency-Key is reused on retry.

## Verification results

| Check | Result | Evidence |
| --- | --- | --- |
| `dotnet build backend/Cms.sln --nologo -v q` | **0 warnings, 0 errors** | `docs/evidence/dev4/build.log` |
| `dotnet test backend/Cms.sln --nologo -v q --no-build` | **586 passed, 0 failed** | `docs/evidence/dev4/test.log` |
| `npm run typecheck` | passed | `docs/evidence/dev4/frontend-typecheck.log` |
| `npm run build` | passed; routes include `/resources`, `/shelters`, `/volunteers`, `/operations/map` | `docs/evidence/dev4/frontend-build.log` |
| Fresh LocalDB reset, schema, scenario seeds, idempotent re-seed | passed | `fresh-db-init.log`, `scenario-seeds.log` |
| Real SQL Server guards, 20 iterations each | resource 20/20, shelter 20/20, idempotency key 20/20, CHECK constraints PASS | `sql-guards.log`, `localdb-races.log` |
| Live API / Dev 2 data / Dev 3 decision chain / concurrency / role reads / geo fallback | **41 PASS, 0 FAIL** | `live-api.log`, `Invoke-Dev4LiveVerification.ps1` |
| Postman collection static check | 41 requests, all required endpoints and negative idempotency probe present | `postman-static.log`, `Test-Dev4PostmanStatic.ps1` |
| Manual browser UI pass | Resources, Shelters, Volunteers and Map verified with role tokens; no console errors | `ui-verification.md` |

The live API run exercised the R6/R7 seeded requests, approved Dev 3 recommendation decisions, resource allocation/dispatch/delivery, shelter allocation including overbooking and duplicate replay, volunteer profile/availability/verification/assignment, same-key concurrent allocation, geo distance/fallback, invalid input, missing key, unauthorised access, and the role-specific map/type reads.

Outbox messages without recipients and MARS savepoint warnings in `api-run.log` are expected LocalDB evidence noise; the expected `SHELTER_FULL` and duplicate-key exceptions are handled and asserted by the verification scripts.

## Conclusion

The duplicate-controller merge break is resolved and the Member 4 feature branch is ready for PR/merge to `develop`. All build, test, database, API and UI checks above passed on Windows with LocalDB and the local demo identities.
