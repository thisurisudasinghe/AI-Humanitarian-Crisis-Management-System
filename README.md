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
│   │   ├── 01_cases.sql ... 08_don.sql   (module owners)
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

1. Create a SQL Server database named `CmsDb`.
2. Configure the connection string (`ConnectionStrings__DefaultConnection`).
3. Run the ordered SQL scripts from `backend/db`.

The schema is defined by plain, idempotent SQL scripts. EF migrations are not
used and `dotnet ef database update` is not part of the workflow.

### SQL Script Execution Order

Run the scripts in this canonical order:

1. `backend/db/00_common.sql` — identity and common tables (Dev 1)
2. `backend/db/01_cases.sql` … `backend/db/08_don.sql` — module scripts (owners)
3. `backend/db/98_foreign_keys.sql` — guarded cross-module foreign keys
4. `backend/db/99_seed_users.sql` — roles, demo organisation, demo users, settings
5. `backend/db/90_seed_scenario.sql`, `backend/db/91_seed_ops.sql` — optional demo data

Example with `sqlcmd` (the `-I` flag is required for filtered indexes):

```powershell
sqlcmd -S "(localdb)\mssqllocaldb" -d CmsDb -I -b -i backend/db/00_common.sql
sqlcmd -S "(localdb)\mssqllocaldb" -d CmsDb -I -b -i backend/db/98_foreign_keys.sql
sqlcmd -S "(localdb)\mssqllocaldb" -d CmsDb -I -b -i backend/db/99_seed_users.sql
```

Every Dev 1 script is idempotent: re-running it skips existing tables, indexes,
roles, users and settings instead of failing.

Demo users are seeded with the password `Demo1234!`. See
`backend/db/99_seed_users.sql` for the full list of demo accounts and roles.

## 7. Secret Management

Never commit real secrets, passwords, or credentials to Git. The only exception
is the fixed, public, development-only configuration described below.

The application may require the following environment variables:

- `EMAIL_ENCRYPTION_KEY`
- `EMAIL_HASH_KEY`
- `JWT_SECRET`
- `SMTP_HOST`
- `SMTP_PORT`
- `SMTP_USERNAME`
- `SMTP_PASSWORD`

### Local Development

`backend/src/Cms.Api/appsettings.Development.json` already provides fixed
development-only keys and a LocalDB connection string, so a fresh checkout can
run the SQL-seeded demo users without extra setup. These keys are public and
must never be used outside local development.

To override only the connection string (for example, to use a different SQL
Server instance):

```powershell
cd backend/src/Cms.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<local SQL connection>"
```

If you replace the email or JWT keys with your own values, the demo users seeded
by `99_seed_users.sql` will no longer be found by email until you reseed them
with the new keys. User-secrets override `appsettings.Development.json`, so
remove any old values before using the committed demo keys:

```powershell
dotnet user-secrets remove "EmailProtection:EncryptionKey"
dotnet user-secrets remove "EmailProtection:HashKey"
dotnet user-secrets remove "Jwt:SecretKey"
```

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

| Area | Endpoints |
|---|---|
| Identity | `/api/v1/auth/register`, `/login`, `/refresh`, `/logout`, `/me`, `/role-check` |
| Users | `/api/v1/users` (SystemAdmin) |
| Organisations | `/api/v1/organisations` (SystemAdmin) |
| Configuration | `/api/v1/admin/config` (SystemAdmin) |
| Audit | `/api/v1/audit-events` (SystemAdmin) |
| KPIs | `/api/v1/kpis` (`KpiViewer`) |
| Donations | `/api/v1/donations`, `/donors`, `/campaigns`, `/report` (`DonationViewer`; writes require `DonationManager`) |
| Files | `/api/v1/files` (authenticated evidence upload; PNG/JPEG/WebP/PDF, 1 MB) |
| Health | `/api/Health` (public) |

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
end-to-end UAT loop and the manual regression checklist. The suite currently
runs 94 backend tests with no SQL Server instance required.

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
- Donations (`D1-09`, P2) are implemented end to end: the idempotent
  `08_don.sql` schema (`don` schema, five tables), donor and campaign
  management, manual monetary/material donation entry, value-constrained
  allocations, campaign expenditure and a currency-aggregated transparency
  report that never exposes donor names. Suspicious values are flagged by a
  rule and cleared only by a human reviewer.
- Evidence storage is available to every module through `IFileStore`
  (`LocalFileStore` writes under `FileStorage:RootPath`, `App_Data/uploads` by
  default). `POST /api/v1/files` accepts PNG, JPEG, WebP and PDF up to 1 MB,
  validates the magic bytes and records a `File.Uploaded` audit event. Reading
  a file back is left to the owning module so it can authorise the caller
  against the owning record.
