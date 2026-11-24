# AI-Powered Humanitarian Crisis Management System

## 1. Project Overview

A modular humanitarian crisis management platform designed to support crisis response, aid coordination, resource allocation, notifications, and reporting.

The system aims to improve humanitarian operations through a centralized platform that supports efficient coordination, informed decision-making, and transparent resource management.

## 2. Technology Stack

- **Backend:** ASP.NET Core Web API (.NET 10)
- **Database:** Microsoft SQL Server
- **ORM:** Entity Framework Core
- **Frontend:** React / Next.js
- **Architecture:** Modular Monolith
- **Version Control:** Git and GitHub

## 3. Project Structure

The repository is organized into the following main directories:

```text
AI-Humanitarian-Crisis-Management-System/
│
├── backend/
│   ├── src/
│   │   ├── Cms.Api/
│   │   ├── Cms.Application/
│   │   ├── Cms.Domain/
│   │   └── Cms.Infrastructure/
│   │
│   ├── tests/
│   │   └── Cms.Tests/
│   │
│   ├── db/
│   │   ├── 00_common.sql
│   │   ├── 01_cases.sql ... 10_operations_centre.sql (module owners; includes 02_notif.sql)
│   │   ├── 98_foreign_keys.sql
│   │   ├── 99_seed_users.sql
│   │   └── 90_seed_scenario.sql, 91_seed_ops.sql
│   │
│   ├── Cms.sln
│   ├── Cms.slnx
│   └── global.json
│
├── frontend/
│   ├── app/
│   ├── components/
│   ├── lib/
│   └── README.md
│
├── docs/
├── .gitignore
├── ALLOCATION.md
└── README.md
```

## 4. Prerequisites

Make sure the following tools are installed:

- .NET SDK 10.0.400
- Microsoft SQL Server
- Git
- Node.js (for frontend development)

## 5. Project Setup

### Clone the Repository

```powershell
git clone https://github.com/thisurisudasinghe/AI-Humanitarian-Crisis-Management-System.git
cd AI-Humanitarian-Crisis-Management-System
```

### Restore and Build the Backend

```powershell
cd backend
dotnet restore Cms.slnx
dotnet build Cms.slnx
```

### Run Backend Tests

```powershell
dotnet test Cms.slnx
```

## 6. Database Setup

1. Create a disposable SQL Server database (for example `CmsDb_IntegrationTest`). Do not use the legacy shared `CmsDb`.
2. Configure the connection string (`ConnectionStrings__DefaultConnection`).
3. Run the ordered SQL scripts from `backend/db`.

The schema is defined by plain, idempotent SQL scripts. EF migrations are not
used and `dotnet ef database update` is not part of the workflow.

### SQL Script Execution Order

Run the scripts in this canonical order on a fresh database:

1. `backend/db/00_common.sql` — identity and common tables (Dev 1)
2. `backend/db/01_cases.sql` — case/request tables (Dev 2)
3. `backend/db/02_notif.sql` — notification templates (Dev 3; before AI)
4. `backend/db/03_ai.sql` — recommendations and decision records (Dev 3)
5. `backend/db/04_res.sql` — Member 4 resource tables
6. `backend/db/05_shelter.sql` — Member 4 shelter tables
7. `backend/db/06_vol.sql` — Member 4 volunteer tables
8. `backend/db/08_don.sql` — donation tables (Dev 1)
9. `backend/db/09_donor_portal.sql` — additive donor portal fields and structured items
10. `backend/db/10_operations_centre.sql` — additive public live-map publication fields and indexes
11. `backend/db/11_access_reporting.sql` — staff access applications and hashed-token guest reporting
12. `backend/db/98_foreign_keys.sql` — guarded cross-module foreign keys
13. `backend/db/99_seed_users.sql` — roles and Lead Agency organisation
14. Start the API once in `Development`; `DevelopmentDemoSeeder` creates the demo users.
15. `backend/db/90_seed_scenario.sql` — Member 2 case workflow fixtures
16. `backend/db/91_seed_ops.sql` — Member 4 resource/shelter/volunteer fixtures

`backend/db/07_geo.sql` is intentionally skipped because Member 4 uses `IMemoryCache`, not a persistent `geo.geo_cache` table. The two scenario seed scripts must run after the API has created the demo users. All schema and seed scripts are intended to be safe to re-run.

For a disposable LocalDB setup and the SQL concurrency/constraint probes, see `docs/evidence/dev4/README.md` and `scripts/dev4/`.

### Member 4 frontend integration

Member 4's logistics, shelter, volunteer and map pages are integrated into the active SAFEWAY Next.js application under `frontend/app/(app)/`. They reuse the SAFEWAY authentication/token-refresh API client. The standalone `web/` folder remains as the original handoff/reference implementation; it is no longer the only location of the operations UI.

