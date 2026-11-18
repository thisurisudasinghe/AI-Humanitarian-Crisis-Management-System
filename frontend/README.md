# SAFEWAY Frontend

Next.js 16 App Router client for the AI-Powered Humanitarian Crisis Management
System. The UI is the SAFEWAY Figma design, migrated from the Vite export into
`frontend/` without changing the visual language: the exported stylesheet is kept
verbatim in `app/globals.css` and only additive integration rules follow it.

This branch (`feature/dev1-frontend`) targets the Dev 1 identity, administration,
donation, KPI, file and audit APIs. Modules owned by Dev 2, Dev 3 and Dev 4 render
their designed shells with explicit integration notices instead of invented data.

## Setup

```powershell
cd frontend
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

### Public (marketing shell with `PublicNav` / `PublicFooter`)

| Route | Purpose |
|---|---|
| `/` | SAFEWAY landing page |
| `/about` | Mission, values and human-oversight principle |
| `/services` | Public and authorized service catalogue |
| `/how-it-works` | Report → verify → assess → coordinate → update |
| `/contact` | Support options and prototype contact form |
| `/find-assistance` | Assistance directory shell (awaiting Dev 4 data) |
| `/report` | Three-step crisis report flow |

### Authentication

| Route | Purpose |
|---|---|
| `/login` | Real `POST /api/v1/auth/login`, redirects by backend role |
| `/register` | Real `POST /api/v1/auth/register` (citizen account) |

### Protected citizen workspace

| Route | Purpose |
|---|---|
| `/dashboard` | Citizen landing page; staff accounts are redirected to `/operations` |
| `/my-reports` | Report tracking shell (awaiting Dev 2) |
| `/notifications` | Inbox shell (awaiting M9 / Member 3) |
| `/account` | Profile, roles and session details from `/auth/me` |

### Staff operations workspace

| Route | Purpose |
|---|---|
| `/operations` | Live identity/donation KPIs and audit activity, operational placeholders |
| `/operations/crises` | Incident queue shell (awaiting Dev 2) |
| `/operations/crises/[id]` | Case workspace shell (awaiting Dev 2) |
| `/operations/ai` | Decision-support shell (awaiting Dev 3) |
| `/operations/map` | Situation-map shell (awaiting Dev 2 / Dev 4) |
| `/resources` | Inventory shell (awaiting Dev 4) |
| `/shelters` | Shelter network shell (awaiting Dev 4) |
| `/volunteers` | Volunteer coordination shell (awaiting Dev 4) |
| `/kpis` | Full KPI report (`SystemAdmin`, `EmergencyCoordinator`, `PartnerOfficer`) |
| `/donations` | Donations, campaigns, allocations (`DonationViewer` / `DonationManager`) |
| `/admin/users` | User create, role replacement and activation (`SystemAdmin`) |
| `/admin/organisations` | Organisation CRUD (`SystemAdmin`) |
| `/admin/config` | Runtime settings (`SystemAdmin`) |
| `/admin/audit` | Append-only audit viewer (`SystemAdmin`) |
| `/admin/outbox` | Read-only outbox dead-letter queue (`SystemAdmin`) |

Every `/admin/*` page shares an `AdminNav` tab strip so the sections are
reachable without returning to the sidebar.

`/403`, `not-found`, `error` and `loading` use the same design system.

## Integration status

Live against the Dev 1 API:

- authentication (`register`, `login`, `refresh`, `logout`, `me`, `role-check`)
- users, organisations and admin configuration
- audit events (append-only, `AdminOnly`)
- KPI report (`KpiViewer`)
- donations, campaigns, allocations, expenses and transparency report
- file upload and outbox dead-letter endpoints (`/admin/outbox` is read-only;
  replay is owned by the outbox processor)
- role-aware landing: `homePathForRoles` and `/dashboard` send staff accounts to
  `/operations`, citizens stay in the public workspace
- date-range filters default to the current local month and text filters are
  debounced (`frontend/lib/useDebouncedValue.ts`) to avoid a request per keystroke

Not connected yet — shown as designed shells with no fabricated records:

| Area | Owner | Seam |
|---|---|---|
| Crisis intake, cases, assignments | Dev 2 | `frontend/lib/crisis.ts` |
| AI recommendations and approvals | Dev 3 | `/operations/ai`, `/operations/crises/[id]` |
| Resources, shelters, volunteers, map | Dev 4 | `/resources`, `/shelters`, `/volunteers`, `/operations/map` |
| Notification delivery and read state | Member 3 (M9) | `/notifications` |

`submitCrisisReport` in `frontend/lib/crisis.ts` fails closed with
`FeatureNotAvailableError`, so the report flow tells the user the report was not
submitted. Replace that stub when the Dev 2 intake endpoint is published; no UI
changes are required.

## Token storage decision

- The access token is kept **in memory only** (`frontend/lib/api.ts`). It is never
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

## Authorization

`AuthGuard` redirects unauthenticated users to `/login` and renders a 403 panel
when `allowedRoles` is supplied and the account holds none of them. The sidebar
and header also hide role-specific entries, but the backend policies remain the
source of truth (`AdminOnly`, `KpiViewer`, `DonationViewer`, `DonationManager`,
`StaffOnly`, `DataOfficer`, `CoordinatorOnly`).

## Role names

Use `frontend/lib/roles.ts` rather than raw strings. The ten roles are frozen by the
shared team contract and must match `Cms.Domain.Constants.RoleNames`.

## Styling notes

- `app/globals.css` is the legacy Dev 1 stylesheet followed by the exported SAFEWAY
  stylesheet, then a small integration layer. The design rules win on collisions
  (`badge`, `brand`, `notice`, `sidebar`).
- The Figma export uses `<button>` for navigation. Those buttons are now Next.js
  `<Link>` elements, with matching anchor rules in the integration layer.
- Tailwind is not used: the design has no Tailwind utility classes, so the
  `@import "tailwindcss"` line from the Vite export was removed rather than adding
  a build dependency.
