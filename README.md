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
3. Apply EF Core migrations from the repository root:

```powershell
dotnet ef database update --project backend/src/Cms.Infrastructure --startup-project backend/src/Cms.Api
```

### SQL Script Execution Order

For manual SQL setup, execute the following scripts in order:

1. `backend/db/00_foundation.sql`
2. `backend/db/98_foreign_keys.sql`

**Important:** Do not execute the foundation script against a database that already contains the foundation tables.

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