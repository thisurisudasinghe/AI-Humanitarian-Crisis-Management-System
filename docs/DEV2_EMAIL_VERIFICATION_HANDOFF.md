# DEV2 Email Verification / Resend Handoff

Status: **inspection and design handoff only**. No production code, authentication code, shared contract, configuration, database script, or existing frontend file was modified.

This document records the ownership conflicts and exact changes required before Safeway email verification can be implemented. The implementation requested spans DEV1 identity, shared notifications/configuration, and DEV1 frontend routing. Those changes are outside DEV2's approved ownership boundary (`ALLOCATION.md`: DEV1 owns M1 Identity & Access and M9 Notifications; DEV2 owns M2 Crisis & Case Management and M8 Donor Management).

---

## 1. Existing implementation discovered

- Authentication is custom, not ASP.NET Identity. Only `Microsoft.AspNetCore.Identity.PasswordHasher<User>` is used as a password-hashing helper.
- `identity.users` already contains `is_email_verified`, and `User.IsEmailVerified` exists.
- Registration sets `IsEmailVerified = false` but immediately issues access and refresh tokens.
- `IsEmailVerified` is not exposed in `AuthResponseDto` or `CurrentUserDto` and is not enforced by login, refresh, JWT generation, authorization policies, or case endpoints.
- No verification token table, token service, verification endpoint, resend endpoint, Resend integration, or verification email template exists.
- Existing email delivery is MailKit SMTP through `INotificationSender`, with a log-only fallback. Notification templates are in-code (`NotificationTemplates.cs`) and seeded in `backend/db/02_notif.sql`; the two are not connected at runtime.
- Existing external HTTP infrastructure is `backend/src/Cms.Infrastructure/Http/HttpClientExtensions.cs` (`AddCmsHttpClient`, named `cms-external`).
- Rate limiting exists: global 100/min, `auth` 60/min, `public` 30/min, `uploads` 20/min.
- Frontend `frontend/app/register/page.tsx` tells the user they are signed in immediately after registration. No verification pages exist.

---

## 2. Safe additive artifacts created

These files are design/reference artifacts only and are not wired into runtime code:

- `docs/assets/safeway-email-logo.svg`
- `docs/email-templates/email-verification-register.html`
- `docs/email-templates/email-verification-register.txt`
- `docs/email-templates/email-verification-resend.html`
- `docs/email-templates/email-verification-resend.txt`
- `docs/DEV2_EMAIL_VERIFICATION_HANDOFF.md`

They must be reviewed and adopted by the owner of notifications/identity before use.

---

## 3. Ownership decision

| Area | Owner | DEV2 action |
|---|---|---|
| Auth endpoints, registration behavior, tokens, `/auth/me` | DEV1 | **No change by DEV2.** DEV1 approval/implementation required. |
| `identity.users`, `identity.*` verification token table, `CmsDbContext` | DEV1 | **No change by DEV2.** DEV1 approval/implementation required. |
| `INotificationSender`, notification handler, templates, `02_notif.sql` | DEV1/M3 disputed | **No change by DEV2.** Owner must be confirmed. |
| Resend config, `Program.cs`, DI, secret mapping | DEV1/shared | **No change by DEV2.** DEV1 approval/implementation required. |
| Frontend auth context, register page, `AuthGuard`, verify routes | DEV1/shared | **No change by DEV2.** DEV1 approval/implementation required. |
| DEV2 case submission gating (`POST /api/v1/requests`, `ReportFlow`, `crisis.ts`) | DEV2 with DEV1 contract | **Deferred** until DEV1 defines the verified-session contract and error code. |
| Standalone SVG/template design artifacts | New files only | Completed in this handoff. |

---

## 4. Exact required DEV1/shared changes and impact

### 4.1 Backend identity contract

Files:

- `backend/src/Cms.Api/Controllers/AuthController.cs`
- `backend/src/Cms.Application/Interfaces/IAuthenticationService.cs`
- `backend/src/Cms.Application/DTOs/Authentication/`
- `backend/src/Cms.Application/DTOs/CurrentUserDto.cs`
- `backend/src/Cms.Infrastructure/Services/AuthenticationService.cs`
- `backend/src/Cms.Infrastructure/Authentication/JwtTokenService.cs`
- `backend/src/Cms.Api/Authentication/JwtAuthenticationExtensions.cs`

