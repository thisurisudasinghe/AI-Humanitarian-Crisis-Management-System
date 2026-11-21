# Security Baseline

This document records the controls enforced by the API composition root and the
`Cms.Api` middleware pipeline. It is the Dev 1 security hand-off for the whole
team (NFR-SEC-01/02/03/04/05/08 and NFR-AUD-01).

## Threat model (summary)

| Asset | Primary threat | Control |
|---|---|---|
| Credentials | Password/token theft in logs or storage | PBKDF2 password hashes, AES-GCM encrypted email, HMAC email lookup, SHA-256 refresh-token hashes, log scrubbing and audit masking |
| Admin operations | Privilege escalation | Ten frozen roles, `RoleNames` constants only, role policies, default-deny fallback |
| Public endpoints | Brute force / scraping | 60/min auth limiter, 30/min public read limiter, 100/min global limiter, 429 + `Retry-After` |
| Request pipeline | Oversized/malformed payloads | 1 MiB body limit, central validation -> 422 ProblemDetails, JSON content type on writes |
| Evidence uploads | Malicious file, path traversal, storage abuse | PNG/JPEG/WebP/PDF allowlist, magic-byte check, generated storage keys, 1 MB file cap, 20/min upload limiter, `File.Uploaded` audit row |
| Browser clients | XSS/clickjacking/referrer leakage | Conservative CSP, `X-Frame-Options: DENY`, `nosniff`, `Referrer-Policy: no-referrer`, Permissions-Policy |
| Audit trail | Tampering or sensitive-data leakage | Append-only EF guard, no `UpdatedAt`, explicit actor overload, recursive masking |
| Outbox | Lost or duplicated events, poison messages | Event row shares the caller's transaction, unique idempotency key, retry backoff, dead-letter after three attempts, `AdminOnly` dead-letter view with no payload |
| Secrets | Accidental commit | No secrets in any committed config; Development uses user-secrets/environment variables or ephemeral process keys; demo users are seeded at runtime; production uses environment variables or a secret store; non-dev startup fails fast |

## Authentication

- `POST /api/v1/auth/register`, `/login`, `/refresh` are explicitly anonymous.
- `POST /api/v1/auth/logout` and `GET /api/v1/auth/me` require a bearer token.
- Access tokens are HMAC-SHA256 JWTs, 15 minutes by default, with `sub`, `email`,
  `jti`, optional `orgId` and one `role` claim per role.
- `MapInboundClaims = false` and `RoleClaimType = "role"` are set in JWT bearer
  options so role names are not remapped to XML claim URIs.
- Refresh tokens are 256-bit random base64url values. Only their SHA-256 hash is
  stored. Rotation revokes the presented token and issues a 7-day replacement.
- Reuse of a rotated/revoked refresh token revokes every live token for that user.
- Inactive/suspended users cannot log in or refresh.

## Authorization

Policies registered in `JwtAuthenticationExtensions`:

| Policy | Roles |
|---|---|
| `StaffOnly` | All frozen roles except `Citizen` |
| `AdminOnly` | `SystemAdmin` |
| `CoordinatorOnly` | `EmergencyCoordinator` |
| `DataOfficer` | `EmergencyCoordinator`, `CaseManager` |
| `KpiViewer` | `SystemAdmin`, `EmergencyCoordinator`, `PartnerOfficer` |
| `DonationManager` | `SystemAdmin`, `DonorOfficer` |
| `DonationViewer` | `SystemAdmin`, `DonorOfficer`, `EmergencyCoordinator`, `PartnerOfficer` |

A fallback policy requires authentication on every endpoint unless it opts out
with `[AllowAnonymous]`. Use `RoleNames` constants, never raw role strings.

## Request hardening

- `SecurityHeadersMiddleware` adds headers to every response, including errors.
- `RequestBodyLimitMiddleware` rejects bodies over 1 MiB with `413` and code
  `REQUEST_TOO_LARGE`.