## 7. Secret Management

Never commit real secrets, passwords, or credentials to Git.

The application may require the following environment variables:

- `EMAIL_ENCRYPTION_KEY`
- `EMAIL_HASH_KEY`
- `JWT_SECRET`
- `SMTP_HOST`
- `SMTP_PORT`
- `SMTP_USERNAME`
- `SMTP_PASSWORD`

### Local Development

`backend/src/Cms.Api/appsettings.Development.json` contains only non-secret
local defaults: a LocalDB connection string, logging, and `DemoSeed:Enabled`.
Provide secrets through `dotnet user-secrets` or environment variables.

In Development, missing `Jwt:SecretKey`, `EmailProtection:EncryptionKey` and
`EmailProtection:HashKey` are replaced with ephemeral process keys so a fresh
checkout can start. Those keys are not persisted, so tokens do not survive a
restart.

To set persistent local keys (do not print or commit the values):

```powershell
cd backend/src/Cms.Api
dotnet user-secrets set "Jwt:SecretKey" "<new random value>"
dotnet user-secrets set "EmailProtection:EncryptionKey" "<32-byte base64 value>"
dotnet user-secrets set "EmailProtection:HashKey" "<32-byte base64 value>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<local SQL connection>"
```

The demo users are upserted on every Development startup using the current
keys, so changing keys does not break demo login. Run the SQL schema scripts
first; if the database is not ready, the seeder logs a warning and the API still
starts.

For deployment, configure `JWT_SECRET`, `EMAIL_ENCRYPTION_KEY`,
`EMAIL_HASH_KEY` and `ConnectionStrings__DefaultConnection` securely through
environment variables or a secret store. SMTP settings are required when email
delivery is configured.

## 8. Team Module Allocation

Refer to `ALLOCATION.md` for the agreed developer responsibilities and module assignments.

Each developer should work within their assigned modules and coordinate changes to shared components.

## 9. Development Workflow

- Use `develop` as the integration branch.
- Work on assigned feature branches.
- Keep changes within assigned modules whenever possible.
- Follow the shared Domain, Application, Infrastructure, and API architecture.
- Coordinate changes involving shared entities, interfaces, and database structures.
- Build and test the backend before submitting changes.
- Submit feature changes through pull requests targeting `develop`.

## 10. Main System Workflow

```text
Submit
  ↓
Verify
  ↓
AI Classify & Score
  ↓
Human Review
  ↓
Allocate
  ↓
Notify
  ↓
Complete
  ↓
Audit
```

AI provides recommendations and decision-support capabilities only. Critical humanitarian decisions remain under human control.

## 11. Build and test

```powershell
cd backend
dotnet build Cms.slnx
dotnet test Cms.slnx
```

The test suite includes an in-memory `WebApplicationFactory` harness that runs
the real middleware, JWT authentication, authorization policies and controllers.
No SQL Server instance is required for `dotnet test`.

## 12. API overview

| Area                  | Endpoints                                                                                                                                                                              |
| --------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Identity              | `/api/v1/auth/register`, `/login`, `/refresh`, `/logout`, `/me`, `/role-check`                                                                                                         |
| Users                 | `/api/v1/users` (SystemAdmin)                                                                                                                                                          |
| Organisations         | `/api/v1/organisations` (SystemAdmin)                                                                                                                                                  |
| Configuration         | `/api/v1/admin/config` (SystemAdmin)                                                                                                                                                   |
| Audit                 | `/api/v1/audit-events` (SystemAdmin)                                                                                                                                                   |
| Outbox                | `/api/v1/admin/outbox/dead` (SystemAdmin dead-letter view)                                                                                                                             |
| AI (Dev 3)            | `/api/v1/recommendations`, `/api/v1/recommendations/match`, `/api/v1/recommendations/{id}/decision` (plural `/decisions` kept as an alias), `/api/v1/ai/chat` (informational FAQ only) |
| Governance (Dev 3)    | `/api/v1/post-incident-review`, `/api/v1/reviews/decisions` (compatibility alias), `/api/v1/announcements`                                                                             |
| Notifications (Dev 3) | `/api/v1/notifications`, `/api/v1/admin/dead-letters` (AdminOnly)                                                                                                                      |
| KPIs                  | `/api/v1/kpis` (`KpiViewer`)                                                                                                                                                           |
| Donations             | `/api/v1/donations`, `/donors`, `/campaigns`, `/report` (`DonationViewer`; writes require `DonationManager`)                                                                           |
| Files                 | `/api/v1/files` (authenticated evidence upload; PNG/JPEG/WebP/PDF, 1 MB)                                                                                                               |
| Health                | `/api/Health` (public)                                                                                                                                                                 |