Required changes:

1. Add verification endpoints under `/api/v1/auth` (or `/api/v1/identity/email-verification`) with explicit `[AllowAnonymous]` and a dedicated stricter rate-limit policy.
2. Add verification request/response DTOs. Never return the token after creation.
3. Expose `isEmailVerified` on `AuthResponseDto` and `CurrentUserDto`.
4. Decide and implement the enforcement model:
   - Preferred: add `email_verified` claim and a `VerifiedOnly` policy; apply it only to endpoints that require verified email (e.g., case submission), not the whole API.
   - Alternative: block login/refresh for unverified users. This changes registration/login behavior and is a larger contract break.
5. Decide whether registration still returns tokens. Existing `AuthFlowTests.cs` expects `201 Created` plus `AuthResponseDto`. Changing to `202 Accepted`/message-only requires DEV1 test, Postman, and frontend updates.

Impact:

- Existing frontend `register()` stores tokens immediately. If registration no longer returns tokens, `frontend/lib/auth.tsx` and `frontend/app/register/page.tsx` must change together.
- Admin-created users in `AdminService.cs` currently default to `IsEmailVerified = false`; DEV1 must decide whether to verify them, send verification, or exempt them.
- Applying a global verified gate would break `/verify-email` and `/account` unless an allowlist is added.

### 4.2 Verification token persistence

Files:

- `backend/src/Cms.Domain/Entities/` (new entity)
- `backend/src/Cms.Infrastructure/Data/CmsDbContext.cs`
- new EF configuration under `backend/src/Cms.Infrastructure/Data/Configurations/`
- new SQL script under `backend/db/` (recommended) or `backend/db/00_common.sql`

Required table (minimum):

```sql
identity.email_verification_tokens (
    id uniqueidentifier NOT NULL PRIMARY KEY,
    user_id uniqueidentifier NOT NULL REFERENCES identity.users(id) ON DELETE CASCADE,
    token_hash nvarchar(64) NOT NULL,
    created_at datetime2 NOT NULL,
    expires_at datetime2 NOT NULL,
    consumed_at datetime2 NULL,
    revoked_at datetime2 NULL
)
```

Required indexes: unique `token_hash`; index on `(user_id, created_at)` or filtered active token index.

Security requirements:

- Generate 32 cryptographically secure random bytes; encode Base64Url.
- Store only uppercase SHA-256 hex of the token.
- Expiry: 30 minutes.
- Single-use: set `consumed_at` atomically.
- On resend, revoke or supersede all active tokens for the user.
- Do not put raw tokens in `AuthResponseDto`, logs, audit details, notifications stored in `common.notifications`, or exception messages.
- Log only non-sensitive metadata such as user id, event type, and token record id.

### 4.3 Resend provider and email sender

Files requiring DEV1/M3 approval:

- `backend/src/Cms.Api/Program.cs`
- `backend/src/Cms.Api/appsettings*.json`
- `backend/src/Cms.Infrastructure/DependencyInjection.cs`
- `backend/src/Cms.Infrastructure/Cms.Infrastructure.csproj`
- `backend/src/Cms.Infrastructure/Notifications/` or a new dedicated email-verification folder
- `backend/src/Cms.Application/Contracts/` if a new sender abstraction is introduced

Recommended approach:

- Do **not** replace the existing `INotificationSender` silently. Existing registration uses `TryAddScoped<INotificationSender, ...>`; a later `TryAdd` will not override it.
- Do **not** route verification through `Notification.Send` without owner review: the current handler persists rendered body into `common.notifications`, which would store the one-time verification token in plain text.
- Introduce a dedicated verification-email sender abstraction (for example `IEmailVerificationEmailSender`) that sends via Resend using `IHttpClientFactory` and does not persist the token.
- Reuse `AddCmsHttpClient`/`cms-external` conventions or register a dedicated named client with the same resilience settings.
- Use sender display name `Safeway` from `RESEND_FROM_NAME`, defaulting only in configuration documentation, never in source secrets.
- Use configured verified sender email from `RESEND_FROM_EMAIL`. Do not invent or hardcode a sender address.

Environment variables:

```text
RESEND_API_KEY=<server-side secret>
RESEND_FROM_EMAIL=<verified sender address>
RESEND_FROM_NAME=Safeway
EMAIL_VERIFICATION_BASE_URL=https://<frontend-host>/verify-email
EMAIL_LOGO_URL=https://<public-host>/safeway-email-logo.svg
```

