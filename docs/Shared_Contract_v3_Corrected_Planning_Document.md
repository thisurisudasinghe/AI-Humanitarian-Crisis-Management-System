# Shared Contract v3 --- Corrected Planning Document

**Project:** AI-Powered Humanitarian Crisis Management and Aid
Coordination Platform **Review date:** 13 Nov 2026 \| **Deadline:** 30
Nov 2026 (17 days remaining) **Status:** Proposal --- awaiting team
approval before scaffolding begins **Changes from:** Shared Contract v2

------------------------------------------------------------------------

## Corrections Made (summary)

  ---------------------------------------------------------------------------------------------------------------
  \#                Issue             v2 Problem                                v3 Fix
  ----------------- ----------------- ----------------------------------------- ---------------------------------
  1                 Identity & Roles  No `Role` or `UserRole` entities;         Added `Role`, `UserRole`
                                      multi-role user structure undefined       entities, SQL tables, EF configs,
                                                                                seed data, JWT array claim

  2                 Foundation DB     `RefreshTokens` table missing from        All 7 foundation tables now
                                      `00_foundation.sql`; `Roles`/`UserRoles`  defined with PKs, FKs, indexes,
                                      absent                                    unique constraints

  3                 Notifications     No `Notification` entity;                 Added `Notification` entity (Dev
                                      `OutboxMessage.Status` conflated with     1); clarified OutboxMessage vs
                                      delivery status                           Notification distinction

  4                 Geolocation       `IGeoService.GetDistanceAsync`            Corrected: Nominatim = geocoding
                                      incorrectly claimed "Nominatim route,     only; Haversine = all distance
                                      then Haversine fallback"                  calculations; routing =
                                                                                optional/not in P1

  5                 Idempotency       `AllocateRequestDto { IdempotencyKey }`   Resolved: HTTP `Idempotency-Key`
                                      in body conflicted with original          header for allocation POST; body
                                      Contract's HTTP header spec               has only `DecisionId`; DB unique
                                                                                constraint on
                                                                                `Allocations.IdempotencyKey`;
                                                                                unique constraint on
                                                                                `OutboxMessages.IdempotencyKey`

  6                 Dev 1 workload    32 items across Days 1--4 with no clear   Restructured into 3 phases: Day 1
                                      blocking/deferrable split                 (everyone unblocked), Day 2--3
                                                                                (auth + infra), Day 4--6
                                                                                (features, non-blocking)

  7                 Interface         "Owner" column ambiguously meant both     Separated into "Defined by"
                    ownership         "defines" and "implements"                (always Dev 1 in Contracts/) and
                                                                                "Implemented by" (the responsible
                                                                                developer)
  ---------------------------------------------------------------------------------------------------------------

------------------------------------------------------------------------

## A. Corrected Technical Decisions

No changes from v2 (A1 PDF finding, A2 .NET 10 LTS, A3 other decisions)
**except** the following two items:

### A.4 Geolocation --- corrected decision (replaces v2 E4 and v2 E6 commentary in D3)

> \[!IMPORTANT\] **Nominatim is a geocoding service, not a routing
> service.** This was incorrect in v2.

  ---------------------------------------------------------------------------
  Capability        Service           Accuracy          Notes
  ----------------- ----------------- ----------------- ---------------------
  Address →         **Nominatim**     Exact (when       External HTTP call;
  coordinates       (OpenStreetMap)   matched)          cache results;
                                                        IsApproximate=false

  Coordinates →     **Nominatim**     Exact             Same caching applies
  address           reverse geocode                     

  Straight-line     **Haversine       Approximate by    No external call;
  distance          formula**         nature            IsApproximate=true
                    (internal)                          always

  Road-routing      *(not implemented ---               SRS says "external
  distance/time     in P1)*                             provider" for routing
                                                        --- deferred;
                                                        Haversine satisfies
                                                        FR-MAP-06 fallback
  ---------------------------------------------------------------------------

**Corrected `IGeoService` contract:**

    GeocodeAsync(address: string) → GeocodeResultDto
      - Checks in-memory/DB cache first
      - On cache miss: calls Nominatim API via AddCmsHttpClient (timeout + retry)
      - On Nominatim failure: returns IsApproximate=true with null coordinates
      - Never throws; always returns a result

    GetDistanceAsync(from, to) → DistanceResultDto
      - Always uses Haversine formula (pure math, no external call)
      - Returns IsApproximate=true unconditionally
      - Optionally, Minutes=null (road time not calculated)

**Report justification:** "Road-routing calculation requires a separate
routing engine (e.g., OSRM). Within the 17-day constraint, we use
Haversine straight-line distance, which satisfies SRS FR-MAP-06's
requirement for a fallback when the external provider is unavailable,
and is acceptable for resource matching and shelter recommendation."

### A.5 Idempotency --- corrected decision (replaces v2 A3 last row and D1-2)

The v2 contract placed `IdempotencyKey` inside `AllocateRequestDto`
(request body), contradicting the original Contract's specification of
an HTTP **header**. The corrected approach:

  --------------------------------------------------------------------------------------------------
  Concern                 Mechanism                                  Location
  ----------------------- ------------------------------------------ -------------------------------
  Allocation duplicate    `Idempotency-Key` **HTTP header** (string) Request header --- client
  prevention                                                         supplies; server reads from
                                                                     `HttpContext.Request.Headers`

  Allocation body         `AllocateRequestDto { DecisionId }` only   No IdempotencyKey in body

  DB enforcement          `UNIQUE` constraint on                     `03_resources.sql` (Dev 4)
                          `Allocations.IdempotencyKey`               

  Outbox duplicate        `UNIQUE` constraint on                     `00_foundation.sql` (Dev 1)
  prevention              `OutboxMessages.IdempotencyKey`            

  ShelterAllocation       `Idempotency-Key` header on                Same pattern
  duplicate               `POST /api/v1/shelters/{id}/allocations`   

  IdempotencyKey format   `"{type}:{entityId}:{version}"` e.g.,      Documented in comment on
                          `"Allocation:req-guid:1"`                  `IOutbox` and in Swagger
  --------------------------------------------------------------------------------------------------

