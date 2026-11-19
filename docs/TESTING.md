# Testing and UAT

This document is the Dev 1 (team lead) testing hand-off for Task 3. It records
the test layers, the commands, current coverage, the end-to-end UAT loop, and the
manual regression checklist for the modules Dev 1 owns (`identity`, `common`,
`don`, administration, audit, KPIs and the frontend shell).

Cross-module steps stay marked **pending** until Members 2–4 publish their SQL
scripts and endpoints. Nothing in this document assumes another member's schema.

## 1. Test layers

| Layer | Location | What it proves |
|---|---|---|
| Unit | `backend/tests/Cms.Tests/Domain`, `.../Infrastructure` | Frozen role names, snake_case naming conventions, JWT issuing/validation, password hashing, sensitive-data masking, Haversine fallback, append-only audit guard |
| Integration | `backend/tests/Cms.Tests/Integration` | The real middleware pipeline, JWT authentication, authorization policies, validation, ProblemDetails mapping and controllers against an isolated in-memory EF database |
| Live SQL verification | Manual, LocalDB/SQL Server | EF configurations and queries run against the hand-written `backend/db` scripts (the in-memory provider cannot prove SQL translation) |
| Frontend | `frontend` | TypeScript strictness, route compilation and static prerendering |

The integration harness (`CmsWebApplicationFactory`) seeds four accounts
(SystemAdmin, PartnerOfficer + CaseManager, Citizen, Inactive) and does not need
SQL Server.

## 2. Commands

```powershell
# Backend
dotnet build backend/Cms.sln
dotnet test backend/tests/Cms.Tests/Cms.Tests.csproj

# Frontend
cd frontend
npm ci
npm run typecheck
npm run build
```

Database (canonical order, every script is idempotent — run each one twice to
prove it):

```powershell
# 00_common.sql -> 03_ai.sql -> 01_cases.sql ... 08_don.sql
# -> 98_foreign_keys.sql -> 99_seed_users.sql
# -> 90_seed_scenario.sql, 91_seed_ops.sql
sqlcmd -S "(localdb)\mssqllocaldb" -d CmsDb -I -b -i backend/db/00_common.sql
sqlcmd -S "(localdb)\mssqllocaldb" -d CmsDb -I -b -i backend/db/03_ai.sql
sqlcmd -S "(localdb)\mssqllocaldb" -d CmsDb -I -b -i backend/db/08_don.sql
sqlcmd -S "(localdb)\mssqllocaldb" -d CmsDb -I -b -i backend/db/98_foreign_keys.sql
sqlcmd -S "(localdb)\mssqllocaldb" -d CmsDb -I -b -i backend/db/99_seed_users.sql
```