Swagger is available at `/swagger` in Development. Every endpoint is authenticated
by default; public endpoints use `[AllowAnonymous]`.

A Postman master collection is provided at
`docs/postman/Cms.postman_collection.json`. Set `baseUrl`, `adminEmail` and
`password` collection variables before running it.

## 13. Security

See `docs/SECURITY.md` for the enforced controls, role policies, rate limits,
secret handling and threat-model summary. Do not commit secrets, role strings
outside `RoleNames`, or audit-row updates.

## 14. Testing

See `docs/TESTING.md` for the test layers, commands, coverage matrix, the
end-to-end UAT loop and the manual regression checklist. Run `dotnet test` for
the current backend test count; the in-memory harness does not require a SQL
Server instance. Frontend checks are `npm.cmd run typecheck` and
`npm.cmd run build` from `frontend/`.

## 15. Dev 1 status notes

- Identity, hardening, audit viewer and administration endpoints are implemented
  and covered by the integration harness.
- `D1-02` is complete: the stale EF migrations and `00_foundation.sql` were
  replaced by the idempotent `00_common.sql`, `98_foreign_keys.sql` and
  `99_seed_users.sql`.
- KPI endpoint returns a typed report. The identity, audit and donation sections
  are live aggregates over the `identity`, `common` and `don` schemas; the
  cases, resources, shelter, AI and notification sections stay empty and are
  listed in `pendingSections` until Members 2–4 publish their final
  cross-module table/column names. No guessed SQL is used.
- `98_foreign_keys.sql` currently guards the `notifications` → `identity.users`
  relationship and documents the extension pattern for other cross-module FKs.
- AI decision support (`ai.recommendations`, `ai.decision_records`) is integrated
  from Dev 3 on the shared foundation: `RecommendationService` uses `CmsDbContext`,
  `03_ai.sql` creates the `ai` schema, and `IRecommendationService` is registered
  in `AiModule`. `AiModule` now registers the real `DecisionGate`; the
  fail-closed `DenyAllDecisionGate` is only a fallback when that module is not
  loaded.
- Donations (`D1-09`, P2) are implemented end to end: the idempotent
  `08_don.sql` schema (`don` schema, five tables), donor and campaign
  management, manual monetary/material donation entry, value-constrained
  allocations, campaign expenditure and a currency-aggregated transparency
  report that never exposes donor names. Suspicious values are flagged by a
  rule and cleared only by a human reviewer. The signed-in donor portal adds a
  four-step recording flow, structured multi-item supplies, optional campaign
  intent, caller-scoped tracking and honest, backend-derived impact views;
  `09_donor_portal.sql` adds only optional columns and a structured item table.
- Evidence storage is available to every module through `IFileStore`
  (`LocalFileStore` writes under `FileStorage:RootPath`, `App_Data/uploads` by
  default). `POST /api/v1/files` accepts PNG, JPEG, WebP and PDF up to 1 MB,
  validates the magic bytes and records a `File.Uploaded` audit event. Reading
  a file back is left to the owning module so it can authorise the caller
  against the owning record.
- The transactional outbox (`common.outbox_messages`, `IOutbox`,
  `IOutboxHandler`, `OutboxProcessor`) is owned by Dev 1. `EnqueueAsync` adds
  the event row to the caller's `CmsDbContext` so it commits atomically with the
  business change; the unique `UX_outbox_idempotency_key` index and an in-memory
  pre-check make enqueue idempotent. The background processor polls every five
  seconds, dispatches to the registered handler, retries with exponential
  backoff and dead-letters after three attempts. `GET
/api/v1/admin/outbox/dead` lists dead-lettered rows for SystemAdmin without
  exposing the payload or idempotency key. The processor is disabled in the
  in-memory test harness via `Outbox:ProcessorEnabled=false`. The
  `Notification.Send` handler is registered by the notification module; failed
  sends retry with back-off and dead-letter after three attempts, and Member 3
  owns the admin dead-letter route `/api/v1/admin/dead-letters`.

## 16. Dev 3 status notes (AI, governance, notifications)

- Dev 3 endpoints are `/api/v1/recommendations`, `/api/v1/post-incident-review`
  (with `crisisId`, `from`, `to`, `outcome`, `page`, `pageSize` and a
  `finalRequestStatus` field on each row), `/api/v1/reviews/decisions`
  (compatibility alias), `/api/v1/notifications`, `/api/v1/announcements`,
  `/api/v1/admin/dead-letters` and `/api/v1/ai/chat`.
- AI advice is advisory only. It never performs a critical action; only an
  authorized human role records a decision. The P2 FAQ chat at
  `/operations/ai/chat` is explicitly informational and decides nothing.