**Corrected `AllocateRequestDto`:**

    AllocateRequestDto { DecisionId: Guid }

The service layer reads `Idempotency-Key` from the HTTP header and
stores it. A second identical request with the same key returns the
original response (200/201) without re-executing.

------------------------------------------------------------------------

## B. Module-to-Developer Allocation

**Unchanged from v2.** Allocation is confirmed correct and balanced.

  -----------------------------------------------------------------------
  Developer               SRS Modules             Primary SRS Features
  ----------------------- ----------------------- -----------------------
  **Dev 1**               M1 Identity/Access · M9 Auth, roles, JWT,
                          Notifications · M11     outbox processor,
                          Reporting/KPIs · M12    notification delivery,
                          Administration          KPIs, admin

  **Dev 2**               M2 Crisis/Case          Requests, state
                          Management · M8 Donor   machine, crisis events,
                          (P2)                    merge/escalate, donor
                                                  (lean, deferred)

  **Dev 3**               M3 AI Decision Support  AI pipeline,
                          · M4 Governance/Audit   recommendations,
                                                  decisions, duplicate
                                                  detection,
                                                  post-incident review

  **Dev 4**               M5 Resource/Logistics · Inventory, allocation,
                          M6 Shelter · M7         shelter capacity,
                          Volunteer · M10 Mapping volunteer tasks,
                                                  geocoding, distance
  -----------------------------------------------------------------------

------------------------------------------------------------------------

## C. Corrected Shared Foundation --- Dev 1 Responsibilities

### C1. Phase 1 --- Day 1 (everyone unblocked by EOD Day 1)

These items block all three other developers from starting. Dev 1 must
complete all of them before anyone else writes module code.

  ---------------------------------------------------------------------------------------------------------------------------------------------
  \#                Deliverable                                                           Location                            Blocks
  ----------------- --------------------------------------------------------------------- ----------------------------------- -----------------
  C1-01             `Cms.sln` with 5 projects; all compile with empty namespaces          root                                All

  C1-02             `global.json` pinning .NET 10.0.400                                   root                                All

  C1-03             `EntityBase { Id: Guid, CreatedAt: DateTime, UpdatedAt: DateTime }`   `Cms.Domain/Common/`                All

  C1-04             All shared enums (Section E1) in separate files                       `Cms.Domain/Enums/`                 All

  C1-05             Shared domain exceptions: `DomainException`, `NotFoundException`,     `Cms.Domain/Exceptions/`            All
                    `ForbiddenException`, `ConflictException`                                                                 

  C1-06             `PagedResult<T>` and `PagedQuery` (page default 1, pageSize default   `Cms.Application/Common/`           All
                    20 max 100)                                                                                               

  C1-07             All shared interface files in `Contracts/` as stubs                   `Cms.Application/Contracts/`        All
                    (`throw new NotImplementedException()`)                                                                   

  C1-08             All shared DTOs (Section E5)                                          `Cms.Application/DTOs/`             All

  C1-09             Foundation entity files: `User`, `Role`, `UserRole`, `Organisation`,  `Cms.Domain/Identity/` and          All
                    `RefreshToken`, `AuditLog`, `OutboxMessage`, `Notification`           `Cms.Domain/Shared/`                

  C1-10             `CmsDbContext` skeleton with auto-discovery of                        `Cms.Infrastructure/Persistence/`   All
                    `IEntityTypeConfiguration`; DbSets for foundation entities only                                           

  C1-11             `Program.cs` with `AddXxxModule()` placeholder comments and minimal   `Cms.Api/`                          All
                    middleware pipeline (no auth yet)                                                                         

  C1-12             `/db/00_foundation.sql` --- all 7 foundation tables (Section C3)      `/db/`                              All

  C1-13             `/db/98_foreign_keys.sql` stub file                                   `/db/`                              All

  C1-14             `ALLOCATION.md` committed to `develop` with the agreed                root                                All
                    module-to-developer table                                                                                 
  ---------------------------------------------------------------------------------------------------------------------------------------------

### C2. Phase 2 --- Days 2--3 (auth infrastructure; unblocks Dev 2 for citizen registration)

  -----------------------------------------------------------------------------------------------------------------------------------
  \#                Deliverable                         Location                                                    Blocks
  ----------------- ----------------------------------- ----------------------------------------------------------- -----------------
  C2-01             BCrypt password hasher utility      `Cms.Infrastructure/Auth/`                                  Dev 2 register

  C2-02             JWT token service: generate access  `Cms.Infrastructure/Auth/JwtService.cs`                     Dev 2 register
                    token (15-min), refresh token                                                                   
                    (7-day), validate, revoke                                                                       

  C2-03             `ICurrentUser` real implementation  `Cms.Infrastructure/Auth/CurrentUserService.cs`             All services
                    reading JWT claims from                                                                         
                    `HttpContext`                                                                                   

  C2-04             Auth endpoints:                     `Cms.Api/Controllers/AuthController.cs`                     Dev 2 test
                    `POST /api/v1/auth/register`,                                                                   
                    `/login`, `/refresh`, `/logout`                                                                 

  C2-05             JWT setup in `Program.cs`:          `Cms.Api/Program.cs`                                        All `[Authorize]`
                    `options.RoleClaimType = "role"`,                                                               
                    `MapInboundClaims = false`                                                                      

  C2-06             `GlobalExceptionHandler` middleware `Cms.Api/Middleware/`                                       All error
                    → RFC 7807 ProblemDetails + `code`                                                              handling
                    string field                                                                                    

  C2-07             Swagger with Bearer auth scheme,    `Cms.Api/Program.cs`                                        All devs testing
                    `JsonStringEnumConverter`, UTC                                                                  
                    DateTime                                                                                        

  C2-08             CORS policy                         `Cms.Api/Program.cs`                                        Dev 2 frontend
                    (`http://localhost:3000`) and rate                                                              
                    limiting (60/min on `/auth`)                                                                    

  C2-09             `AddCmsHttpClient(name, baseUrl)`   `Cms.Infrastructure/Http/`                                  Dev 3 (AI calls),
                    Polly factory (10 s timeout, 3                                                                  Dev 4 (Nominatim)
                    retries, circuit breaker 5/30 s)                                                                

  C2-10             EF Core UTC DateTime value          `CmsDbContext.cs`                                           All entity writes
                    converter applied globally in                                                                   
                    `OnModelCreating`                                                                               

  C2-11             `IEntityTypeConfiguration` files    `Cms.Infrastructure/Persistence/Configurations/Identity/`   DB creation
                    for all foundation entities (C1-09) and `/Shared/`                                              

  C2-12             `web/lib/api.ts` --- HTTP client    `/web/lib/`                                                 Frontend devs
                    with Bearer token injection,                                                                    
                    automatic refresh interceptor                                                                   

  C2-13             In-memory stubs for all interfaces  `Cms.Infrastructure/Stubs/`                                 All devs
                    not yet implemented by other devs                                                               
  -----------------------------------------------------------------------------------------------------------------------------------

