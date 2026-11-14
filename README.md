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