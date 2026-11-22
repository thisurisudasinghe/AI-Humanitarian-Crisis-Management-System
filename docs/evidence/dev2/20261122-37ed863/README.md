# DEV2 runtime verification — 2026-11-22 (`37ed863`)

**Scope:** API/SQL/Postman runtime verification for the Cases Module on `feature/dev2-crisis` at `37ed8633b0a297a863d3d77130989b3ce1964180`.

**Important:** This is **automated runtime evidence**, not user acceptance testing (UAT) and not usability testing. No real participants, bookings, consent records, keyboard-only runs, 360 px checks, satisfaction scores or screenshots were collected. `docs/UAT_CORE_LOOP.md` and `docs/USABILITY_TEST.md` remain unexecuted.

## 1. Environment

| Item | Value |
|---|---|
| Branch | `feature/dev2-crisis` |
| HEAD | `37ed8633b0a297a863d3d77130989b3ce1964180` |
| SQL Server | SQL Server LocalDB `(localdb)\mssqllocaldb`, Windows auth |
| Temporary databases | `dev2_runtime_verify_37ed863` (first run), `dev2_runtime_verify_37ed863_run2` (second run), `dev2_runtime_verify_37ed863_run3` (final clean run) |
| API runtime | `dotnet run --project backend/src/Cms.Api --no-build --no-restore --no-launch-profile` |
| Base URLs | `http://localhost:5210` (first), `http://127.0.0.1:5211` (second), `http://127.0.0.1:5212` (final) |
| Newman | Node 22 / Newman via `C:\Program Files\nodejs\npx.cmd` |
| Demo accounts | `citizen@example.org`, `coordinator@example.org`, `casemanager@example.org`, `admin@example.org`; password `Demo1234!` |

`--no-launch-profile` was required because `launchSettings.json` overrode `ASPNETCORE_URLS` and forced port `5210`. Environment variables used for the final run were `ASPNETCORE_ENVIRONMENT=Development`, `ASPNETCORE_URLS=http://127.0.0.1:5212` and `ConnectionStrings__DefaultConnection` pointing only to the temporary `run3` database.

No shared database, repository file owned by another member, or existing persisted database was modified to resolve a dependency. All test mutations were confined to the three temporary LocalDB databases.

## 2. Commands and results

### 2.1 Schema

For each temporary database, the canonical scripts were run in this order with `sqlcmd -S "(localdb)\mssqllocaldb" -E -I -b`:

`00_common.sql`, `01_cases.sql`, `02_notif.sql`, `03_ai.sql`, `04_res.sql`, `05_shelter.sql`, `06_vol.sql`, `07_geo.sql`, `08_don.sql`, `98_foreign_keys.sql`, `99_seed_users.sql`.

Every script exited `0` in each temporary database (observed from the `sqlcmd -b` exit codes). Per-script raw logs were not retained; the final object/count checks are in `schema-verification-run3.log`:

- `cases` tables: **11** (`alerts`, `areas`, `attachments`, `crises`, `feedback`, `households`, `partner_shares`, `request_links`, `request_status_history`, `requests`, `service_centres`)
- cases foreign keys: **12**
- required `cases.requests` columns present: **11**
- seed request count at verification time: **11** (10 scenario fixtures + 1 request created by Newman)
- R6–R8 urgent Food fixtures: **3**

### 2.2 Readiness and seed

A login request was used as the readiness probe because Swagger generation failed in shared Member 1 code (`FilesController.Upload` / `[FromForm] IFormFile`; see `api_startup.log`). On the final database:

- `POST http://127.0.0.1:5212/api/v1/auth/login` as `citizen@example.org` → **200 OK**
- `backend/db/90_seed_scenario.sql` → exit **0** (`seed_run3_90_seed_scenario.sql.log`)
- `backend/db/90_seed_scenario.verify.sql` → **19/19 PASS** (`verify_run3_90_seed_scenario.sql.log`), including `shortage group R6-R8 Food | 3 | 3 | PASS`
- `backend/db/91_seed_ops.sql` → **FAILED**, `Msg 208, Level 16, State 1 ... Invalid object name 'res.resource_types'`. (`seed_run2_91_seed_ops.sql.log`; the first-run failure is in `seed_91_seed_ops.sql.log`). This is a Member 4 script/schema casing dependency and was not changed.

### 2.3 Postman / Newman

Final clean run command:

```text
npx.cmd newman run postman/dev2-cases.json --env-var baseUrl=http://127.0.0.1:5212 --delay-request 100 --reporters cli,json --reporter-json-export docs/evidence/dev2/20261122-37ed863/newman-results-run3.json
```