### C3. Phase 3 --- Days 4--6 (Dev 1 features; non-blocking for other developers)

  ---------------------------------------------------------------------------------------
  \#                      Deliverable                             Day target
  ----------------------- --------------------------------------- -----------------------
  C3-01                   `IOutbox` real implementation (insert   Day 4
                          to `OutboxMessages`, enforce            
                          idempotency key unique)                 

  C3-02                   Outbox background processor             Day 4
                          `IHostedService` (5-second poll,        
                          dispatch to handlers, dead-letter after 
                          3 retries)                              

  C3-03                   `IAuditLogger` real implementation      Day 4
                          (insert to `AuditLogs`, mask sensitive  
                          fields)                                 

  C3-04                   `Notification.Send` outbox handler →    Day 4
                          creates `Notification` row + sends      
                          email via SMTP MailKit                  

  C3-05                   `IFileStore` local disk                 Day 4
                          implementation + `POST /api/v1/files`   
                          endpoint                                

  C3-06                   Admin endpoints:                        Day 5
                          `GET/POST/PATCH /api/v1/admin/users`,   
                          `/roles`, `/organisations`, `/audit`    

  C3-07                   Dead-letter admin view:                 Day 5
                          `GET /api/v1/admin/outbox/dead`         

  C3-08                   In-app notification poll endpoint:      Day 5
                          `GET /api/v1/notifications` for         
                          authenticated user                      

  C3-09                   `/db/99_seed.sql` --- 10 roles, 2--3    Day 5
                          users per role, 1 crisis, 2 areas       

  C3-10                   KPI endpoint:                           Day 6
                          `GET /api/v1/reports/kpis` (placeholder 
                          aggregates; real queries added Day 7+)  

  C3-11                   Frontend: auth pages (login, register), Day 6
                          layout, nav, error boundary             

  C3-12                   Frontend: admin panel skeleton          Day 6
  ---------------------------------------------------------------------------------------

### C4. What Dev 1 does NOT own

-   Entity classes, EF configurations and SQL tables for modules owned
    by Dev 2, 3 and 4
-   Service and repository implementations inside Dev 2/3/4 module
    folders
-   `AddXxxModule(IServiceCollection)` extensions for other modules
-   Frontend pages under `/web/app/crisis/`, `/web/app/ai/`,
    `/web/app/resources/`, `/web/app/shelter/`, `/web/app/volunteers/`

------------------------------------------------------------------------

## D. Corrected Module-Specific Responsibilities

No structural changes from v2. Only Dev 4's geo section is corrected.

### D3 (corrected) --- Dev 4: Geolocation

Replace the v2 `IGeoService` row in D3 with:

  -----------------------------------------------------------------------
  Responsibility                      Corrected Detail
  ----------------------------------- -----------------------------------
  `IGeoService` implementation        Two distinct behaviours: (1)
                                      `GeocodeAsync` calls Nominatim via
                                      `AddCmsHttpClient`; caches result
                                      in memory/DB; on failure returns
                                      `IsApproximate=true`. (2)
                                      `GetDistanceAsync` always uses
                                      Haversine formula (no external
                                      call, no timeout risk); always
                                      returns `IsApproximate=true`

  `DistanceResultDto.Minutes`         Always `null` in P1 --- road travel
                                      time is not calculated

  Geocode cache                       Simple in-memory `IMemoryCache`
                                      keyed on normalised address string;
                                      24-hour sliding expiry

  No OSRM dependency                  Road routing is explicitly out of
                                      P1 scope; do not add OSRM client
  -----------------------------------------------------------------------

### D4 (corrected) --- Dev 4: Idempotency for allocation

Replace the v2 allocation row in D3 with:

  -----------------------------------------------------------------------------------------------------------------------------------------------------------
  Responsibility                             Corrected Detail
  ------------------------------------------ ----------------------------------------------------------------------------------------------------------------
  `POST /api/v1/allocations`                 Reads `Idempotency-Key` from HTTP header. If the key already exists in `Allocations` table, returns the existing
                                             allocation (200 OK) without executing again. Otherwise: calls `IDecisionGate.RequireApprovedAsync`, executes
                                             `UPDATE Inventory SET Available = Available - @qty WHERE Available >= @qty` in one DB transaction, inserts
                                             `Allocation` row with the key, calls `IRequestStatusService.TransitionAsync(Allocated)`.

  `POST /api/v1/shelters/{id}/allocations`   Same pattern: `Idempotency-Key` header;
                                             `UPDATE Shelter SET CurrentOccupancy = CurrentOccupancy + @size WHERE (Capacity - CurrentOccupancy) >= @size`;
                                             unique key on `ShelterAllocations.IdempotencyKey`

  `AllocateRequestDto`                       `{ DecisionId: Guid }` only --- no `IdempotencyKey` in body
  -----------------------------------------------------------------------------------------------------------------------------------------------------------

------------------------------------------------------------------------

## E. Corrected Shared Entities, Enums, DTOs and Interfaces

### E1. Shared enums --- no changes from v2

All enums from v2 Section E1 are correct and unchanged.

------------------------------------------------------------------------

### E2. Foundation entities --- corrected (adds Role, UserRole, Notification)

