# Member 4 verification evidence

This folder contains repeatable verification scripts. A script being present is **not** evidence that it has been executed. Save the command output and screenshots here only after running it on the Windows development machine.

## Required sequence

1. Open PowerShell at the repository root.
2. Create a disposable LocalDB database and run schema scripts:

   ```powershell
   .\scripts\dev4\Initialize-Dev4LocalDb.ps1 -Database CmsDev4Verification -ResetExisting
   ```

3. Start the API in `Development` against `CmsDev4Verification`. Set `ConnectionStrings__DefaultConnection` to `Server=(localdb)\MSSQLLocalDB;Database=CmsDev4Verification;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;`. Wait for `GET http://localhost:5210/api/health` to return `Healthy`. The DevelopmentDemoSeeder creates the demo users.
4. Run the case and operations demo seeds and re-run idempotent scripts:

   ```powershell
   .\scripts\dev4\Initialize-Dev4LocalDb.ps1 -Database CmsDev4Verification -RunScenarioSeeds
   ```

5. Run the real SQL Server guard probes:

   ```powershell
   .\scripts\dev4\Test-Dev4SqlGuards.ps1 -Database CmsDev4Verification
   ```

   This runs 20 iterations each of last-unit inventory updates, last-place shelter updates and concurrent idempotency-key inserts, then probes the inventory/shelter CHECK constraints using concurrent SQL Server sessions. SQL process output is retained under `docs/evidence/dev4/runs/`. It does **not** replace a concurrent HTTP/API test through `OperationsService`.
6. Obtain role-specific bearer tokens and approved decision IDs from the live application, then run the full operational flow. In local Development only, the seeder creates `logistics@example.org`, `shelter@example.org`, `volunteercoordinator@example.org` and `volunteer@example.org` (default demo password `Demo1234!`). Obtain each `accessToken` with `POST /api/v1/auth/login` and the corresponding email/password. Never reuse these demo credentials outside local Development. The resource and shelter decision IDs must already be approved by the AI decision workflow.

   Run the full operational flow:

   ```powershell
   .\scripts\dev4\Invoke-Dev4EndToEnd.ps1 `
     -LogisticsManagerToken '<token>' -ApprovedResourceDecisionId '<approved-resource-decision-guid>' `
     -ShelterAdminToken '<token>' -ApprovedShelterDecisionId '<approved-shelter-decision-guid>' `
     -VolunteerCoordinatorToken '<token>' -VolunteerToken '<token>'
   ```

7. Run the 41-request `docs/postman/Member4_Operations.postman_collection.json` against the same API with valid environment variables and role tokens. Export Newman/Postman results to this folder.
8. Run `dotnet build Cms.slnx` and `dotnet test Cms.slnx` from `backend`, then run `npm ci`, `npm run typecheck` and `npm run build` from `frontend`.

## Evidence still required after these scripts

- Live HTTP parallel requests through the allocation and shelter endpoints (not only direct SQL guard statements), repeated at least 20 times each.
- Same-key concurrent allocation API replay proving one allocation row and the expected existing DTO response.
- Phase-3 request-status failure followed by the explicit retry endpoint; confirm the committed allocation remains and audit event is written.
- Audit/outbox/notification delivery verification for allocation, dispatch, delivery, shelter and volunteer transitions.
- Nominatim real-clock spacing proof from `OperationsGeoAdapterTests.Geocode_RealClock_SeparatesProviderRequestsByAtLeastOneSecond` and OSRM-down fallback test output.
- Manual responsive UI checks at 360, 768 and 1280 px, including 403/409/validation and provider-offline cases.

Do not write PASS beside an item until the corresponding command or manual test has actually completed and its output has been saved here.