- Model-state errors return `422` with code `VALIDATION_FAILED` and per-field errors.
- Rate-limit rejections return `429` with code `RATE_LIMITED` and `Retry-After: 60`.
- `POST /api/v1/files` requires authentication, accepts one file field named
  `file`, caps the file at 1 MB, derives the stored extension from the validated
  content type and rejects content that does not match its declared type.
- Controllers that accept bodies declare `[Consumes("application/json")]`.
- Non-Development startup throws if `JWT_SECRET`, `EMAIL_ENCRYPTION_KEY`,
  `EMAIL_HASH_KEY` or `ConnectionStrings__DefaultConnection` are missing.

## Audit and privacy

- `AuditLogger` inserts one `common.audit_events` row per state change and
  commits it after the business change.
- `SensitiveDataMasker` redacts keys containing password, token, secret, email,
  phone, medical, accessibility or file-path terms. It is applied before storage
  and again on audit reads.
- Audit events are append-only: EF throws on `Modified` or `Deleted`.
- Failed logins are audited with the email masked.
- Never log credentials, bearer tokens, phone numbers, medical data or
  accessibility details on any module.

## Transactional outbox

- `IOutbox.EnqueueAsync` adds a `common.outbox_messages` row to the caller's
  `CmsDbContext` and does not save. The caller's `SaveChangesAsync` commits the
  business change and the event together, so a committed change always has an
  event and a rolled-back change never does.
- Enqueue is idempotent. A key that is already tracked or persisted is ignored,
  and the unique `UX_outbox_idempotency_key` index stops a concurrent duplicate.
  Callers should use `OutboxEventTypes.BuildIdempotencyKey` (`type:entityId:version`).
- `OutboxProcessor` polls `Pending` and `Failed` rows every five seconds, claims
  a row as `Processing`, resolves the single `IOutboxHandler` whose `Type`
  matches, and saves the handler's changes with the `Processed` status. A
  handler failure discards that scope, increments `RetryCount` with exponential
  backoff, and moves the row to `DeadLettered` after `Outbox:MaxRetries`
  (default three). Rows stuck in `Processing` past
  `Outbox:StaleProcessingMinutes` (default ten) are recovered.
- `GET /api/v1/admin/outbox/dead` is `AdminOnly`. It returns type, entity id,
  status, retry count, timestamps and the error message, but never the serialized
  payload or idempotency key.
- `Outbox:ProcessorEnabled` (default `true`) disables the worker. The in-memory
  integration harness sets it to `false`; production must keep it enabled.
- SQL Server rejects `OR`/`IN` in filtered-index predicates, so the v4 contract's
  single `IX_outbox_status_retry` is implemented as the equivalent
  `IX_outbox_status_retry_pending` and `IX_outbox_status_retry_failed` indexes.
- Handlers must be idempotent: a crash after the handler commits but before the
  `Processed` status is saved can cause a retry of the same event.

## Local development secrets

No secrets are committed to the repository. `backend/src/Cms.Api/appsettings.Development.json`
contains only non-secret defaults: a LocalDB connection string, logging, and
`DemoSeed:Enabled`.

Development and test secrets must be supplied through `dotnet user-secrets` or
environment variables. If `Jwt:SecretKey`, `EmailProtection:EncryptionKey` or
`EmailProtection:HashKey` are missing in Development, the API generates
ephemeral process keys so a fresh checkout can start. Those keys are not
persisted, so tokens do not survive a restart.

Demo users are created/updated at API startup by `DevelopmentDemoSeeder` using
the current keys. This means changing keys does not break demo login, and no
key-derived ciphertext or hashes need to be committed. Run the SQL schema
scripts first; if the database is not ready, the seeder logs a warning and the
API still starts.

To set persistent local keys (do not print or commit the values):

```powershell
cd backend/src/Cms.Api
dotnet user-secrets set "Jwt:SecretKey" "<new random value>"
dotnet user-secrets set "EmailProtection:EncryptionKey" "<32-byte base64 value>"
dotnet user-secrets set "EmailProtection:HashKey" "<32-byte base64 value>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<local SQL connection>"
```

Generate distinct keys for non-development environments, for example:

```powershell
[Convert]::ToBase64String([byte[]](1..32 | ForEach-Object { Get-Random -Max 256 }))
```