#### All 9 foundation entities owned by Dev 1

  -----------------------------------------------------------------------------------------------------
  Entity                  Location                 Key Fields
  ----------------------- ------------------------ ----------------------------------------------------
  `Organisation`          `Cms.Domain/Identity/`   Id, Name, Type (string), IsActive

  `User`                  `Cms.Domain/Identity/`   Id, Email, PasswordHash, FirstName, LastName,
                                                   OrgId?, IsActive

  `Role`                  `Cms.Domain/Identity/`   Id, Name (exact role string --- see below)

  `UserRole`              `Cms.Domain/Identity/`   UserId (FK), RoleId (FK) --- composite PK, no
                                                   EntityBase

  `RefreshToken`          `Cms.Domain/Identity/`   Id, UserId (FK), TokenHash, ExpiresAt, IsRevoked,
                                                   CreatedAt

  `AuditLog`              `Cms.Domain/Shared/`     Id, Action, EntityType, EntityId?, ActorUserId,
                                                   Details (nvarchar(max)), CreatedAt only (no
                                                   UpdatedAt --- append-only)

  `OutboxMessage`         `Cms.Domain/Shared/`     Id, Type, EntityId, Payload (nvarchar(max)),
                                                   IdempotencyKey, Status (enum:
                                                   Pending/Processing/Processed/Failed/DeadLettered),
                                                   RetryCount, NextRetryAt?, ProcessedAt?,
                                                   ErrorMessage?

  `Notification`          `Cms.Domain/Shared/`     Id, RecipientUserId, Channel (enum), TemplateKey,
                                                   Subject?, Body?, Status (enum: Pending/Sent/Failed),
                                                   SentAt?, RequestId?, CreatedAt, UpdatedAt
  -----------------------------------------------------------------------------------------------------

> \[!NOTE\] `AuditLog` intentionally omits `UpdatedAt` --- it is
> append-only and must never be updated. `UserRole` has no `EntityBase`
> inheritance because its identity is the composite key
> `(UserId, RoleId)`, not a surrogate Guid.

#### Role name seed values (exact strings --- frozen)

    Citizen, EmergencyCoordinator, CaseManager, LogisticsManager,
    ShelterAdmin, VolunteerCoordinator, Volunteer, DonorOfficer,
    PartnerOfficer, SystemAdmin

#### OutboxMessage.Status vs Notification.Status --- distinction

  ------------------------------------------------------------------------------
  Field                    Tracks            Values            Owner
  ------------------------ ----------------- ----------------- -----------------
  `OutboxMessage.Status`   Whether the       Pending →         Dev 1 processor
                           outbox            Processing →      
                           **processor** has Processed /       
                           picked up and     Failed →          
                           handled the event DeadLettered      

  `Notification.Status`    Whether the       Pending → Sent /  Dev 1
                           **delivery** to   Failed            notification
                           the recipient                       handler
                           succeeded                           
  ------------------------------------------------------------------------------

An `OutboxMessage` with type `Notification.Send` is Processed once the
handler runs. The handler then creates a `Notification` row and attempts
delivery. The `Notification` row's status is updated independently of
the outbox.

------------------------------------------------------------------------

### E3. Foundation SQL schema --- `00_foundation.sql` (corrected, complete)

All 7 foundation tables with PKs, FKs, indexes and unique constraints.
Dev 1 owns this file.

``` sql
-- 00_foundation.sql  (Dev 1)
-- Run order: this file first, then 01..06, then 98_foreign_keys.sql, then 99_seed.sql

-- ─────────────────── ORGANISATIONS ───────────────────
CREATE TABLE organisations (
    id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_organisations PRIMARY KEY,
    name            NVARCHAR(200)    NOT NULL,
    org_type        NVARCHAR(100)    NOT NULL,
    is_active       BIT              NOT NULL DEFAULT 1,
    created_at      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME()
);

-- ─────────────────── USERS ────────────────────────────
CREATE TABLE users (
    id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_users PRIMARY KEY,
    email           NVARCHAR(256)    NOT NULL,   -- store encrypted; index on hash
    email_hash      NVARCHAR(64)     NOT NULL,   -- SHA-256 hex of lowercase email for lookup
    password_hash   NVARCHAR(256)    NOT NULL,
    first_name      NVARCHAR(100)    NOT NULL,
    last_name       NVARCHAR(100)    NOT NULL,
    org_id          UNIQUEIDENTIFIER NULL,
    is_active       BIT              NOT NULL DEFAULT 1,
    created_at      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_users_organisations FOREIGN KEY (org_id) REFERENCES organisations(id)
);
CREATE UNIQUE INDEX UX_users_email_hash ON users(email_hash);
CREATE INDEX IX_users_org_id        ON users(org_id);

-- ─────────────────── ROLES ────────────────────────────
CREATE TABLE roles (
    id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_roles PRIMARY KEY,
    name            NVARCHAR(100)    NOT NULL,
    created_at      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX UX_roles_name ON roles(name);

-- ─────────────────── USER_ROLES (join) ────────────────
CREATE TABLE user_roles (
    user_id         UNIQUEIDENTIFIER NOT NULL,
    role_id         UNIQUEIDENTIFIER NOT NULL,
    assigned_at     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_user_roles PRIMARY KEY (user_id, role_id),
    CONSTRAINT FK_user_roles_users FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT FK_user_roles_roles FOREIGN KEY (role_id) REFERENCES roles(id)
);
CREATE INDEX IX_user_roles_role_id ON user_roles(role_id);

-- ─────────────────── REFRESH_TOKENS ───────────────────
CREATE TABLE refresh_tokens (
    id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_refresh_tokens PRIMARY KEY,
    user_id         UNIQUEIDENTIFIER NOT NULL,
    token_hash      NVARCHAR(256)    NOT NULL,   -- SHA-256 of the opaque token
    expires_at      DATETIME2        NOT NULL,
    is_revoked      BIT              NOT NULL DEFAULT 0,
    created_at      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_refresh_tokens_users FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX UX_refresh_tokens_hash ON refresh_tokens(token_hash);
CREATE INDEX IX_refresh_tokens_user_id ON refresh_tokens(user_id);

-- ─────────────────── AUDIT_LOG ────────────────────────
CREATE TABLE audit_log (
    id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_audit_log PRIMARY KEY,
    action          NVARCHAR(200)    NOT NULL,
    entity_type     NVARCHAR(200)    NOT NULL,
    entity_id       UNIQUEIDENTIFIER NULL,
    actor_user_id   UNIQUEIDENTIFIER NULL,       -- NULL for system actions
    details         NVARCHAR(MAX)    NULL,        -- JSON; sensitive fields masked
    created_at      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME()
    -- no updated_at: append-only
);
CREATE INDEX IX_audit_log_entity    ON audit_log(entity_type, entity_id);
CREATE INDEX IX_audit_log_actor     ON audit_log(actor_user_id);
CREATE INDEX IX_audit_log_created   ON audit_log(created_at DESC);

-- ─────────────────── OUTBOX_MESSAGES ─────────────────
CREATE TABLE outbox_messages (
    id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_outbox_messages PRIMARY KEY,
    type                NVARCHAR(100)    NOT NULL,
    entity_id           UNIQUEIDENTIFIER NOT NULL,
    payload             NVARCHAR(MAX)    NOT NULL,  -- JSON
    idempotency_key     NVARCHAR(256)    NOT NULL,
    status              NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    retry_count         INT              NOT NULL DEFAULT 0,
    next_retry_at       DATETIME2        NULL,
    processed_at        DATETIME2        NULL,
    error_message       NVARCHAR(MAX)    NULL,
    created_at          DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at          DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX UX_outbox_idempotency_key ON outbox_messages(idempotency_key);
CREATE INDEX IX_outbox_status_retry ON outbox_messages(status, next_retry_at)
    WHERE status IN ('Pending','Failed');

-- ─────────────────── NOTIFICATIONS ───────────────────
CREATE TABLE notifications (
    id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_notifications PRIMARY KEY,
    recipient_user_id   UNIQUEIDENTIFIER NOT NULL,
    channel             NVARCHAR(50)     NOT NULL,  -- Email, InApp
    template_key        NVARCHAR(100)    NOT NULL,
    subject             NVARCHAR(500)    NULL,
    body                NVARCHAR(MAX)    NULL,
    status              NVARCHAR(50)     NOT NULL DEFAULT 'Pending',  -- Pending/Sent/Failed
    sent_at             DATETIME2        NULL,
    request_id          UNIQUEIDENTIFIER NULL,  -- optional link to the triggering request
    created_at          DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at          DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_notifications_recipient ON notifications(recipient_user_id, created_at DESC);
CREATE INDEX IX_notifications_status    ON notifications(status) WHERE status = 'Pending';
```

