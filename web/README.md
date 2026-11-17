# Frontend Application

Next.js 16 App Router client for the AI-Powered Humanitarian Crisis Management
System. This shell covers Dev 1 surfaces: authentication, protected layout,
dashboard, user/organisation/configuration administration, audit viewer and the
provisional KPI report.

## Setup

```powershell
cd web
npm install
npm run dev
```

Create `.env.local` if the API is not at the default URL:

```powershell
NEXT_PUBLIC_API_URL=http://localhost:5210
```

## Scripts

```powershell
npm run typecheck
npm run build
npm start
```

## Routes

| Route | Purpose |
|---|---|
| `/login`, `/register` | Anonymous authentication |
| `/dashboard` | Protected landing page |
| `/kpis` | KPI report (`SystemAdmin`, `EmergencyCoordinator`, `PartnerOfficer`) |
| `/admin/users` | User create, role replacement and activation (`SystemAdmin`) |
| `/admin/organisations` | Organisation CRUD (`SystemAdmin`) |
| `/admin/config` | Runtime settings (`SystemAdmin`) |
| `/admin/audit` | Append-only audit viewer (`SystemAdmin`) |
| `/403`, not-found page | Error states |

## Token storage decision

- The access token is kept **in memory only** (`web/lib/api.ts`). It is never
  written to `localStorage` or `sessionStorage`.
- The refresh token is stored in `sessionStorage` so a tab refresh can restore
  the session without persisting it across browser restarts.
- `sessionStorage` is readable by JavaScript, so an XSS flaw in this app could
  expose the refresh token. The API mitigates the impact with short-lived access
  tokens, refresh-token rotation, reuse detection, strict CSP, security headers,
  audit logging and rate limits. Do not change this to `localStorage`.
- A failed refresh clears local tokens and protected pages redirect to `/login`.
- API errors are surfaced from RFC 7807 `ProblemDetails` with stable `code`
  values such as `VALIDATION_FAILED`, `EMAIL_ALREADY_EXISTS` and `RATE_LIMITED`.

## Role names

Use `web/lib/roles.ts` rather than raw strings. The ten roles are frozen by the
shared team contract and must match `Cms.Domain.Constants.RoleNames`.
