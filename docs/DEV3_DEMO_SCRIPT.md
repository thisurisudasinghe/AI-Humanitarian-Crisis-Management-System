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

Expected final evidence from the final integrated run: 0 build warnings,
0 errors, 499 passing tests, successful TypeScript and Next.js builds, and a
parseable Postman collection. The collection in
`docs/postman/Cms.postman_collection.json` covers the AI, Governance and
Notifications folders.

## 6. Live Recommendation -> Approval -> Case Action run

The final verification was performed on 2026-11-21 against a fresh isolated
LocalDB database (`CmsDb_Dev3Live_20261121`) and a real API on
`http://127.0.0.1:5210`. The checked-in schema scripts and
`90_seed_scenario.sql` were applied first. Because of the M4 resource naming
mismatch, temporary snake_case compatibility views/triggers were created only
in that disposable database; they are not part of the repository or a shared
database. See `docs/DEV3_LIVE_EVIDENCE.md` for the copied response bodies and
SQL rows.

Reproduce the same flow with authenticated API calls (demo password
`Demo1234!`):

1. Log in as `citizen@example.org`, `logistics@example.org` and
   `coordinator@example.org` through `POST /api/v1/auth/login` (all HTTP 200).
2. `POST /api/v1/requests` as the citizen returns HTTP 201 with the new request
   id. After a few seconds,
   `GET /api/v1/recommendations?targetId=<new request id>` returns rule-based
   `Classification` and `Priority` recommendations.
3. Read seeded request `1d000000-0000-0000-0000-000000000006` as the
   coordinator; confirm `Triaged`, `Food`, `Critical`.
4. `POST /api/v1/recommendations/match` with
   `{ "requestId": "1d000000-0000-0000-0000-000000000006", "type": "ResourceMatch" }`
   returns HTTP 200 with a real resource candidate, recommendation id
   `8b86b352-4189-4d59-a3fb-b3ba93d61f00` and `requestStatus: "Recommended"`
   in this run.
5. Try the decision as the citizen -> HTTP 403. Try the case action without a
   decision -> HTTP 403. Record the approval as logistics through
   `POST /api/v1/recommendations/8b86b352-4189-4d59-a3fb-b3ba93d61f00/decisions`
   -> HTTP 201, decision id `1f78bcb7-6a1e-4631-afe3-820eabdc579d` in this
   run. Repeating it -> HTTP 409. The request now reads `Approved`.
6. Transition the request with the coordinator endpoint through `Allocated`,
   `Dispatched` and `Fulfilled` (HTTP 204 each). Closing directly -> HTTP 403
   with `code: "CLOSE_DECISION_REQUIRED"`.
7. Create a `CloseCase` recommendation, approve it as coordinator (HTTP 201;
   decision `765ae2ce-a7ea-4ec0-ae32-71c5c3c686d3` in this run), then close with
   that `decisionId` -> HTTP 204. The final request is `Closed`.
8. Verify with `sqlcmd`: request/history rows, recommendations and decisions,
   outbox and notification rows, audit events, and both append-only triggers.
   The observed SQL verification exited 0.

This run intentionally does not demonstrate a successful M4 allocation write:
that endpoint has an unrelated EF execution-strategy/transaction defect. The
decision-gated Critical close transition is the Dev3-owned positive case action
and passed.