> \[!NOTE\] `email` column stores the encrypted value. `email_hash`
> stores a deterministic SHA-256 hex digest of the lowercase plaintext
> for lookup (never stores plaintext). NFR-SEC-07: encrypt contact
> details at rest. The application layer encrypts before saving and
> decrypts after reading; the hash enables `WHERE email_hash = @hash`
> queries.

> \[!IMPORTANT\] The `UNIQUE INDEX UX_outbox_idempotency_key` is
> mandatory. Without it, concurrent API calls or processor retries can
> insert duplicate outbox events. The EF `IOutbox.EnqueueAsync`
> implementation catches `SqlException` with error 2601/2627 and
> silently returns (treating it as the duplicate-is-ignored contract).

------------------------------------------------------------------------

### E4. Identity and Roles --- ASP.NET Core integration

#### Multi-role user design

A `User` has zero or more roles through the `UserRoles` join table. JWT
token builder queries `user_roles JOIN roles` for the given user and
places all role names as a **JSON array** in the `role` claim:

    JWT claims:
      sub    : "user-guid"
      role   : ["EmergencyCoordinator","CaseManager"]   ← JSON array (multiple roles)
      orgId  : "org-guid"    (omitted if OrgId is null)
      exp    : unix timestamp

#### ASP.NET Core authorization configuration (Dev 1, `Program.cs`)

    JWT Bearer options:
      TokenValidationParameters.RoleClaimType = "role"
      MapInboundClaims = false

    ASP.NET Core reads the "role" claim.
    When the claim value is a JSON array, the middleware creates one ClaimsIdentity
    claim per array element — standard behaviour with System.IdentityModel.Tokens.Jwt.

    [Authorize(Roles = "EmergencyCoordinator,CaseManager")]
    // matches a user who has ANY of the listed roles.
    // IsInRole("EmergencyCoordinator") returns true if any role claim matches.

#### `ICurrentUser` corrected signature

    ICurrentUser:
      Guid UserId
      Guid? OrgId                           // null for Citizens with no organisation
      IReadOnlyList<string> Roles           // all role name strings from JWT
      bool IsInRole(string role)            // true if Roles contains role
      bool IsInAnyRole(params string[] roles)  // NEW — added for convenience

#### EF Core configuration for UserRole (no EntityBase)

`UserRole` does not inherit `EntityBase`. Its configuration sets the
composite primary key explicitly:

    UserRoleConfiguration:
      HasKey(ur => new { ur.UserId, ur.RoleId })
      HasOne<User>().WithMany().HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Cascade)
      HasOne<Role>().WithMany().HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Restrict)
      HasIndex(ur => ur.RoleId)

#### Seed data for roles and users (`99_seed.sql`, Dev 1)

The seed script inserts: - All 10 roles by exact name string. - One user
per role (minimum), with BCrypt-hashed password `"Demo1234!"`. - Two
additional users to demonstrate multi-role (e.g., one user with both
`EmergencyCoordinator` and `CaseManager`). - One `Organisation` of type
`"LeadAgency"` for staff users. - Citizens have `org_id = NULL`. - One
`CrisisEvent`, two `CrisisArea` rows (Dev 1 inserts only placeholder
rows; Dev 2 may extend).

------------------------------------------------------------------------

### E5. Shared DTOs --- corrected (replaces v2 E5)