Result (`newman-cli-run3.log`, `newman-results-run3.json`):

- iterations **1/1**, requests **39/39**, test scripts **39/39**, prerequest scripts **24/24**, assertions **34/34**, failed **0**
- total run duration **13.5 s**; average response time **144 ms**
- attachment upload **201 Created** and download **200 OK** using the bundled `postman/fixtures/dev2-evidence-sample.png`

Observed final statuses:

| Request group | Observed status(es) |
|---|---|
| Role logins (citizen, coordinator, case manager) | 200, 200, 200 |
| Invalid password / unauthenticated staff queue / citizen staff queue | 401 / 401 / 403 |
| Invalid create payload / area-crisis mismatch / citizen crisis write | 422 / 422 / 403 |
| Invalid transition / critical close without decision | 409 / 403 |
| Self-transition no-op / transitions / assignment / merge / publish alert | 204 |
| Critical request advance to Fulfilled | 204 |
| Submit request / create crisis / create area / create alert | 201 / 201 / 200 / 200 |
| My requests, detail, timeline, staff queue, overdue, unresolved, crises, areas, crisis summary, public alerts, public service centres, household profile get/save, feedback | 200 |
| Update my request / update crisis / upload attachment | 204 / 204 / 201 |
| Download attachment | 200 |

The first run (`newman-cli.log`, `newman-results.json`) had 39 requests but 2 failed assertions and 5 failed test scripts: invalid Chai `.or` usage and JSON parsing on empty 401/403 bodies, plus an empty attachment `src`. The second run (`newman-cli-run2.log`) fixed the scripts but still failed the attachment upload/download because the fixture path was resolved relative to the repository root. The DEV2-owned collection and bundled fixture were corrected; the final run above is the clean result. The earlier logs are retained for transparency and must not be presented as a pass.

Bearer JWTs in the three Newman JSON files were redacted after capture. The first pass replaced only textual `Bearer eyJ...` values (32 per file); a later repair decoded every `run.executions[].response.stream.data` byte array, found three raw JSON `accessToken` JWTs per file that the textual scan could not see, and replaced each complete JWT with `<redacted-jwt>` before re-encoding the byte arrays. The final scan found **0 textual and 0 decoded JWT-like values** in each file, with three decoded redaction markers per file. All three files remain valid JSON with 39 executions/requests and 34 assertions (`redaction.log`). Request/response metadata, headers and assertions were not altered. No token values are printed or stored in the log.

### 2.4 Positive Critical close

An extra check was performed only in `dev2_runtime_verify_37ed863_run3`:

1. Inserted one `ai.recommendations` row of type `CloseCase` and one approved `ai.decision_records` row for Critical request `1d000000-0000-0000-0000-000000000002` using SQL (`critical-close-positive.log`). This was a **temporary local test fixture**, not a change to M3 code or seed scripts.
2. Logged in as coordinator → **200 OK**.
3. `POST /api/v1/requests/1d000000-0000-0000-0000-000000000002/transition` with `to: Closed`, `reason`, and the locally seeded `decisionId` → **200 OK**, empty response body, matching the frozen decision-gated close contract.
4. SQL read-back confirmed the request status is **Closed**.

This proves the composed runtime gate accepts a valid approved decision when one is supplied. It does **not** prove an in-app M3 flow discovers/creates a `CloseCase` decision ID; that remains a Member 3 dependency.

### 2.5 Timeline, notifications, audit and outbox

`history-audit-notifications.log` records:

- coordinator `GET /api/v1/requests/{id}/timeline` → **200 OK**; the response includes the runtime close transition with reason `Runtime verification with approved CloseCase decision.`
- citizen `GET /api/v1/notifications?unreadOnly=false&limit=50` → **200 OK**; response includes an in-app `request.status.Closed` notification for the closed request
- `admin@example.org` login → **200 OK** (`systemadmin@example.org` returned **401**; the seeded admin email is `admin@example.org`)
- `GET /api/v1/audit-events?page=1&pageSize=50` → **200 OK** with `User.LoggedIn` rows
- `GET /api/v1/admin/outbox/dead?page=1&pageSize=20` → **200 OK** with `totalCount: 0`

The outbox replay/idempotency scenario (NEG-04) was **NOT EXECUTED**; no replay tooling was run. Dead-letter emptiness is not proof of idempotent replay.

### 2.6 Member 3/Member 4 dependency calls