- The post-incident review UI is at `/operations/decisions`; it filters by
  crisis id, date range and outcome and shows the final request status.
- Database run order for Dev 3 is `backend/db/00_common.sql` →
  `backend/db/02_notif.sql` → `backend/db/03_ai.sql` → `backend/db/98_foreign_keys.sql`
  → `backend/db/99_seed_users.sql`. `02_notif.sql` seeds English (`en`) and
  Spanish (`es`) template rows keyed by `template_key` + `language`; the
  in-code fallback registry holds English, Sinhala, Tamil and Spanish defaults
  for the core notification keys, with new staff-access keys localized in code.
- Resilience expectations: AI failure, timeout or low confidence falls back to
  the rule-based provider with `Source = Rule`; map failure falls back to
  cache then Haversine (`IsApproximate = true`); email failure leaves the
  notification `Failed`, retries with back-off and dead-letters after three
  attempts. Reprocessing the same outbox idempotency key must produce one
  notification, and dead letters must be visible to administrators.
- Postman: the AI, Governance and Notifications folders in
  `docs/postman/Cms.postman_collection.json` cover the routes above. Swagger XML
  comments are included for these controllers in Development.
- See `docs/DEV3_REPORT.md` for the factual audit and the recorded frontend
  verification results; backend verification remains with the owning worker.

## 17. Final integration status (2026-11-22)

PR #28, #29 and #30 are merged into `develop`. Final professionalization,
coursework compliance mapping, and verification evidence are in:

- `docs/COURSEWORK_COMPLIANCE.md`
- `docs/FINAL_VERIFICATION.md`
- `docs/DEVELOPER_HANDOVER.md`
- `docs/evidence/final-integration-20261122.md`

### Start a clean demo (do not use the original `CmsDb`)

```powershell
# 1. Create a disposable database.
$db = "CmsDb_IntegrationTest"
sqlcmd -S "(localdb)\mssqllocaldb" -E -b -Q "CREATE DATABASE [$db];"

# 2. Canonical schema order (07_geo.sql is stale and intentionally skipped).
$scripts = @(
  "00_common.sql", "01_cases.sql", "02_notif.sql", "03_ai.sql",
  "04_res.sql", "05_shelter.sql", "06_vol.sql", "08_don.sql",
  "09_donor_portal.sql", "10_operations_centre.sql", "11_access_reporting.sql",
  "98_foreign_keys.sql", "99_seed_users.sql"
)
foreach ($s in $scripts) {
  sqlcmd -S "(localdb)\mssqllocaldb" -d $db -I -b -i "backend/db/$s"
}

# 3. Start the API (it seeds demo users on first run; no SMTP/SMS is sent).
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5210"
$env:ConnectionStrings__DefaultConnection = "Server=(localdb)\mssqllocaldb;Database=$db;Trusted_Connection=True;TrustServerCertificate=True;"
$env:Outbox__ProcessorEnabled = "true"
$env:Smtp__Host = ""
dotnet run --project backend/src/Cms.Api/Cms.Api.csproj

# 4. In a second terminal, apply only synthetic demo data, then build/start the frontend.
sqlcmd -S "(localdb)\mssqllocaldb" -d $db -I -b -i backend/db/90_seed_scenario.sql
sqlcmd -S "(localdb)\mssqllocaldb" -d $db -I -b -i backend/db/91_seed_ops.sql
& "C:\Program Files\nodejs\npm.cmd" --prefix frontend install  # fresh clone: install declared packages first
$env:NEXT_PUBLIC_API_URL = "http://localhost:5210"   # baked in at build time
& "C:\Program Files\nodejs\npm.cmd" --prefix frontend run build
& "C:\Program Files\nodejs\npm.cmd" --prefix frontend run start
```

Open `http://localhost:3000`. Demo accounts include `citizen@example.org`,
`coordinator@example.org`, `logistics@example.org`, `shelter@example.org`,
`admin@example.org`, `volunteercoordinator@example.org` and `donor@example.org`
(password `Demo1234!`). Health check: `GET http://localhost:5210/api/health`.

**Known limitations:**

- Nominatim currently answers the application's compliant User-Agent with HTTP
  `403`; geocoding returns `404 GEOCODE_NOT_FOUND` while routing, stored
  coordinates and Haversine fallback keep mapping workflows usable. No provider
  restriction was bypassed.
- Notification delivery is in-app and log/email in demo mode; no real email or
  SMS is sent. SMS/push payment and advanced forecasting dashboard remain out of
  prototype scope or bonus/optional where the coursework allows.
- Automated tests and live API evidence cover the core workflow; formal
  participant UAT/usability sessions are not recorded in this repository unless
  added to `docs/UAT_CORE_LOOP.md` and `docs/USABILITY_TEST.md`.