Changes from v2: - `AllocateRequestDto` removes `IdempotencyKey` field -
`IsInAnyRole` added to `ICurrentUser` (reflected in DTO section as a
note) - No other DTO changes

  -----------------------------------------------------------------------------------------------------
  DTO                        Fields                              Used by
  -------------------------- ----------------------------------- --------------------------------------
  `PagedResult<T>`           `Items: T[]`, `Total: int`,         All list endpoints
                             `Page: int`, `PageSize: int`        

  `PagedQuery`               `Page: int = 1`,                    All list queries
                             `PageSize: int = 20` (server        
                             enforces max 100)                   

  `RequestSummaryDto`        Id, Category, Urgency, Status,      `IRequestReader` return
                             Description, Lat, Lng, AreaId,      
                             HouseholdSize, AccessibilityNeeds,  
                             CreatedAt                           

  `NewRecommendationDto`     Type, TargetId, PayloadJson,        `IRecommendationService.CreateAsync`
                             Confidence (0.0--1.0), Rationale,   
                             Source                              

  `MatchCandidateDto`        TargetId,                           Matcher interfaces
                             `TargetType: MatchTargetType`,      
                             Score (0.0--1.0), Rationale,        
                             IsApproximate                       

  `DecisionDto`              Id, Outcome, FinalPayloadJson,      `IDecisionGate` return
                             DecidedBy, DecidedAt                

  `DistanceResultDto`        `Km: double`,                       `IGeoService.GetDistanceAsync`
                             `Minutes: double? = null`,          
                             `IsApproximate: bool = true`        

  `GeocodeResultDto`         `Lat: double?`, `Lng: double?`,     `IGeoService.GeocodeAsync`
                             DisplayName, `IsApproximate: bool`  

  `NotificationPayloadDto`   RecipientUserId, TemplateKey,       `Notification.Send` outbox payload
                             Language,                           
                             `Data: Dictionary<string,string>`   

  `AllocateRequestDto`       `DecisionId: Guid` *(only field --- Allocation endpoint body
                             IdempotencyKey is in HTTP header)*  

  `JwtTokenDto`              AccessToken, RefreshToken,          Auth login/refresh response
                             `ExpiresAt: DateTime`               

  `UserClaimsDto`            `UserId: Guid`, `OrgId: Guid?`,     JWT builder input/output
                             `Roles: string[]`                   

  `ProblemDetailsExtended`   Standard RFC 7807 (type, title,     All error responses
                             status, detail, instance) +         
                             `code: string`                      
  -----------------------------------------------------------------------------------------------------

------------------------------------------------------------------------

### E6. Shared interfaces --- corrected (Interface definition vs implementation ownership)

> \[!IMPORTANT\] **All interfaces are defined by Dev 1** in
> `Cms.Application/Contracts/`. "Defined by" means writing the interface
> file. "Implemented by" means writing the class that provides the
> behaviour. These are different responsibilities.

  -------------------------------------------------------------------------------------------------------------------------------
  Interface                  Defined by     Implemented by                   Stub by               Consumers
  -------------------------- -------------- -------------------------------- --------------------- ------------------------------
  `ICurrentUser`             Dev 1          Dev 1 (`CurrentUserService`)     Dev 1 (in-memory fake All
                                                                             for tests)            

  `IAuditLogger`             Dev 1          Dev 1 (`AuditLogger`)            Dev 1 (no-op stub)    All

  `IFileStore`               Dev 1          Dev 1 (`LocalFileStore`)         Dev 1 (in-memory      Dev 2
                                                                             stub)                 

  `IOutbox`                  Dev 1          Dev 1 (`OutboxService`)          Dev 1 (in-memory list Dev 2, Dev 3, Dev 4
                                                                             stub)                 

  `IOutboxHandler`           Dev 1          Dev 1                            Dev 1 (no-op stub)    Dev 1 processor
                                            (`EmailNotificationHandler`) +                         
                                            Dev 3                                                  
                                            (`RequestSubmittedHandler`)                            

  `INotificationSender`      Dev 1          Dev 1 (`SmtpNotificationSender`) Dev 1 (log-only stub) Dev 1
                                                                                                   (`EmailNotificationHandler`)

  `IRequestReader`           Dev 1          **Dev 2** (`RequestReader`)      Dev 1 (returns null   Dev 3, Dev 4
                                                                             stub)                 

  `IRequestStatusService`    Dev 1          **Dev 2**                        Dev 1 (no-op stub)    Dev 2 internally, Dev 3, Dev 4
                                            (`RequestStatusService`)                               

  `IRecommendationService`   Dev 1          **Dev 3**                        Dev 1 (returns new    Dev 2 (trigger), Dev 4 (create
                                            (`RecommendationService`)        Guid stub)            match)

  `IDecisionGate`            Dev 1          **Dev 3** (`DecisionGate`)       Dev 1 (returns        Dev 2 (close critical), Dev 4
                                                                             approved stub)        (allocate)

  `IGeoService`              Dev 1          **Dev 4** (`GeoService`)         Dev 1 (Haversine-only Dev 3 (distance in ranking),
                                                                             stub,                 Dev 4
                                                                             IsApproximate=true)   

  `IResourceMatcher`         Dev 1          **Dev 4** (`ResourceMatcher`)    Dev 1 (returns empty  Dev 3
                                                                             list stub)            

  `IShelterRecommender`      Dev 1          **Dev 4** (`ShelterRecommender`) Dev 1 (returns empty  Dev 3
                                                                             list stub)            

  `IVolunteerMatcher`        Dev 1          **Dev 4** (`VolunteerMatcher`)   Dev 1 (returns empty  Dev 3
                                                                             list stub)            
  -------------------------------------------------------------------------------------------------------------------------------

**Corrected `IGeoService` contract:**

    IGeoService:
      Task<GeocodeResultDto> GeocodeAsync(string address)
          // 1. Check IMemoryCache; on hit return cached result
          // 2. On miss: call Nominatim via AddCmsHttpClient
          // 3. On Nominatim failure (timeout / circuit open): return GeocodeResultDto
          //    { Lat=null, Lng=null, DisplayName=address, IsApproximate=true }
          // 4. On success: cache 24 h; return { Lat, Lng, DisplayName, IsApproximate=false }

      Task<DistanceResultDto> GetDistanceAsync((double Lat, double Lng) from,
                                                (double Lat, double Lng) to)
          // Always Haversine formula — no external call.
          // Returns { Km=haversine(from,to), Minutes=null, IsApproximate=true }

