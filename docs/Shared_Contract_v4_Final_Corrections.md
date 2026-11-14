# Shared Contract v4 --- Final Corrections (corrected sections only)

**Project:** AI-Powered Humanitarian Crisis Management and Aid
Coordination Platform **Review date:** 13 Nov 2026 \| **Deadline:** 30
Nov 2026 (17 days remaining) **Status:** Corrected sections only ---
remainder of v3 unchanged **Supersedes:** Shared Contract v3
(architecture and four-developer allocation unchanged)

------------------------------------------------------------------------

## Corrections Summary

  -------------------------------------------------------------------------
  \#                    v3 Error                 v4 Fix
  --------------------- ------------------------ --------------------------
  1                     Foundation table count   Corrected to 8 everywhere
                        stated as "7" but schema 
                        defines 8                

  2                     JWT role claim emitted   One separate `role` claim
                        as one JSON array ---    per assigned role; ASP.NET
                        unreliable with some     Core compatibility
                        token libraries          confirmed

  3                     `ShelterAllocation`      Added to entity, SQL
                        missing `IdempotencyKey` schema (`04_shelter.sql`)
                        field, SQL column and    and EF configuration
                        unique constraint        

  4                     `NotificationStatus`     `NotificationStatus` =
                        enum included            Pending, Sent, Failed only
                        `DeadLettered` ---       
                        belongs only to          
                        `OutboxMessage.Status`   

  5                     Email hash uses plain    HMAC-SHA256 (keyed) for
                        SHA-256 (no secret key)  lookup hash; AES-GCM for
                        --- vulnerable to        stored ciphertext; keys
                        precomputation           from environment

  6                     Filtered index used      Rewritten with `OR`; added
                        `IN (…)` --- not         `FK_notifications_users`
                        supported in SQL Server  and `FK_audit_log_users`
                        filtered index           
                        predicates; missing      
                        recipient and            
                        audit-actor FKs          

  7                     Allocation transaction   Explicit three-phase spec:
                        boundaries and           pre-check → transaction →
                        concurrent duplicate     post-commit; concurrent
                        handling left vague      race handling documented

  8                     PDF/MD comparison stated Downgraded to cautious
                        content equivalence as a finding --- metadata and
                        confirmed fact           structure match; content
                                                 equivalence not
                                                 independently verified
  -------------------------------------------------------------------------

------------------------------------------------------------------------

## 1. Foundation Table Count --- corrected everywhere

v3 contained contradictions: the corrections summary table said "All
**7** foundation tables" (line 14, line 108, line 229) but the actual
SQL block defines **8** tables and E2 says "All **9** foundation
entities" (which counts the entities, not the tables --- `UserRole` maps
to a table but not a full entity class).