Run the API for manual checks:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --project backend/src/Cms.Api/Cms.Api.csproj --no-launch-profile --urls http://localhost:5210
```

Stop `Cms.Api` before rebuilding; the running process locks the output DLLs.

### Demo login troubleshooting

If a demo login that previously worked starts returning `401`, check for stale
local User Secrets before touching the code. `dotnet user-secrets` values
override `appsettings.Development.json`, and an old `EmailProtection` or `Jwt`
key breaks both the seeded email hashes and token validation. Clear the
override and restart:

```powershell
dotnet user-secrets clear --project backend/src/Cms.Api/Cms.Api.csproj
```

The committed `appsettings.Development.json` demo keys are the source of truth
for local development; do not re-add them to User Secrets.

## 3. Current coverage

- 141 backend tests, 0 skipped (unit + integration).
- 30 frontend routes build statically, including `/notifications`, `/donations`
  and `/kpis`.

| Area | Covered by |
|---|---|
| Registration, login, refresh rotation, logout, `/me`, inactive rejection | `AuthFlowTests` |
| Default-deny, security headers, body limit, validation shape | `SecurityBaselineTests` |
| Rate limits and `429` + `Retry-After` | `RateLimitingTests` |
| Users, organisations, configuration, audit search | `AdminEndpointTests` |
| Identity, audit, donation and notification KPI sections, both KPI routes, CSV export | `KpiEndpointTests` |
| Donor, campaign, donation, allocation, expense, flagging and transparency report | `DonationEndpointTests` |
| Evidence upload validation, storage and audit | `FileUploadTests`, `LocalFileStoreTests` |
| Outbox enqueue, idempotency, dispatch, retry, dead-letter, stale recovery and admin view | `OutboxServiceTests`, `OutboxProcessorTests`, `OutboxEndpointTests` |
| Notification inbox, read state, failed-delivery admin view | `NotificationEndpointTests` |
| `Notification.Send` handler: in-app + email rows, idempotent retry, failed delivery, inactive recipient | `EmailNotificationHandlerTests` |
| Cases, AI, governance, resources, shelter, volunteers, mapping | Pending other modules |

## 4. End-to-end UAT loop

The SRS core loop is the UAT scenario. Each step is listed with its owner and
current readiness.

| # | Step | Owner | Ready | Verified today by |
|---|---|---|---|---|
| 1 | Citizen registers and logs in | Dev 1 | Yes | `AuthFlowTests`, manual login of `citizen@example.org` |
| 2 | Citizen submits a request | Dev 2 | Pending | Waiting on `01_cases.sql` and request endpoints |
| 3 | Coordinator verifies the request | Dev 2 | Pending | Waiting on request workflow endpoints |
| 4 | AI classifies and scores with confidence and rationale | Dev 3 | Pending | Waiting on AI endpoints and decision records |
| 5 | Human approves, modifies or rejects the recommendation | Dev 3 | Pending | Waiting on governance endpoints |
| 6 | Resource, shelter and/or volunteer allocation | Dev 4 | Pending | Waiting on allocation endpoints |
| 7 | Notification is sent to the citizen | Dev 1 | Yes | `EmailNotificationHandlerTests`, in-app inbox via `GET /api/v1/notifications` |
| 8 | Case is completed and the audit trail is reviewable | Dev 2 + Dev 1 | Partly | Audit viewer, filters and append-only guard are ready; case closure is pending |

The SRS counts this as seven UAT steps; audit is treated as the cross-cutting
control rather than a separate user action.

## 5. Demo accounts

All demo accounts use the password `Demo1234!` and are created/updated at API
startup by `DevelopmentDemoSeeder` after the SQL schema scripts have run.

| Email | Role(s) | Purpose |
|---|---|---|
| `admin@example.org` | SystemAdmin | Administration, configuration, audit, KPIs |
| `coordinator@example.org` | EmergencyCoordinator | Coordination and KPI read access |
| `casemanager@example.org` | CaseManager | Case handling |
| `logistics@example.org` | LogisticsManager | Logistics |
| `shelter@example.org` | ShelterAdmin | Shelter operations |
| `volunteercoordinator@example.org` | VolunteerCoordinator | Volunteer coordination |
| `volunteer@example.org` | Volunteer | Volunteer self-service |
| `donor@example.org` | DonorOfficer | Donation writes and reads |
| `partner@example.org` | PartnerOfficer | Read-only donation and KPI access |
| `citizen@example.org` | Citizen | Citizen portal |
| `multi@example.org` | EmergencyCoordinator + CaseManager | Multi-role policy checks |
| `inactive@example.org` | Citizen (Inactive) | Must be rejected with `401` |

## 6. Manual regression checklist (Dev 1)

Run this before every Dev 1 hand-off or demo.

1. Run `00_common.sql`, `08_don.sql`, `98_foreign_keys.sql` and
   `99_seed_users.sql` twice. Both passes must succeed; `common.system_settings`
   must contain three rows. Then start the API once in Development so
   `DevelopmentDemoSeeder` creates the demo users.
2. Log in as each active demo account and confirm `GET /api/v1/auth/me` returns
   the expected roles. `inactive@example.org` must return `401`.
3. Confirm the authorization matrix:
   - anonymous → `401` on every non-public endpoint;
   - Citizen → `403` on `/api/v1/users`, `/api/v1/kpis`, `/api/v1/donations`;
   - PartnerOfficer → `200` on donation reads, `403` on donation writes.
4. Admin: create/update an organisation, create a user, replace roles, patch
   configuration. Each write must produce an audit row with the actor.
5. Audit: filter by action, entity type, entity id, actor and date range; confirm
   the details are masked and that updates/deletes are impossible.
6. KPIs: call `GET /api/v1/kpis` (the contract alias
   `GET /api/v1/reports/kpis` must return the same report) and confirm identity,
   audit, donation and notification sections are populated, `pendingSections`
   lists only `cases`, `resources`, `shelter` and `ai`, and the remaining
   cross-module values stay empty. `GET /api/v1/kpis/export` must return a
   `text/csv` attachment whose rows include `Total users` and
   `Total notifications`; the `/kpis` page's Export CSV button downloads it.
7. Donations: create a donor, a campaign (Draft → Active), a monetary donation,
   an allocation and an expense. Confirm over-value and cross-currency
   allocations are rejected, a donation above `SuspiciousDonationAmount` is
   flagged, a human can clear the flag, and the transparency report contains no
   donor name or email.
8. Notifications: insert a `Pending` `common.outbox_messages` row of type
   `Notification.Send` with a `NotificationPayloadDto` payload for an active
   user. The processor must create one `InApp` row (visible in that user's
   inbox) and one `Email` row. Retrying the same event must not duplicate rows.
   With no `Smtp:Host` configured the log sender records the email delivery; a
   failing sender leaves the email row `Failed` and the event retries.
9. Frontend: `npm run typecheck` and `npm run build` must pass; `/notifications`,
   `/donations` and `/kpis` must appear in the route list. As a signed-in user,
   `/notifications` shows the inbox with All/Unread filters, per-item and
   mark-all read, and `/dashboard` shows the three most recent messages.
10. Files: upload a PNG as `citizen@example.org`; confirm `201` with a 32-hex key
    and that the file appears under `backend/src/Cms.Api/App_Data/uploads`.
    Confirm a `.txt` upload, a PNG whose bytes are not a PNG, and a file over
    1 MB are all rejected with `422`, and that `File.Uploaded` appears in the
    audit viewer.
11. Outbox: run `00_common.sql` twice and confirm `common.outbox_messages` plus
    `UX_outbox_idempotency_key`, `IX_outbox_status_retry_pending`,
    `IX_outbox_status_retry_failed` and `IX_outbox_created_at`. Insert a
    `Pending` row of an unhandled type; the processor must move it through
    `Failed` to `DeadLettered`, and `GET /api/v1/admin/outbox/dead` must show it
    without `payload` or `idempotencyKey`.

## 7. Accessibility checks (NFR-USA-02)

Apply to `/login`, `/register`, `/dashboard`, `/admin/*`, `/donations` and
`/kpis`:

- Keyboard-only navigation reaches every control; the skip link is first.
- Every input has a visible label; errors are announced next to the field.
- Tables use `th` with `scope`, and headings are ordered.
- Focus outlines remain visible; colour is never the only signal.
- Text contrast targets WCAG 2.1 AA.

## 8. Blocked coverage

These items cannot be completed until the owning modules publish their schemas
and endpoints:

- `01_cases.sql` … `07_geo.sql` and the `90_seed_scenario.sql` /
  `91_seed_ops.sql` repeatable scenario.
- Cross-module foreign keys in `98_foreign_keys.sql` beyond the notification →
  user relationship (which Dev 1 now owns).
- Integration tests for requests, AI, governance, resources, shelter, volunteers
  and mapping.
- The full UAT run and the Task 3 UAT evidence pack.