------------------------------------------------------------------------

## F. Solution and Folder Structure

Unchanged from v2. One addition: `Notifications/` configuration folder.

    Cms.Infrastructure/
      Persistence/
        Configurations/
          Identity/
            UserConfiguration.cs            [Dev 1]
            RoleConfiguration.cs            [Dev 1]  ← NEW
            UserRoleConfiguration.cs        [Dev 1]  ← NEW
            OrganisationConfiguration.cs    [Dev 1]
            RefreshTokenConfiguration.cs    [Dev 1]
          Shared/
            AuditLogConfiguration.cs        [Dev 1]
            OutboxMessageConfiguration.cs   [Dev 1]
            NotificationConfiguration.cs    [Dev 1]  ← NEW
          Crisis/        [Dev 2]
          AI/            [Dev 3]
          Logistics/     [Dev 4]
          Shelter/       [Dev 4]
          Volunteer/     [Dev 4]

All other folders are unchanged from v2 Section F.

------------------------------------------------------------------------

## G. Corrected Implementation Sequence

### G1. Correction to v2 Section G1 (D1/D2 items)

  -------------------------------------------------------------------------------------------------------------------
  Item                                                  v2 Assignment           v3 Correction
  ----------------------------------------------------- ----------------------- -------------------------------------
  D1-2                                                  Foundation Day 1        **Corrected:**
  `AllocateRequestDto { DecisionId, IdempotencyKey }`                           `AllocateRequestDto { DecisionId }`
                                                                                only; `IdempotencyKey` removed from
                                                                                body

  D3-5 Multi-role JWT: `role` claim as JSON array       Foundation Day 2        **Clarified:** JWT builder joins
                                                                                `user_roles` → `roles` table, adds
                                                                                one `role` claim per row;
                                                                                System.IdentityModel.Tokens.Jwt
                                                                                deserialises array automatically

  Nominatim for `GetDistanceAsync`                      Foundation Day 1        **Corrected:** interface doc comment
                                                        (interface definition)  says "Haversine only"; no Nominatim
                                                                                call in distance
  -------------------------------------------------------------------------------------------------------------------

### G2. Revised sprint plan

``` mermaid
flowchart TD
    A["Day 1 (all devs unblocked by EOD)\n──────────────────────\nDev 1: Cms.sln · global.json · EntityBase · Enums\n       Exceptions · Contracts/ (stubs) · DTOs\n       Foundation entities (User,Role,UserRole,Org,\n       RefreshToken,AuditLog,OutboxMessage,Notification)\n       CmsDbContext skeleton · Program.cs placeholder\n       00_foundation.sql · 98_foreign_keys.sql stub\n       ALLOCATION.md\nAll: Clone repo, run 00_foundation.sql, dotnet build ✓"] --> B

    B["Days 2–3 (auth + infra; unblocks Dev 2 register)\n──────────────────────\nDev 1: BCrypt · JwtService · CurrentUserService\n       Auth endpoints (register/login/refresh/logout)\n       JWT RoleClaimType config · GlobalExceptionHandler\n       Swagger Bearer · JsonStringEnumConverter\n       CORS · RateLimit · UTC converter · EF configs\n       AddCmsHttpClient Polly factory\n       web/lib/api.ts · In-memory stubs (all interfaces)\nAll others: Begin domain entities + SQL scripts"] --> C

    B --> D1["Dev 2 (Day 2+)\n──────────────────────\nCrisisEvent · CrisisArea · Request entities\n01_crisis.sql\nIRequestReader impl\nIRequestStatusService (state machine)\nRequest CRUD endpoints"]
    B --> D2["Dev 3 (Day 2+)\n──────────────────────\nRecommendation · Decision · Announcement\n02_ai.sql\nRule-based classifier\nIRecommendationService impl\nRequest.Submitted outbox handler"]
    B --> D3["Dev 4 (Day 2+)\n──────────────────────\nInventory · Allocation · Shelter entities\nVolunteerProfile · Task entities\n03/04/05 SQL scripts\nGeoService (Nominatim + Haversine)\nMatcher implementations"]

    C["Days 4–6 (Dev 1 non-blocking features)\n──────────────────────\nDev 1: IOutbox real impl · Outbox processor\n       IAuditLogger · Notification handler\n       IFileStore + upload endpoint\n       Admin endpoints · Dead-letter view\n       Notification poll endpoint\n       99_seed.sql · KPI endpoint skeleton"]
    D1 --> E
    D2 --> E
    D3 --> E
    C --> E

    E["CP-1: Day 6 EOD\n──────────────────────\nAll compile against real implementations\nSubmit request → Outbox enqueued\nRequest.Submitted handler fires\nClassification + Priority recommendations created\nTest via Postman"]

    E --> F["Days 7–9 (integration + decision gate)\n──────────────────────\nDev 2: Verify/Triage transitions · merge/escalate\n       Timeline endpoint\nDev 3: IDecisionGate impl · Decision endpoints\n       POST /api/v1/recommendations (match trigger)\nDev 4: POST /api/v1/allocations (header + gate + tx)\n       Shelter allocation · Volunteer task assign\nDev 1: 98_foreign_keys.sql (cross-module FKs)\n       KPI real queries"]

    F --> G2["CP-2: Day 9 EOD\n──────────────────────\nFull Postman chain: Submit → AI → Verify\n→ Match → Approve → Allocate → Dispatch\n→ Fulfilled → Close\nAll UAT 7 steps pass in backend"]

    G2 --> H["Days 10–12: Frontend\n──────────────────────\nDev 1: Auth pages · Admin panel · layout/nav\nDev 2: Citizen portal · Coord dashboard · forms\nDev 3: Recommendation list · Decision form\nDev 4: Inventory · Shelter · Volunteer pages\n       Map.tsx (Leaflet + OpenStreetMap)"]

    H --> I["Days 13–14: Integration testing + fixes\n──────────────────────\nAll: End-to-end UAT 7 scenarios\nDev 1: Email via SMTP MailKit · Dead-letter admin\nDev 3: Anomaly flag · Announcement approval gate\nDev 4: Shortage engine demo"]

    I --> J["Days 15–17: Report + deliverables\n──────────────────────\nAll: PDF report · Diagrams (Use Case,Class,Sequence,Component)\n     Demo video · README · Final ZIP"]
```

