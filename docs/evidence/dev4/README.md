# Member 4 verification evidence — executed 22 November 2026

These logs are execution evidence from Windows + LocalDB, not just script inventory. The disposable database is `CmsDev4Verification`; demo password is `Demo1234!`.

## Reproduce locally

Open PowerShell at the repository root:

```powershell
.\scripts\dev4\Initialize-Dev4LocalDb.ps1 -Database CmsDev4Verification -ResetExisting
```

Start the API in `Development` so the demo users are seeded, then wait for `GET http://localhost:5210/api/health` to return `Healthy`:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ConnectionStrings__DefaultConnection='Server=(localdb)\MSSQLLocalDB;Database=CmsDev4Verification;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;'
dotnet run --project backend/src/Cms.Api/Cms.Api.csproj --launch-profile http
```

Seed the scenario rows and re-run idempotently:

```powershell
.\scripts\dev4\Initialize-Dev4LocalDb.ps1 -Database CmsDev4Verification -RunScenarioSeeds
.\scripts\dev4\Test-Dev4SqlGuards.ps1 -Database CmsDev4Verification -Iterations 20
.\scripts\dev4\Invoke-Dev4LiveVerification.ps1 -ApiBaseUrl 'http://localhost:5210' -Database 'CmsDev4Verification'
.\scripts\dev4\Test-Dev4PostmanStatic.ps1
```

Build/test and frontend checks:

```powershell
dotnet build backend/Cms.sln --nologo -v q
dotnet test backend/Cms.sln --nologo -v q --no-build
cd frontend
npm run typecheck
npm run build
```

## Saved results

| File | Result |
| --- | --- |
| `build.log` | Build succeeded: 0 warnings, 0 errors |
| `test.log` | 586 passed, 0 failed, 0 skipped |
| `frontend-typecheck.log` | `tsc --noEmit` passed |
| `frontend-build.log` | Next.js production build passed |
| `fresh-db-init.log` | Fresh `CmsDev4Verification` schema initialized |
| `scenario-seeds.log` | Scenario seeds and idempotent re-run passed |
| `sql-guards.log` | Resource 20/20, shelter 20/20, key uniqueness 20/20, CHECK constraints PASS |
| `localdb-races.log` | Copy of the final SQL race output |
| `live-api.log` | Live API verification: 41 PASS, 0 FAIL |
| `postman-static.log` | 41 Postman requests; all required endpoints and negative key probe present |
| `geo-tests.log` | Geo adapter/service tests passed |
| `api-run.log` | API run output; expected outbox/MARS warnings only |
| `ui-verification.md` | Manual browser results for Resources, Shelters, Volunteers and Map |
| `runs/` | Retained raw SQL process output from the guard script |

## Notes

- `GET /resources/types` remains read-only for `ShelterAdmin`; creating resource types still requires `LogisticsManager` or `SystemAdmin`.
- Area markers use the dedicated `GET /operations/areas`, not the Dev 2 case-reader endpoint.
- A cleared manual coordinate is ignored by `MapPickerInner`; it never becomes `(0, 0)`.
- Failed token refresh now invalidates the session and redirects guarded pages to `/login`.
- The resources recovery panel only offers transitions allowed by `RequestStateMachine`; already-at-target retries are idempotent and illegal direct retries return `409 STATUS_TRANSITION_NOT_RETRYABLE`.
- `SHELTER_FULL`, duplicate shelter-key exceptions in the API log, no-recipient outbox warnings and MARS savepoint warnings are expected evidence from the negative probes and LocalDB configuration.
