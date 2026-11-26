# SAFEWAY Developer Handover

Date: 2026-11-22  
Branch: `final/professionalization-compliance`  
Base: `origin/develop` @ `2e00edb2e17eac26eda43f0ba1f3cd7c9b0e3357` (includes merged PRs #28, #29, #30)

## Status

| Item | Status | Notes |
|---|---|---|
| Final commit | `d8ffbfda33383a1a5837cd95312cf13a7b1c0d53` (code/docs pass); see git log for final handover-doc update commit | Changes committed on `final/professionalization-compliance`. |
| PR to `develop` | PENDING (not created) | Branch pushed to `origin/final/professionalization-compliance`. `gh` CLI is not installed and no GitHub token was confirmed, so PR creation could not be automated. Create PR: https://github.com/thisurisudasinghe/AI-Humanitarian-Crisis-Management-System/pull/new/final/professionalization-compliance or compare https://github.com/thisurisudasinghe/AI-Humanitarian-Crisis-Management-System/compare/develop...final/professionalization-compliance |
| Merge to `develop` | PENDING | Do not merge until PR checks/protection pass. Normal merge only; no force-push, reset, discard, branch deletion, direct `develop`/`main` push, or protection bypass. |
| Local `develop` sync | PENDING | After merge, sync local `develop` with `origin/develop` and record final merged commit hash here. |

## What This Pass Delivered

- **Coursework compliance mapping**: `docs/COURSEWORK_COMPLIANCE.md` maps SE205.3 requirements to implementation, API/UI, tests/evidence, demo steps, and PASS/PARTIAL/MISSING status with no un-evidenced PASS claims.
- **Final verification record**: `docs/FINAL_VERIFICATION.md` records exact test/build results and limitations.
- **KPI/audit correctness**: `KpiService` computes live case/resource/shelter/AI/area metrics; `Decision.Recorded` audit now includes recommendation confidence.
- **Citizen portal gaps**: report flow exposes all required categories, uses map picker, enforces 1 MB evidence cap consistently; account page has household profile create/update; My Reports supports feedback for resolved requests; Find Assistance shows live public alerts/service centres.
- **Demo seed fix**: seeded volunteer profiles in `backend/db/91_seed_ops.sql` now reference real `DevelopmentDemoSeeder` demo user IDs so notification/assignment demos can resolve recipients.
- **Cleanup/polish**: raw role strings replaced by `RoleNames` in affected areas; modal focus trap/restore and error toast alert role; stale docs updated.

## UI Confirmation

The public SAFEWAY redesign merged by PR #28 remains present on `develop` and is preserved in this branch. This pass did not replace or regress the public UI; it added public alert/service-centre data wiring to Find Assistance and kept existing public styling/components.

## Verification

| Check | Command | Result |
|---|---|---|
| Targeted backend tests | `dotnet test backend/Cms.sln --nologo -v q --filter "FullyQualifiedName~KpiServiceTests|FullyQualifiedName~DecisionServiceTests.RecordAsync_AuditIncludesRecommendationConfidence|FullyQualifiedName~KpiEndpointTests|FullyQualifiedName~AdminEndpointTests.Kpi_endpoint_enforces_roles_and_date_validation"` | PASS (8 passed, 0 failed) |
| Full backend suite | `dotnet test backend/Cms.sln --nologo -v q` | PASS (**589 passed, 0 failed, 0 skipped**) |
| Frontend typecheck | `npm --prefix frontend run typecheck` | PASS |
| Frontend production build | `npm --prefix frontend run build` | PASS |

## Known Limitations / Partial Items

- **Nominatim geocoding 403**: external provider may reject compliant User-Agent; API returns `404 GEOCODE_NOT_FOUND`. OSRM distance/routing, stored coordinates, and Haversine fallback remain usable. No provider restriction bypassed.
- **No real email/SMS/push**: demo uses in-app notifications and log email sender (`Smtp__Host=''`). SMS/push not implemented.
- **Demand forecasting**: basic demand forecasting endpoint/UI not implemented; advanced forecasting dashboard is optional per coursework.
- **Formal UAT/usability evidence**: automated/API/live evidence exists, but participant UAT/usability sessions are not recorded as completed studies in-repo (`docs/UAT_CORE_LOOP.md`, `docs/USABILITY_TEST.md`).
- **Submission packaging**: final report PDF/ZIP/video-link packaging is external to repo automation.
- **Partner assistance UI**: API exists; prominent end-to-end UI flow is limited.

## Exact Demo Startup Steps (disposable DB)

> Do **not** use the original `CmsDb`. Use a disposable LocalDB database.

### 1. Create disposable DB and apply canonical schema

```powershell
$db = "CmsDb_IntegrationTest"
sqlcmd -S "(localdb)\mssqllocaldb" -E -b -Q "IF DB_ID('$db') IS NOT NULL BEGIN ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$db]; END; CREATE DATABASE [$db];"

$scripts = @(
  "00_common.sql","00_identity_email_verification.sql","01_cases.sql",
  "02_notif.sql","03_ai.sql","04_res.sql","05_shelter.sql","06_vol.sql",
  "08_don.sql","09_donor_portal.sql","10_operations_centre.sql","11_access_reporting.sql","98_foreign_keys.sql","99_seed_users.sql",
  "90_seed_scenario.sql","91_seed_ops.sql"
)
foreach ($s in $scripts) {
  sqlcmd -S "(localdb)\mssqllocaldb" -d $db -I -b -i "backend/db/$s"
}
```

Notes: `07_geo.sql` is stale and intentionally skipped. `99_seed_users.sql` creates roles/org/settings only; demo users are created by `DevelopmentDemoSeeder` on API startup.

### 2. Run backend API

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ConnectionStrings__DefaultConnection="Server=(localdb)\mssqllocaldb;Database=CmsDb_IntegrationTest;Trusted_Connection=True;TrustServerCertificate=True"
$env:Outbox__ProcessorEnabled='true'
$env:Smtp__Host=''
dotnet run --project backend/src/Cms.Api/Cms.Api.csproj --no-launch-profile --urls http://localhost:5210
```

### 3. Run frontend

```powershell
npm --prefix frontend install
$env:NEXT_PUBLIC_API_URL='http://localhost:5210'
npm --prefix frontend run dev -- --port 3000
```

Frontend: `http://localhost:3000`  
API/Swagger: `http://localhost:5210/swagger` (Development)  
Health: `GET http://localhost:5210/api/health`

### 4. Demo accounts

All seeded demo accounts use password `Demo1234!` (created by `DevelopmentDemoSeeder` on API startup in Development).

| Role | Email |
|---|---|
| SystemAdmin | `admin@example.org` |
| EmergencyCoordinator | `coordinator@example.org` |
| CaseManager | `casemanager@example.org` |
| LogisticsManager | `logistics@example.org` |
| ShelterAdmin | `shelter@example.org` |
| VolunteerCoordinator | `volcoord@example.org` |
| Volunteer | `volunteer@example.org` |
| DonorOfficer | `donorofficer@example.org` |
| PartnerOfficer | `partner@example.org` |
| Citizen | `citizen@example.org` |

## PR / Delivery Commands

Because `gh` is unavailable and no GitHub token is configured, PR creation/merge could not be automated here. After reviewing, use authenticated git/gh:

```powershell
# review
git status
git diff --stat

# commit (example)
git add -A
git commit -m "chore: final coursework compliance and verification"

# push
git push -u origin final/professionalization-compliance

# create PR (if gh installed/authenticated)
gh pr create --base develop --head final/professionalization-compliance --title "Final coursework compliance and verification" --body "Includes compliance matrix, verification evidence, citizen/KPI/audit fixes, and handover docs."
```

If `gh` is unavailable, create PR from:  
`https://github.com/thisurisudasinghe/AI-Humanitarian-Crisis-Management-System/compare/develop...final/professionalization-compliance`

## Post-Merge Checklist

1. Confirm PR checks/protection pass.
2. Merge PR into `develop` with normal merge (no squash/rebase required by this note; follow repo policy).
3. Sync local develop: `git fetch origin && git checkout develop && git pull --ff-only origin develop`.
4. Record final merged commit hash and PR URL in this file, `docs/FINAL_VERIFICATION.md`, and `docs/COURSEWORK_COMPLIANCE.md` if needed.
5. Do not delete remote branches automatically unless requested/allowed.

## Files to Review First

- `docs/COURSEWORK_COMPLIANCE.md`
- `docs/FINAL_VERIFICATION.md`
- `docs/DEVELOPER_HANDOVER.md`
- `docs/evidence/final-integration-20261122.md`
- `backend/src/Cms.Infrastructure/Services/KpiService.cs`
- `backend/src/Cms.Infrastructure/Services/Governance/DecisionService.cs`
- `frontend/components/safeway/ReportFlow.tsx`
- `frontend/app/(app)/account/page.tsx`
- `frontend/app/(app)/my-reports/page.tsx`
- `frontend/components/safeway/PublicCaseUpdates.tsx`
