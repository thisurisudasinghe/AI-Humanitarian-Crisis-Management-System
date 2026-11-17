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
│   │   ├── 00_foundation.sql
│   │   └── 98_foreign_keys.sql
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
2. Configure the database connection string in the API configuration.
3. Apply the schema.

> **Known state (D1-02 pending):** The checked-in EF migrations predate the V4
> `identity`/`common` snake_case model, so `dotnet ef database update` currently
> stops with `PendingModelChangesWarning`. Do not regenerate or delete the
> migrations yet: the agreed plan is to replace them with ordered SQL scripts
> once the D-01 schema decision is signed off. Until then, create a scratch
> database from the current model (for example with an `EnsureCreated` harness)
> when you need to run the API locally.

### SQL Script Execution Order

After D1-02 lands, execute the scripts in the canonical order:

1. `backend/db/00_common.sql`
2. `backend/db/01_cases.sql` … `backend/db/08_don.sql` (module owners publish these)
3. `backend/db/98_foreign_keys.sql`
4. `backend/db/99_seed_users.sql`
5. `backend/db/90_seed_scenario.sql`, `backend/db/91_seed_ops.sql`

**Important:** Do not execute a foundation script against a database that already
contains the foundation tables.

## 7. Secret Management

Never commit secrets, passwords, or credentials to Git.

The application may require the following environment variables:

- `EMAIL_ENCRYPTION_KEY`
- `EMAIL_HASH_KEY`
- `JWT_SECRET`
- `SMTP_HOST`
- `SMTP_PORT`
- `SMTP_USERNAME`
- `SMTP_PASSWORD`

### Local Development

Navigate to the API project:

```powershell
cd backend/src/Cms.Api
dotnet user-secrets init
```

Set secret values using `dotnet user-secrets set` and the configuration keys expected by the application:

- `EmailProtection:EncryptionKey`
- `EmailProtection:HashKey`
- `Jwt:SecretKey`

Use separate secure values for encryption, email hashing, and JWT signing.

Never share or commit these values.

For deployment, configure the corresponding environment variables securely. SMTP settings are required when email delivery is configured.

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

## 14. Dev 1 status notes

- Identity, hardening, audit viewer and administration endpoints are implemented
  and covered by the integration harness.
- KPI endpoint returns a typed provisional report until Members 2–4 publish the
  final cross-module table/column names; no guessed SQL is used.
- Plain SQL script replacement (`D1-02`) and cross-module foreign keys (`98`)
  remain gated on the D-01 team decision. Keep the EF migrations until sign-off.
- Donations (`D1-09`) is P2 and is not part of the P1 release unless the core
  loop is green before feature freeze.
