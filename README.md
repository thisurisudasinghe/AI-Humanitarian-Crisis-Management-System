# AI-Powered Humanitarian Crisis Management System

## 1. Project Overview
A modular humanitarian crisis management platform supporting crisis response, aid coordination, resource allocation, notifications, and reporting.

## 2. Technology Stack
- Backend: ASP.NET Core Web API (.NET 10)
- Database: Microsoft SQL Server
- ORM: Entity Framework Core
- Frontend: React / Next.js
- Architecture: Modular Monolith

## 3. Prerequisites
- .NET SDK 10.0.400
- SQL Server
- Git
- Node.js (for frontend development)

## 4. Project Setup

Clone the repository and open the project folder.

```powershell
dotnet restore Cms.slnx
dotnet build Cms.slnx
```

## 5. Database Setup

1. Create a SQL Server database named `CmsDb`.
2. Configure the database connection string in the API configuration.
3. Apply EF Core migrations:

```powershell
dotnet ef database update --project src/Cms.Infrastructure --startup-project src/Cms.Api
```

### SQL Script Run Order
For manual SQL setup, execute:

1. `db/00_foundation.sql`
2. `db/98_foreign_keys.sql`

Do not execute the foundation script against a database that already has the foundation tables.

## 6. Secret Management

Never commit secrets or credentials to Git.

Required environment variable names:

- `EMAIL_ENCRYPTION_KEY`
- `EMAIL_HASH_KEY`
- `JWT_SECRET`
- `SMTP_HOST`
- `SMTP_PORT`
- `SMTP_USERNAME`
- `SMTP_PASSWORD`

For local development, use .NET User Secrets:

```powershell
cd src/Cms.Api
dotnet user-secrets init
```

Set secret values locally using `dotnet user-secrets set`. Use the configuration keys expected by the application:

- `EmailProtection:EncryptionKey`
- `EmailProtection:HashKey`
- `Jwt:SecretKey`

Use separate secure values for encryption, email hashing, and JWT signing. Never share or commit these values.

For deployment, configure the corresponding environment variables securely. SMTP settings are required when email delivery is configured.

## 7. Team Module Allocation

Refer to `ALLOCATION.md` for the agreed developer responsibilities.

## 8. Development Workflow

- Branch from `develop`.
- Work only within your assigned modules.
- Follow the shared Domain, Application, Infrastructure, and API structure.
- Coordinate changes to shared entities and interfaces.
- Build and test before submitting changes.

## 9. Main System Workflow

Submit → Verify → AI Classify & Score → Human Review → Allocate → Notify → Complete → Audit

AI provides recommendations only. Critical decisions remain under human control.

## 10. Build and test

```powershell
dotnet build Cms.slnx
dotnet test Cms.slnx
```

The test suite includes an in-memory `WebApplicationFactory` harness that runs
the real middleware, JWT authentication, authorization policies and controllers.
No SQL Server instance is required for `dotnet test`.

## 11. API overview

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

## 12. Security

See `docs/SECURITY.md` for the enforced controls, role policies, rate limits,
secret handling and threat-model summary. Do not commit secrets, role strings
outside `RoleNames`, or audit-row updates.

## 13. Dev 1 status notes

- Identity, hardening, audit viewer and administration endpoints are implemented
  and covered by the integration harness.
- KPI endpoint returns a typed provisional report until Members 2–4 publish the
  final cross-module table/column names; no guessed SQL is used.
- Plain SQL script replacement (`D1-02`) and cross-module foreign keys (`98`)
  remain gated on the D-01 team decision. Keep the EF migrations until sign-off.
- Donations (`D1-09`) is P2 and is not part of the P1 release unless the core
  loop is green before feature freeze.