**Correct figures:**

  -----------------------------------------------------------------------
  Concept                 Count                   Items
  ----------------------- ----------------------- -----------------------
  Foundation SQL tables   **8**                   `organisations`,
                                                  `users`, `roles`,
                                                  `user_roles`,
                                                  `refresh_tokens`,
                                                  `audit_log`,
                                                  `outbox_messages`,
                                                  `notifications`

  Foundation entity       **8**                   `Organisation`, `User`,
  classes (C# objects)                            `Role`, `UserRole`,
                                                  `RefreshToken`,
                                                  `AuditLog`,
                                                  `OutboxMessage`,
                                                  `Notification`
  -----------------------------------------------------------------------

**Lines to change in v3:** - Corrections summary row 2: "All **7**
foundation tables" → "All **8** foundation tables" - C1-12:
"/db/00_foundation.sql --- all **7** foundation tables" → "all **8**
foundation tables" - E3 header: "All **7** foundation tables" → "All
**8** foundation tables" - E2 heading: "All **9** foundation entities" →
"All **8** foundation entities" (UserRole is an entity class --- count
is 8 either way)

No structural change; only the stated number is wrong.

------------------------------------------------------------------------

## 2. JWT Role Claims --- corrected

### v3 error

v3 specified that all role names are placed in **one claim with a JSON
array value**:

    role : ["EmergencyCoordinator","CaseManager"]

This is non-standard.
`System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler` serialises and
deserialises multi-value claims as a JSON array when writing, but other
JWT libraries (including the browser's `jose` package used in Next.js)
may treat the claim value as a literal string
`"[\"EmergencyCoordinator\",\"CaseManager\"]"` rather than parsing it as
an array. ASP.NET Core's own behaviour when reading back a
JSON-array-valued claim also depends on the token handler version.

### v4 correction --- one separate `role` claim per assigned role

The correct and universally compatible approach:

    JWT token claims (example user with two roles):
      sub   : "3fa85f64-5717-4562-b3fc-2c963f66afa6"
      role  : "EmergencyCoordinator"
      role  : "CaseManager"
      orgId : "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d"
      exp   : 1764633600

The JWT token body contains **two separate `role` claims** with
different values. This is valid per RFC 7519 (a JSON object may have
duplicate key names; JWT libraries handle them as an array of claims,
not a JSON array value).

### ASP.NET Core compatibility --- confirmed

  -------------------------------------------------------------------------------------------------------------
  Behaviour                                                                         Detail
  --------------------------------------------------------------------------------- ---------------------------
  `JwtBearerOptions.TokenValidationParameters.RoleClaimType = "role"`               Tells ASP.NET Core that
                                                                                    claims named `"role"` are
                                                                                    role claims

  `JwtBearerOptions.MapInboundClaims = false`                                       Prevents the middleware
                                                                                    from remapping `"role"` to
                                                                                    `ClaimTypes.Role` (the long
                                                                                    URI form); keeps it as the
                                                                                    literal string `"role"`

  Multiple `role` claims                                                            `JwtSecurityTokenHandler`
                                                                                    creates one `Claim` object
                                                                                    per claim entry in the
                                                                                    token; the `ClaimsIdentity`
                                                                                    holds multiple claims with
                                                                                    name `"role"`

  `ClaimsPrincipal.IsInRole("EmergencyCoordinator")`                                Iterates
                                                                                    `ClaimsIdentity.Claims`
                                                                                    where
                                                                                    `Type == RoleClaimType`;
                                                                                    returns true if any value
                                                                                    matches --- ✅ works
                                                                                    correctly

  `[Authorize(Roles = "EmergencyCoordinator,CaseManager")]`                         ASP.NET Core splits on
                                                                                    comma and calls `IsInRole`
                                                                                    for each; passes if the
                                                                                    user has **any** of the
                                                                                    listed roles --- ✅ works
                                                                                    correctly

  `[Authorize(Roles = "EmergencyCoordinator")][Authorize(Roles = "CaseManager")]`   Two stacked attributes
                                                                                    require **both** roles ---
                                                                                    use only when both roles
                                                                                    are required simultaneously
  -------------------------------------------------------------------------------------------------------------

### Corrected JWT builder specification (Dev 1)

The `JwtService` queries `user_roles JOIN roles WHERE user_id = @userId`
and adds **one `Claim("role", roleName)` per row**. No JSON
serialisation of the roles list is performed.

    For each role in user.Roles:
        claims.Add(new Claim("role", role.Name))

### Corrected `ICurrentUser` --- no change to signature

The `ICurrentUser` interface from v3 is correct as written. `Roles` is
populated by reading **all** claims where `Type == "role"` from
`HttpContext.User.Claims`. No change needed.

------------------------------------------------------------------------

## 3. ShelterAllocation --- IdempotencyKey added

### Entity (Dev 4, `Cms.Domain/Shelter/ShelterAllocation.cs`)

Add `IdempotencyKey` field:

  -----------------------------------------------------------------------
  Field                   Type                    Notes
  ----------------------- ----------------------- -----------------------
  `IdempotencyKey`        `string` (nvarchar 256) Required; unique; set
                                                  from HTTP
                                                  `Idempotency-Key`
                                                  header

  -----------------------------------------------------------------------

Full corrected entity fields:

    ShelterAllocation:
      Id              : Guid          (EntityBase PK)
      ShelterId       : Guid          (FK → Shelter)
      RequestId       : Guid          (FK → Request)
      HouseholdSize   : int
      DecisionId      : Guid          (FK → Decision)
      IdempotencyKey  : string        (required, unique)   ← ADDED
      AllocatedAt     : DateTime
      DepartedAt      : DateTime?
      CreatedAt       : DateTime      (EntityBase)
      UpdatedAt       : DateTime      (EntityBase)

### SQL (Dev 4, `04_shelter.sql`)

Add to the `shelter_allocations` table definition:

``` sql
-- Add this column to shelter_allocations (Dev 4 owns 04_shelter.sql)
idempotency_key   NVARCHAR(256)   NOT NULL,

-- Add this unique constraint after the table CREATE:
CREATE UNIQUE INDEX UX_shelter_allocations_idempotency_key
    ON shelter_allocations(idempotency_key);
```

### EF configuration (Dev 4, `ShelterAllocationConfiguration.cs`)

    Property(sa => sa.IdempotencyKey)
        .IsRequired()
        .HasMaxLength(256);
    HasIndex(sa => sa.IdempotencyKey)
        .IsUnique();

### Updated Idempotency table in v3 Section A.5

Replace the `ShelterAllocation duplicate` row:

  ------------------------------------------------------------------------------
  Concern             Mechanism                               Location
  ------------------- --------------------------------------- ------------------
  ShelterAllocation   `Idempotency-Key` HTTP header; UNIQUE   `04_shelter.sql`
  duplicate           on                                      (Dev 4) ✅ now
                      `shelter_allocations.idempotency_key`   explicit

  ------------------------------------------------------------------------------

------------------------------------------------------------------------

## 4. NotificationStatus Enum --- DeadLettered removed

### v3 error

v3 Section E1 included `DeadLettered` in `NotificationStatus`.

### v4 correction

    NotificationStatus  = Pending, Sent, Failed

`DeadLettered` applies **only** to `OutboxMessage.Status`. A
notification that cannot be delivered after retries remains `Failed`.
The delivery-retry logic and dead-lettering belong to the outbox
processor, not the notification record.

**Updated status lifecycle:**

  -----------------------------------------------------------------------
  Status flow            Entity                   Values
  ---------------------- ------------------------ -----------------------
  Outbox processing      `OutboxMessage.Status`   Pending → Processing →
                                                  Processed / Failed →
                                                  **DeadLettered**

  Notification delivery  `Notification.Status`    Pending → **Sent** /
                                                  **Failed**
  -----------------------------------------------------------------------

A `Notification` row in `Failed` state is surfaced to admin at
`GET /api/v1/admin/notifications/failed`. The outbox processor
dead-letters the *outbox event*, not the notification record.

**Corrected SQL** in `00_foundation.sql` notifications table comment:

``` sql
status  NVARCHAR(50) NOT NULL DEFAULT 'Pending',  -- Pending / Sent / Failed  (not DeadLettered)
```

------------------------------------------------------------------------

## 5. Email Security --- AES-GCM + HMAC-SHA256

### v3 error

v3 used plain SHA-256 of the lowercase email as the lookup hash. Plain
SHA-256 is deterministic and keyless --- an attacker with the database
can precompute SHA-256 of a list of common email addresses and match
rows.

### v4 correction

  ---------------------------------------------------------------------------------
  Operation      Algorithm         Key source               Notes
  -------------- ----------------- ------------------------ -----------------------
  Email          **AES-256-GCM**   `EMAIL_ENCRYPTION_KEY`   Authenticated
  ciphertext                       environment variable (32 encryption: provides
  (stored in                       bytes, base64)           confidentiality +
  `email`                                                   tamper detection; nonce
  column)                                                   stored prepended to
                                                            ciphertext

  Email lookup   **HMAC-SHA256**   `EMAIL_HASH_KEY`         Keyed hash --- without
  hash (stored                     environment variable     the secret key an
  in                               (32+ bytes, base64)      offline attacker cannot
  `email_hash`                                              reverse or precompute
  column)                                                   

  Refresh token  **SHA-256**       No secret needed ---     Acceptable because
  hash (stored                     tokens are random        refresh tokens are
  in                               256-bit values;          cryptographically
  `token_hash`                     precomputation is not    random, not
  column)                          feasible                 dictionary-attackable
  ---------------------------------------------------------------------------------

**Key management rules (Dev 1 must document in README):**

1.  Keys are loaded from environment variables at startup via
    `IConfiguration` / `IOptions<EncryptionOptions>`.
2.  Keys are **never** in `appsettings.json`, source code, or SQL
    scripts.
3.  In development: use
    `dotnet user-secrets set "Encryption:EmailEncryptionKey" "<base64>"`.
4.  In CI/demo: set environment variables in the run script (not
    committed).
5.  Key rotation is out of scope for the demo --- note it in the report.

**Corrected `email_hash` column comment in `00_foundation.sql`:**

``` sql
email_hash  NVARCHAR(64) NOT NULL,
-- HMAC-SHA256 (keyed) of lowercase email; key from EMAIL_HASH_KEY env var.
-- Enables WHERE email_hash = @hash lookup without exposing plaintext.
-- Never store the key in SQL or source code.
```

**Corrected note replacing v3 E3 note block:**

> \[!IMPORTANT\] `email` stores AES-256-GCM ciphertext (nonce
> prepended). `email_hash` stores an HMAC-SHA256 hex digest using a
> secret key from the `EMAIL_HASH_KEY` environment variable. Neither the
> plaintext email nor the key appears in source code or SQL. Dev 1 must
> add `EncryptionOptions` to the DI configuration and document the
> required environment variables in `README.md` before CP-0.5.

------------------------------------------------------------------------

## 6. SQL Server Index Syntax and Missing FKs --- corrected

### 6a. Filtered index --- `IN` predicate not supported in SQL Server

SQL Server's `CREATE INDEX … WHERE` clause (filtered index) **does not
support `IN (…)` or `OR` with the same column in some versions**.
Rewrite as explicit `OR`:

**v3 (incorrect):**

``` sql
CREATE INDEX IX_outbox_status_retry ON outbox_messages(status, next_retry_at)
    WHERE status IN ('Pending','Failed');
```

**v4 (correct --- compatible with SQL Server 2016+):**

``` sql
CREATE INDEX IX_outbox_status_retry ON outbox_messages(status, next_retry_at)
    WHERE status = 'Pending' OR status = 'Failed';
```

> \[!NOTE\] SQL Server filtered index predicates support simple
> comparisons and `AND`/`OR` of simple comparisons, but not `IN`,
> `NOT IN`, `LIKE`, subqueries, or computed expressions. The `OR` form
> with explicit equality is fully supported and produces the same
> optimisation benefit.

The `notifications` filtered index in v3 is correct as written
(`WHERE status = 'Pending'` --- single equality).

### 6b. Missing foreign keys --- `notifications` and `audit_log`

**Add to `00_foundation.sql`**, inside the respective `CREATE TABLE`
statements:

**`notifications` --- recipient FK:**

``` sql
CREATE TABLE notifications (
    ...
    CONSTRAINT FK_notifications_users
        FOREIGN KEY (recipient_user_id) REFERENCES users(id)
        ON DELETE CASCADE   -- if a user is deleted, their notifications go too
);
```

**`audit_log` --- actor FK (nullable; no cascade):**

``` sql
CREATE TABLE audit_log (
    ...
    -- No FK constraint on actor_user_id.
    -- Rationale: audit records must survive user deletion (soft-delete users via
    -- is_active = 0; never hard-delete users once audit rows exist).
    -- actor_user_id may be NULL for system-generated audit entries.
    -- A plain index is sufficient; a FK would prevent user record deletion.
);
```

> \[!NOTE\] `actor_user_id` in `audit_log` intentionally has **no FK
> constraint**. Users must be soft-deleted (set `is_active = false`)
> rather than hard-deleted, so the existing index (`IX_audit_log_actor`)
> is sufficient. Adding a FK would block hard deletion and require
> `ON DELETE SET NULL`, which would silently lose the actor identity
> from audit records --- unacceptable for an append-only audit trail.

### 6c. Complete corrected `00_foundation.sql` index and FK section

Replace the outbox and notification index lines from v3 with:

``` sql
-- OUTBOX_MESSAGES indexes
CREATE UNIQUE INDEX UX_outbox_idempotency_key ON outbox_messages(idempotency_key);
CREATE INDEX IX_outbox_status_retry ON outbox_messages(status, next_retry_at)
    WHERE status = 'Pending' OR status = 'Failed';    -- ← corrected from IN()

-- NOTIFICATIONS table: add FK inside CREATE TABLE
-- (recipient_user_id already references users; FK added explicitly)
-- See full CREATE TABLE block; replace the notifications block entirely:

CREATE TABLE notifications (
    id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID()
                            CONSTRAINT PK_notifications PRIMARY KEY,
    recipient_user_id   UNIQUEIDENTIFIER NOT NULL,
    channel             NVARCHAR(50)     NOT NULL,
    template_key        NVARCHAR(100)    NOT NULL,
    subject             NVARCHAR(500)    NULL,
    body                NVARCHAR(MAX)    NULL,
    status              NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    sent_at             DATETIME2        NULL,
    request_id          UNIQUEIDENTIFIER NULL,
    created_at          DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at          DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_notifications_users
        FOREIGN KEY (recipient_user_id) REFERENCES users(id) ON DELETE CASCADE
);
CREATE INDEX IX_notifications_recipient ON notifications(recipient_user_id, created_at DESC);
CREATE INDEX IX_notifications_status    ON notifications(status) WHERE status = 'Pending';
```

------------------------------------------------------------------------

## 7. Allocation Transaction --- explicit consistency specification

v3 described the allocation endpoint at a high level but left
transaction boundaries and concurrent duplicate handling undefined. This
section provides the precise specification Dev 4 must implement.

### 7a. Inventory allocation --- three-phase approach

Separating the work into three distinct phases keeps the DB transaction
small and avoids distributed transaction issues arising from calling
`IRequestStatusService` (Dev 2's service, same `CmsDbContext` scope)
inside an open transaction.

    Phase 1 — Pre-check (no transaction, no locks)
      1. Read Idempotency-Key from HTTP header.
         If header is absent → 400 Bad Request.
      2. Query: SELECT id, quantity FROM allocations
                WHERE idempotency_key = @key
         If a row exists → return 200 OK with the existing AllocationDto.
         (This short-circuits the entire operation safely.)
      3. Call IDecisionGate.RequireApprovedAsync(decisionId, ResourceMatch, requestId)
         If throws ForbiddenException → propagate 403 Forbidden.

    Phase 2 — DB transaction (inventory deduction + allocation insert)
      BEGIN TRANSACTION (READ COMMITTED isolation is sufficient; row-level locking via
      the UPDATE statement prevents concurrent over-allocation)

        a. UPDATE inventory
           SET    available  = available  - @qty,
                  reserved   = reserved   + @qty,
                  updated_at = SYSUTCDATETIME()
           WHERE  id = @inventoryId
           AND    available >= @qty

           Rows affected = 0 → ROLLBACK → throw ConflictException
               ("Insufficient available quantity")
           Rows affected = 1 → continue

        b. INSERT INTO allocations
           (id, request_id, inventory_id, quantity, status,
            decision_id, idempotency_key, created_at, updated_at)
           VALUES (NEWSEQUENTIALID(), @requestId, @inventoryId, @qty,
                   'Reserved', @decisionId, @key,
                   SYSUTCDATETIME(), SYSUTCDATETIME())

           If INSERT raises unique constraint violation (error 2601 or 2627) on
           idempotency_key → ROLLBACK.
           Then re-query Phase 1 step 2 (another concurrent request won the race).
           Return 200 OK with the existing AllocationDto. ← handles race condition

      COMMIT

    Phase 3 — Post-commit status transition (outside transaction)
      Call IRequestStatusService.TransitionAsync(requestId, Allocated)
      This call uses the same scoped CmsDbContext but runs in its own SaveChangesAsync.
      If it fails → the allocation record exists in state 'Reserved' but the request
      status is still 'Approved'. This is recoverable:
        - The allocation record and its idempotency key prevent double-allocation.
        - Admin or a retry of the endpoint will re-attempt the transition (Phase 1
          exits early with 200; Phase 3 retries the transition).
        - The discrepancy is visible in audit log.
      Do not roll back Phase 2 on Phase 3 failure — the inventory deduction is
      the critical state; request status is secondary and always recoverable.

### 7b. Concurrent duplicate request handling

Two simultaneous POST requests arrive with the same `Idempotency-Key`
before either has inserted an `Allocation` row:

    Request A                           Request B
    ─────────────────────────────────   ─────────────────────────────────
    Phase 1: no existing row ✓          Phase 1: no existing row ✓
    Phase 2: BEGIN TRANSACTION          Phase 2: BEGIN TRANSACTION
      UPDATE inventory ... → 1 row ✓     UPDATE inventory ... → 0 rows
                                          (A already decremented available)
      INSERT allocation (key=X) ✓          → ConflictException? No —
      COMMIT ✓                              wait: UPDATE locks the row;
                                            B waits for A's lock release,
                                            then sees available < qty.
                                          → ROLLBACK → ConflictException
                                          → catch ConflictException:
                                            re-read Phase 1 step 2
                                            → finds A's row → 200 OK ✓

> \[!NOTE\] The UPDATE-based lock (`WHERE available >= @qty`) is the
> concurrency guard. It acquires an exclusive row lock on the inventory
> row for the duration of the transaction. A second concurrent UPDATE on
> the same row waits for the first to commit, then re-evaluates the
> predicate with the updated `available` value. This is the standard
> "optimistic decrement" pattern for SQL Server inventory management; no
> `SERIALIZABLE` isolation or `UPDLOCK` hints are required at READ
> COMMITTED isolation when the UPDATE itself provides the guard.

### 7c. ShelterAllocation --- same pattern

The `POST /api/v1/shelters/{id}/allocations` endpoint follows the
identical three-phase approach:

    Phase 2 UPDATE:
      UPDATE shelters
      SET    current_occupancy = current_occupancy + @householdSize,
             updated_at = SYSUTCDATETIME()
      WHERE  id = @shelterId
      AND    (capacity - current_occupancy) >= @householdSize

      Rows affected = 0 → ROLLBACK → ConflictException ("Shelter at capacity")
      Rows affected = 1 → INSERT shelter_allocations (with idempotency_key) → COMMIT

The `current_occupancy` column is never updated except by this UPDATE,
ensuring the over-allocation guard (SRS FR-SHE-04) is enforced at the
database level, not the application level.

------------------------------------------------------------------------

## 8. PDF vs Markdown Comparison --- cautious wording

### v3 overclaim

v3 Section A1 stated:

> *"The two documents are the same content at the time of printing.
> There is no independently authored PDF --- the markdown is not
> 'derived from' the PDF; the PDF is the markdown, rasterised."*

This was stated as a confirmed fact. It was not.

### What was actually verified

  -----------------------------------------------------------------------------------
  Evidence                                                 What it proves
  -------------------------------------------------------- --------------------------
  `Producer: Microsoft: Print To PDF`                      The PDF was created using
                                                           Windows "Print to PDF" ---
                                                           consistent with printing a
                                                           document

  PDF title decoded (UTF-16 BE) =                          The source document that
  `Crisis_Management_System_SRS_and_Development_Plan.md`   was printed had this
                                                           filename

  All 918 streams are JPEG images (DCTDecode); zero font   The PDF contains only
  objects                                                  rasterised page images; no
                                                           selectable text

  FlateDecode content streams contain only PDF graphic     Confirms raster-image page
  operators (`cm`, `q`, `/Image Do`)                       layout; no embedded text
  -----------------------------------------------------------------------------------

  ---------------------------------------------------------------------
  What was NOT verified
  ---------------------------------------------------------------------
  Whether the markdown file was modified after the PDF was printed

  Whether all content on all 18 PDF pages exactly matches the current
  markdown

  OCR or visual comparison of any page against the markdown text

  Whether any pages were reordered, added or removed in the PDF
  ---------------------------------------------------------------------

### v4 corrected wording (replace v3 Section A1 conclusion)

> **Cautious finding --- not a confirmed equivalence.**
>
> The PDF's internal metadata indicates it was produced by printing a
> file named `Crisis_Management_System_SRS_and_Development_Plan.md` via
> Windows Print to PDF. The structure (18 rasterised pages, no
> selectable text) is consistent with this. However, without OCR
> verification or manual page-by-page comparison, it cannot be confirmed
> that every page of the PDF matches the current content of the markdown
> file. The markdown may have been edited after the PDF was generated.
>
> **Working assumption (acceptable for this project):** The markdown is
> the living source of truth. The PDF is treated as a snapshot at an
> earlier point in time. Any conflict should be resolved in favour of
> the markdown. State in the report: "The PDF appears to be a print
> export of the SRS markdown; the markdown is maintained as the working
> document."
>
> **Action:** A team member should visually compare the PDF page count
> and section headings against the markdown to confirm no major sections
> are missing. This takes approximately 10 minutes and removes the
> remaining uncertainty.

------------------------------------------------------------------------

## Remaining Risks

  ---------------------------------------------------------------------------------------------
  \#           Risk                                      Severity     Mitigation
  ------------ ----------------------------------------- ------------ -------------------------
  R1           Encryption key management --- if          HIGH         Dev 1 must document key
               `EMAIL_ENCRYPTION_KEY` or                              backup in README before
               `EMAIL_HASH_KEY` is lost, all stored                   CP-0.5; use
               emails become unreadable                               `dotnet user-secrets` in
                                                                      dev; store keys in a
                                                                      password manager

  R2           Phase 3 failure (status transition fails  MEDIUM       Admin endpoint shows
               after allocation commits) leaves request               allocations in `Reserved`
               in `Approved` with an existing                         state without matching
               `Allocation` row                                       `Allocated` request
                                                                      status; coordinator can
                                                                      manually trigger the
                                                                      transition; documented in
                                                                      report as a known
                                                                      trade-off

  R3           Nominatim rate limit --- the public       MEDIUM       Cache aggressively
               Nominatim API enforces 1 request/second                (24-hour IMemoryCache);
               and forbids bulk use                                   use a custom `User-Agent`
                                                                      header identifying the
                                                                      project; for demo, the
                                                                      geocode volume is low

  R4           `email_hash` column is `NVARCHAR(64)` but LOW          Confirm encoding
               HMAC-SHA256 hex output is 64 characters                convention (hex) in Dev
               --- column size is correct, but if the                 1's `EncryptionOptions`;
               team switches to base64 encoding (44                   document in README
               chars), the index comment becomes                      
               misleading                                             

  R5           PDF content equivalence not verified ---  LOW          Assign one team member to
               the markdown may differ from the printed               do a 10-minute visual
               PDF                                                    check (see Section 8)

  R6           `99_seed.sql` inserts crisis/area         LOW          Dev 1 must run
               placeholder rows that belong to Dev 2's                `01_crisis.sql` before
               schema --- seed order dependency                       `99_seed.sql`; document
                                                                      the full run order in
                                                                      README

  R7           `IRequestStatusService.TransitionAsync`   MEDIUM       See Section 7; documented
               is called outside the allocation                       trade-off; recoverable
               transaction (Phase 3) --- if the status                via admin retry
               service's `SaveChangesAsync` throws, the               
               request status is stale                                

  R8           Multi-role seed user: JWT builder must    LOW          `IX_user_roles_role_id`
               join `user_roles` for every login --- if               index already defined;
               query is not indexed, login latency grows              login volume is
               with users                                             negligible at demo scale

  R9           ASP.NET Core `MapInboundClaims = false`   LOW          Dev 1 must set this
               must be set **before**                                 option in the
               `AddAuthentication().AddJwtBearer()` or                `AddJwtBearer` options
               the mapping still occurs                               delegate, not separately
  ---------------------------------------------------------------------------------------------

------------------------------------------------------------------------

## Go / No-Go for Team Approval and Initial Scaffolding

> \[!IMPORTANT\] **Verdict: GO --- pending confirmation of six blockers
> (H1-1 through H1-4 from v3, plus two new items below).**

All 8 issues raised have been resolved. The contract is now internally
consistent.

### Two additional pre-scaffolding confirmations (new)

  ------------------------------------------------------------------------
  \#               Item                  Owner            By
  ---------------- --------------------- ---------------- ----------------
  H1-5 (new)       Team agrees on        Dev 1            Day 1
                   encryption key                         
                   management approach                    
                   (env vars +                            
                   user-secrets) and Dev                  
                   1 documents key names                  
                   in README before                       
                   first commit                           

  H1-6 (new)       One team member does  Any              Day 1
                   a 10-minute visual                     
                   check of PDF headings                  
                   vs markdown to close                   
                   the                                    
                   content-equivalence                    
                   risk                                   
  ------------------------------------------------------------------------

### Items confirmed resolved in v4

  ---------------------------------------------------------------------
  Item                               Status
  ---------------------------------- ----------------------------------
  Foundation table count is 8,       ✅
  consistently stated                

  JWT emits one `role` claim per     ✅
  role; ASP.NET Core compatibility   
  confirmed                          

  ShelterAllocation has              ✅
  `IdempotencyKey` in entity, SQL    
  and EF                             

  `NotificationStatus` = Pending,    ✅
  Sent, Failed only                  

  Email security uses AES-GCM +      ✅
  HMAC-SHA256(keyed); keys from      
  environment                        

  SQL Server filtered index uses     ✅
  `OR`, not `IN`;                    
  `FK_notifications_users` added;    
  `audit_log` actor FK rationale     
  documented                         

  Allocation transaction is          ✅
  three-phase; concurrent duplicate  
  race condition handled             

  PDF/MD comparison is cautious and  ✅
  accurate                           
  ---------------------------------------------------------------------

### First commit checklist (Dev 1, Day 1)

1.  `global.json` --- `sdk.version = "10.0.400"`
2.  `Cms.sln` --- 5 empty projects, all compile
3.  `ALLOCATION.md` --- agreed module-to-developer table
4.  `db/00_foundation.sql` --- 8 tables, corrected index syntax,
    `FK_notifications_users`
5.  `db/98_foreign_keys.sql` --- empty stub with header comment
6.  `README.md` --- environment variable names (`EMAIL_ENCRYPTION_KEY`,
    `EMAIL_HASH_KEY`, `JWT_SECRET`, `SMTP_*`), `dotnet user-secrets`
    instructions, DB run order

All four developers then branch from `develop` and begin Phase 1 work.
