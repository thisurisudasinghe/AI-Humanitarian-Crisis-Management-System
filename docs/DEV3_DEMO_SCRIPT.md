# Member 3 demo script (AI, Governance & Notifications)

This script accompanies `docs/demo/Dev3_Demo_Video.zip`. The video is a concise
slide walkthrough of the implemented flow; the commands below reproduce the
evidence locally.

## 1. Outbox first

1. Submit a request as the seeded citizen. The request write and the
   `Request.Submitted` outbox row commit in the same transaction.
2. Open `/admin/outbox` as an admin. Show pending/processed rows and the
   dead-letter view at `/admin/dead-letters`.
3. Explain the processor: 5-second poll, exponential back-off, maximum three
   attempts, then `DeadLettered`. Repeated idempotency keys are ignored.

Routes: `GET /api/v1/admin/outbox/dead`, `GET|POST /api/v1/admin/dead-letters`.

## 2. AI decision support

1. Consume `Request.Submitted` through `RequestSubmittedHandler`.
2. Show the resulting `Classification`, `Priority`, `Duplicate` (when matched)
   and `Anomaly` recommendations at `GET /api/v1/recommendations?targetId=...`.
3. Explain Strategy: `RuleBasedProvider` is the always-available default;
   `LlmProvider` is optional, uses the shared named `cms-external` client, has a
   5-second timeout and sends only category, urgency, area and scrubbed text.
4. Inject an LLM HTTP failure or timeout. The selector falls back to rules and
   sets `Source=Rule`; submission remains non-blocking.

Route: `POST /api/v1/recommendations/match`.

## 3. Human governance

1. Request a match for a `Triaged` request. The top result and alternatives are
   stored; only Resource/Shelter matches move `Triaged -> Recommended`.
2. Record an `Approved`, `Modified`, `Rejected` or `Deferred` decision through
   the exact route `POST /api/v1/recommendations/{id}/decision`.
3. Show that the decision record is append-only and a second decision fails.
   For Approved/Modified Resource/Shelter matches, the request moves
   `Recommended -> Approved` through `IRequestStatusService`.
4. Open `/operations/decisions` and filter the post-incident review table by
   crisis, date and outcome. Point out the override and final-request-status
   columns.

Route: `GET /api/v1/post-incident-review`.

## 4. Notifications and announcements

1. Open `/notifications`; show the in-app inbox and the unread bell badge.
2. Explain the Factory pattern: `NotificationChannelFactory` returns
   `InAppNotificationChannel` and `EmailNotificationChannel`. SMTP failure marks
   the email row failed and rethrows so the outbox retries; in-app delivery is
   unaffected. Reprocessing the same message creates no duplicate notification.
3. Create an announcement draft for roles and/or areas. It creates an
   `Announcement` recommendation and remains pending.
4. As `EmergencyCoordinator`, approve it. A decision record is written first,
   then one `Notification.Send` row per recipient is queued. Rejecting records a
   decision but performs no fan-out.

Routes: `GET /api/v1/notifications`, `GET|POST /api/v1/announcements`,
`POST /api/v1/announcements/{id}/approve`,
`POST /api/v1/announcements/{id}/reject`.

## 5. Verification

Run from the repository root:

```powershell
dotnet restore backend/Cms.sln
dotnet build backend/Cms.sln
dotnet test backend/Cms.sln
cd frontend
npm.cmd run typecheck
npm.cmd run build
```

Expected final evidence: 0 build warnings, 0 errors, 494 passing tests,
successful TypeScript check and 34/34 generated Next.js pages. The Postman
collection in `docs/postman/Cms.postman_collection.json` parses successfully and
covers the AI, Governance and Notifications folders.
