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
| Browser clients | XSS/clickjacking/referrer leakage | Conservative CSP, `X-Frame-Options: DENY`, `nosniff`, `Referrer-Policy: no-referrer`, Permissions-Policy |
| Audit trail | Tampering or sensitive-data leakage | Append-only EF guard, no `UpdatedAt`, explicit actor overload, recursive masking |
| Secrets | Accidental commit | No real secrets in `appsettings.json`; fixed development-only keys are committed in `appsettings.Development.json`; production uses environment variables or a secret store; non-dev startup fails fast |

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

A fallback policy requires authentication on every endpoint unless it opts out
with `[AllowAnonymous]`. Use `RoleNames` constants, never raw role strings.

## Request hardening

- `SecurityHeadersMiddleware` adds headers to every response, including errors.
- `RequestBodyLimitMiddleware` rejects bodies over 1 MiB with `413` and code
  `REQUEST_TOO_LARGE`.
- Model-state errors return `422` with code `VALIDATION_FAILED` and per-field errors.
- Rate-limit rejections return `429` with code `RATE_LIMITED` and `Retry-After: 60`.
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

## Local development keys

`backend/src/Cms.Api/appsettings.Development.json` intentionally contains fixed,
public, development-only keys. They exist so the SQL-seeded demo users in
`backend/db/99_seed_users.sql` keep working across process restarts: the seed
stores an HMAC of each email and an AES-GCM ciphertext produced with these exact
keys.

These keys are not secrets and must never be used outside local development.
Production and any shared environment must supply `JWT_SECRET`,
`EMAIL_ENCRYPTION_KEY`, `EMAIL_HASH_KEY` and `ConnectionStrings__DefaultConnection`
through environment variables or a secret store; non-Development startup fails
fast when they are missing.

If the committed file is removed or the keys are blank, Development still falls
back to ephemeral process keys so a fresh checkout can start. Those keys are not
persisted and tokens do not survive a restart.

If you replace the development keys with your own user-secrets, the demo users
will no longer be found by email until you reseed or register them again.
User-secrets take precedence over `appsettings.Development.json`; remove old
values with `dotnet user-secrets remove "<key>"` if you want the committed demo
keys. To override the connection string only:

```powershell
cd backend/src/Cms.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<local SQL connection>"
```

Generate distinct keys for non-development environments, for example:

```powershell
[Convert]::ToBase64String([byte[]](1..32 | ForEach-Object { Get-Random -Max 256 }))
```