### G3. Checkpoint protocol --- unchanged from v2

  ------------------------------------------------------------------------
  Checkpoint              Day                     Verification
  ----------------------- ----------------------- ------------------------
  CP-0                    Day 1 EOD               `dotnet build Cms.sln`
                                                  passes for all 4 devs

  CP-0.5                  Day 3 EOD               Auth endpoints work; JWT
                                                  roles claim is an array;
                                                  Swagger shows Bearer

  CP-1                    Day 6 EOD               Submit → classify →
                                                  recommendations created
                                                  via Postman

  CP-2                    Day 9 EOD               Full backend UAT chain
                                                  passes without manual DB
                                                  edits

  CP-3                    Day 14 EOD              Full end-to-end
                                                  including frontend; all
                                                  7 UAT steps

  CP-4                    Day 16 EOD              Report draft reviewed;
                                                  diagrams complete
  ------------------------------------------------------------------------

### G4. UAT 7-step mapping --- unchanged from v2

------------------------------------------------------------------------

## H. Final Critical-Issues Checklist

The following items must be resolved or confirmed before the team begins
scaffolding.

### H1. Blockers --- do not start until resolved

  ---------------------------------------------------------------------------
  \#                Item                  Status            Responsible
  ----------------- --------------------- ----------------- -----------------
  H1-1              Team agrees           ⬜ Pending        All 4
                    module-to-developer                     
                    allocation and                          
                    commits                                 
                    `ALLOCATION.md`                         

  H1-2              Dev 1 confirms they   ⬜ Pending        Dev 1
                    can complete Phase 1                    
                    (C1 items) on Day 1                     

  H1-3              All devs install .NET ⬜ Pending        All 4
                    10.0.400 SDK and                        
                    confirm                                 
                    `dotnet --version` =                    
                    `10.0.400`                              

  H1-4              SQL Server instance   ⬜ Pending        All 4
                    confirmed available                     
                    for all 4 devs (local                   
                    or shared)                              

  H1-5              SMTP dev inbox        ⬜ Pending        Dev 1
                    configured (e.g.,                       
                    Mailhog, Mailtrap, or                   
                    Gmail app password)                     
                    and connection string                   
                    shared                                  

  H1-6              OpenAI API key (or    ⬜ Pending        Dev 3
                    equivalent) confirmed                   
                    --- if no key, Dev 3                    
                    starts with                             
                    rule-based only                         
  ---------------------------------------------------------------------------

### H2. Contract items --- confirm before Day 2

  ---------------------------------------------------------------------------------------------------
  \#                Item                                          Status            Action
  ----------------- --------------------------------------------- ----------------- -----------------
  H2-1              `IGeoService.GetDistanceAsync` is             ⬜ Pending        Confirm and
                    Haversine-only, `Minutes = null` --- all devs                   document in
                    accept                                                          report

  H2-2              `Idempotency-Key` is an HTTP **header**, not  ⬜ Pending        Update any
                    a body field --- Dev 4 and all reviewers                        existing notes
                    accept                                                          

  H2-3              `OutboxMessage.Status` ≠                      ⬜ Pending        No code conflict;
                    `Notification.Status` --- both needed; Dev 1                    confirm
                    creates both tables                                             

  H2-4              `UserRole` has no `EntityBase` / no surrogate ⬜ Pending        Dev 1 confirms EF
                    Guid PK --- composite PK `(UserId, RoleId)`                     config

  H2-5              `email` column is encrypted; `email_hash`     ⬜ Pending        Agree on AES-256
                    enables lookup --- Dev 1 owns encryption                        or column-level
                    key/strategy                                                    encryption

  H2-6              `AuditLog` has no `UpdatedAt` column ---      ⬜ Pending        Dev 1 sets
                    append-only; EF config must prevent updates                     `IsReadOnly` in
                                                                                    config

  H2-7              `ICurrentUser.IsInAnyRole(params string[])`   ⬜ Pending        Confirm interface
                    added --- all devs may use this                                 addition
  ---------------------------------------------------------------------------------------------------

### H3. Scope guard --- confirm P2/P3 exclusion

  -----------------------------------------------------------------------
  \#                      Item                    Status
  ----------------------- ----------------------- -----------------------
  H3-1                    Donor module (FR-DON,   ⬜ Pending
                          P2) deferred to Day 10+ 
                          at earliest             

  H3-2                    Partner workspace       ⬜ Pending
                          (FR-PTN, P2) deferred;  
                          no code until CP-2      

  H3-3                    OSRM routing            ⬜ Pending
                          integration is not in   
                          P1 scope                

  H3-4                    SMS/Push notifications  ⬜ Pending
                          (P3) are not built      

  H3-5                    Payment sandbox (P3) is ⬜ Pending
                          not built               
  -----------------------------------------------------------------------

------------------------------------------------------------------------

## I. Go / No-Go for Initial Scaffolding

> \[!IMPORTANT\] **Recommended decision: CONDITIONAL GO**

The contract is technically sound and consistent. All 7 raised issues
are resolved. The solution may proceed to scaffolding **once** H1-1
through H1-4 are confirmed (allocation agreed, .NET 10 installed, SQL
Server available). H1-5 and H1-6 are not Day 1 blockers --- Dev 1 can
stub notifications and Dev 3 can use rule-based only until those are
resolved.

**Suggested first commit on `develop` by Dev 1:** 1. `global.json` 2.
`Cms.sln` (empty projects) 3. `ALLOCATION.md` 4. `db/00_foundation.sql`
5. `db/98_foreign_keys.sql` (stub)

All four developers then branch `feature/dev{N}-foundation` from
`develop` and begin Phase 1 / Phase 2 work in parallel.