- `uat04-recommendation-decision.log`: `GET /api/v1/recommendations?targetId={new request id}` with coordinator token → **200 OK** with two pending recommendations (`Priority`, `Classification`); `POST /api/v1/recommendations/{id}/decisions` with `outcome: Approved` → **201 Created**. This verifies those M3 read/write endpoints with runtime data, but the recommendation was **not** a `CloseCase` recommendation and does not replace the missing in-app close-decision discovery.
- `uat10-match-dependency.log`: `GET /api/v1/recommendations/match/resources/1d000000-0000-0000-0000-000000000006` → **500 Internal Error**, `code: INTERNAL_ERROR`, trace id `0HNQ88TJMVG6N:00000002`. `api_runtime_run3.log` ties that trace to `Microsoft.Data.SqlClient.SqlException: Invalid object name 'res.resource_types'`. This is the same Member 4 schema/name dependency as `91_seed_ops.sql`; no M4 file was changed.

### 2.7 Final source checks

After the runtime work:

- `dotnet build backend/Cms.sln --no-restore` → **0 warnings, 0 errors** (`final-build.log`). The first attempt hit MSB3021/MSB3027 file-lock retries because the three temporary API processes were still running; those processes were stopped and the build was rerun successfully.
- `dotnet test backend/Cms.sln --no-build` → **508 passed, 0 failed, 0 skipped** (`final-test.log`).
- `npm.cmd --prefix frontend run typecheck` → **exit 0** (`final-frontend-typecheck.log`).
- `npm.cmd --prefix web run lint` was attempted but is **not a valid check result** (`final-web-lint.log`): the `web` script is `tsc --noEmit`, but `web/tsconfig.json` is absent, so TypeScript printed its help text and exited 1. No source error was demonstrated. The earlier `web npm.cmd run build` pass is recorded in the DEV2 docs; no new `web/` build claim is made here.

## 3. Evidence index

| File | Purpose |
|---|---|
| `00_database_name.txt` | First temporary database name |
| `api_runtime.log`, `api_runtime_run2.log`, `api_runtime_run2_fixed.log`, `api_runtime_run3.log` | API runtime output; final run includes the M4 match trace |
| `api_startup.log` | Shared Swagger generation failure |
| `schema-verification-run3.log` | Final database object/count checks |
| `seed_91_seed_ops.sql.log`, `seed_run2_91_seed_ops.sql.log` | M4 seed dependency failure |
| `seed_run3_90_seed_scenario.sql.log` | Final seed replay used to retain the raw exit-0 output (the seed is idempotent) |
| `verify_90_seed_scenario.log`, `verify_run2_90_seed_scenario.sql.log`, `verify_run3_90_seed_scenario.sql.log`, `verify_run3_rerun_90_seed_scenario.sql.log` | 19 seed checks for each run and final replay |
| `newman-cli.log`, `newman-cli-run2.log`, `newman-cli-run3.log` | Newman CLI output for all three runs |
| `newman-results.json`, `newman-results-run2.json`, `newman-results-run3.json` | Redacted Newman JSON results |
| `critical-close-positive.log` | Positive Critical close check |
| `history-audit-notifications.log` | Timeline, notification, audit and dead-letter checks |
| `uat04-recommendation-decision.log` | M3 recommendation read and decision write |
| `uat10-match-dependency.log` | M4 match endpoint failure |
| `redaction.log` | Bearer-token redaction record |
| `final-build.log`, `final-test.log`, `final-frontend-typecheck.log` | Final source checks |
| `final-web-lint.log` | Invalid web lint attempt (missing `web/tsconfig.json`), retained for transparency |

## 4. Not executed / blocked

- **UAT:** no real users or signed result cells; test script remains in `docs/UAT_CORE_LOOP.md`.
- **Usability:** no participants, screenshots, timings, satisfaction scores or findings; plan remains in `docs/USABILITY_TEST.md`.
- **M4 resources:** `91_seed_ops.sql` and the match endpoint fail because of `res.resource_types`; allocation was not attempted.
- **M3 in-app close decision discovery:** positive close used a locally inserted decision; the UI/M3 discovery gap remains.
- **Swagger:** shared `FilesController.Upload` generation failure blocked Swagger-based readiness; login was used instead.
- **Outbox replay:** NEG-04 not executed.
- **Sensitive-data runtime SQL inspection:** not executed; no `sensitive-data-runtime.log` was created. Ciphertext/masking is covered by unit assertions only (prefix/length and masked audit details in `HouseholdServiceTests.cs`).
- **Post-capture DEV2 source additions:** partner share API/UI, the public safety-guidance link, real evidence upload/coordinate validation, workflow recovery and the ciphertext-format assertion were added after this 39/39 Newman run. They are covered by new source tests and UAT steps in this working tree but were **not** part of the recorded runtime capture.