Map these in `Program.cs` into a bound options section (for example `Resend:ApiKey`) following the existing `EMAIL_ENCRYPTION_KEY` / `JWT_SECRET` mapping pattern. Frontend environment variables must not contain `RESEND_API_KEY`.

### 4.4 Templates and branding

Reference artifacts:

- Logo: `docs/assets/safeway-email-logo.svg`
- Registration: `docs/email-templates/email-verification-register.{html,txt}`
- Resend: `docs/email-templates/email-verification-resend.{html,txt}`

Template keys proposed for owner adoption:

- `Account.EmailVerification.Welcome` — subject `Welcome to Safeway – Verify Your Email`
- `Account.EmailVerification.Resend` — subject `Safeway – Your New Email Verification Link`

Placeholders: `{{LOGO_URL}}`, `{{FIRST_NAME}}`, `{{VERIFICATION_URL}}`.

The HTML templates use table layout, inline styles, a mobile media query, visible button plus fallback URL, 30-minute expiry, security reminder, Safeway footer, alt text, and a text-based Safeway fallback line. No animations or complex CSS are used.

The SVG must be hosted at a public HTTPS URL and referenced by `EMAIL_LOGO_URL`; a local development path must not be used in emails.

### 4.5 Frontend verification UI

Files requiring DEV1 approval/implementation:

- `frontend/lib/api.ts`
- `frontend/lib/auth.tsx`
- new `frontend/lib/verification.ts`
- `frontend/app/register/page.tsx`
- `frontend/app/login/page.tsx`
- new `frontend/app/verify-email/page.tsx`
- new `frontend/app/verify-email/[token]/page.tsx` or confirm route
- `frontend/components/AuthGuard.tsx`
- `frontend/app/(app)/account/page.tsx`
- `frontend/app/globals.css` — additive styles only, after the Figma/integration boundary

Required states: pending/resend, verifying, success, invalid/expired/used, already verified. Reuse existing `.register-success`, `.success-check`, `.form-error`, `.notice`, `.badge.*`, `Brand`, `Icon`, `Toast`, and `Skeleton` rather than creating a new design system.

### 4.6 DEV2 coordination seam (deferred)

After DEV1 defines the contract, DEV2 can safely add:

- `frontend/lib/crisis.ts`: map a stable verification-required error code in `caseRequestErrorMessage()`.
- `frontend/components/safeway/ReportFlow.tsx`: show a clear "verify your email first" state/link for unverified citizens.
- `backend/src/Cms.Api/Controllers/RequestsController.cs` or shared policy application: require verified email for `POST /api/v1/requests` only if product and DEV1 agree.

No DEV2 file is changed now because the error code, claim/policy name, and redirect path are not yet defined.

---

## 5. Testing plan for the implementing owner

Backend tests to add in `backend/tests/Cms.Tests/`:

- Registration creates user with `IsEmailVerified = false` and emits exactly one verification email request.
- Verification email uses correct subject, sender name, Safeway logo URL, fallback URL, and 30-minute expiry copy.
- Resend email uses correct subject and invalidates/supersedes prior tokens.
- Successful verification sets `IsEmailVerified = true` and consumes token.
- Expired token, invalid token, reused token, and already-verified account return stable safe codes.
- Resend rate limit returns `429` with `Retry-After`.
- Provider failure returns controlled result without leaking provider response or token.
- Unverified user cannot access a verified-only endpoint.
- No token appears in response bodies, logs, audit details, or notification rows.

Mock Resend responses with a fake `HttpMessageHandler`; never send real email in automated tests.

Frontend verification states currently have no automated test framework; use `npm run typecheck` and `npm run build` as the available gates, plus manual UAT.

---

## 6. Required approval before implementation

Do not implement runtime verification until the following are confirmed:

1. DEV1 approves the auth contract changes, enforced endpoint scope, and `/auth/me` flag.
2. DEV1 confirms the database script location and verification token table design.
3. DEV1 and current notification owner confirm who owns Resend sender implementation and template registration.
4. Product/DEV1/DEV2 agree whether unverified citizens may submit case reports.
5. Resend account has a verified sending domain/address; until then, real delivery cannot be claimed.
