# Dev3 live verification evidence (2026-11-21)

This file records the actual observed results of the live
Recommendation -> Human Approval -> Case Action verification. It is an evidence
record, not a claim that every module in the platform passed. No token value is
reproduced. The raw runner log was
`C:\Users\starg\AppData\Local\Temp\opencode\dev3-live-evidence.log` on the test
machine; the material results are copied here.

## Environment

- Branch under test: `feature/dev3-final-live-verification` from
  `origin/develop` `8d8267dd9b4876a63e7754f1db695627fc0093b5`.
- API: `http://127.0.0.1:5210`, started with
  `ASPNETCORE_ENVIRONMENT=Development` and a connection string to the isolated
  LocalDB database `CmsDb_Dev3Live_20261121`.
- Database: fresh database created from the checked-in scripts in this order:
  `00_common.sql`, `02_notif.sql`, `03_ai.sql`, `01_cases.sql`, `04_res.sql`,
  `05_shelter.sql`, `06_vol.sql`, `07_geo.sql`, `08_don.sql`,
  `98_foreign_keys.sql`, `99_seed_users.sql`, then `90_seed_scenario.sql`.
- Test fixture only: because `04_res.sql` creates PascalCase resource tables and
  the M4 EF mappings expect snake_case names/columns, temporary test-only views
  and `INSTEAD OF INSERT/UPDATE` triggers were created in this isolated database.
  They were not added to the repository, a migration, or any shared database.
- Resource fixture: one `ResourceType` and one `Inventory` with 10 units, no
  latitude/longitude (so the M4 matcher used its approximate, local score rather
  than an external OSRM call).
- Login was performed through `POST /api/v1/auth/login` with the seeded
  `citizen@example.org`, `logistics@example.org` and `coordinator@example.org`
  accounts. All three returned HTTP 200; tokens are intentionally omitted.

## API results

### 1. Request submit triggers AI

`POST /api/v1/requests` as `citizen@example.org` returned HTTP 201:

```json
{"id":"13b7abe7-ce4f-42c0-9447-fbd19f537b18"}
```

After the outbox processor ran, `GET /api/v1/recommendations?targetId=13b7abe7-ce4f-42c0-9447-fbd19f537b18`
returned HTTP 200 with two generated recommendations:

```json
[{"id":"4d97a2ea-633f-4d6f-a1e4-875490f2b938","type":"Priority","targetId":"13b7abe7-ce4f-42c0-9447-fbd19f537b18","payloadJson":"{\"urgency\":\"High\",\"priorityScore\":0.8}","confidence":0.5000,"rationale":"No urgency keywords found; retained the submitted urgency.","source":"Rule","status":"Pending","createdAt":"2026-11-21T17:00:13.2955306"},{"id":"ba71b656-b44b-4aba-8390-4e9d61a684c2","type":"Classification","targetId":"13b7abe7-ce4f-42c0-9447-fbd19f537b18","payloadJson":"{\"category\":\"Food\"}","confidence":0.6000,"rationale":"Matched 1 category keyword(s) for Food.","source":"Rule","status":"Pending","createdAt":"2026-11-21T17:00:13.1501167"}]
```

### 2. Real resource match

The seeded request `1d000000-0000-0000-0000-000000000006` was read as
`Triaged`, `Food`, `Critical`. `POST /api/v1/recommendations/match` as
`logistics@example.org` returned HTTP 200:

```json
{"recommendationId":"8b86b352-4189-4d59-a3fb-b3ba93d61f00","type":"ResourceMatch","status":"Pending","requestStatus":"Recommended","candidates":[{"targetId":"c3da4417-8d58-461c-a361-a14fe6f46c38","targetType":"Resource","score":1,"rationale":"Available stock: 10.00; distance: approx. 0 km.","isApproximate":true}]}
```

`GET /api/v1/recommendations/8b86b352-4189-4d59-a3fb-b3ba93d61f00` returned
HTTP 200:

```json
{"id":"8b86b352-4189-4d59-a3fb-b3ba93d61f00","type":"ResourceMatch","targetId":"1d000000-0000-0000-0000-000000000006","payloadJson":"{\"requestId\":\"1d000000-0000-0000-0000-000000000006\",\"type\":\"ResourceMatch\",\"top\":{\"targetId\":\"c3da4417-8d58-461c-a361-a14fe6f46c38\",\"targetType\":\"Resource\",\"score\":1,\"rationale\":\"Available stock: 10.00; distance: approx. 0 km.\",\"isApproximate\":true},\"alternatives\":[],\"generatedAt\":\"2026-11-21T17:00:19.978801Z\"}","confidence":1.0000,"rationale":"Available stock: 10.00; distance: approx. 0 km.","source":"Rule","status":"Pending","createdAt":"2026-11-21T17:00:19.9962661"}
```

### 3. Governance controls and human approval

- `POST /api/v1/recommendations/8b86b352-4189-4d59-a3fb-b3ba93d61f00/decisions`
  as `citizen@example.org` returned **HTTP 403** (empty body).
- `POST /api/v1/allocations` with a random decision id as
  `logistics@example.org` returned **HTTP 403** (empty body). This proves the
  no-decision case-action denial; it does not prove the M4 allocation write path.
- The same decision endpoint as `logistics@example.org` returned HTTP 201:

```json
{"id":"1f78bcb7-6a1e-4631-afe3-820eabdc579d","recommendationId":"8b86b352-4189-4d59-a3fb-b3ba93d61f00","outcome":"Approved","finalPayloadJson":"{\"top\":{\"targetId\":\"c3da4417-8d58-461c-a361-a14fe6f46c38\",\"targetType\":\"Resource\",\"score\":1,\"rationale\":\"Available stock: 10.00; distance: approx. 0 km.\",\"isApproximate\":true},\"inventoryId\":\"c3da4417-8d58-461c-a361-a14fe6f46c38\",\"type\":\"ResourceMatch\",\"quantity\":2,\"requestId\":\"1d000000-0000-0000-0000-000000000006\"}","reason":"Live verification: reviewed the real ResourceMatch candidate and approved two packs.","decidedBy":"0b000000-0000-0000-0000-000000000004","decidedAt":"2026-11-21T17:00:21.36688Z"}
```

- A second decision on the same recommendation returned **HTTP 409**:

```json
{"detail":"A decision has already been recorded for this recommendation. Decision records are immutable."}
```

- `GET /api/v1/requests/1d000000-0000-0000-0000-000000000006` then returned
  HTTP 200 with `status: "Approved"` and a new timeline entry:

```json
{"id":"629ddbf7-0797-4764-b274-3ecb3739b5aa","fromStatus":"Recommended","toStatus":"Approved","actorDisplayName":"Logistics Manager","reason":"Human decision 1f78bcb7-6a1e-4631-afe3-820eabdc579d (Approved) approved the ResourceMatch recommendation.","createdAt":"2026-11-21T17:00:21.4884203"}
```

- `GET /api/v1/recommendations/8b86b352-4189-4d59-a3fb-b3ba93d61f00/decisions`
  returned HTTP 200 with exactly the one decision shown above.

### 4. Case action and critical close gate

The decision endpoint moved the request `Recommended -> Approved`. The request
transition endpoints then returned HTTP 204 for `Approved -> Allocated`,
`Allocated -> Dispatched`, `Dispatched -> Fulfilled` and `Fulfilled -> Closed`.

Closing without a decision returned **HTTP 403**:

```json
{"title":"Forbidden","status":403,"detail":"Closing a Critical request requires an approved CloseCase decision.","code":"CLOSE_DECISION_REQUIRED"}
```

`POST /api/v1/recommendations` with `type: "CloseCase"` returned HTTP 201:

```json
{"id":"d9e8835c-8537-4840-8cc4-0a1d6460391e"}
```

Approving it as `coordinator@example.org` returned HTTP 201:

```json
{"id":"765ae2ce-a7ea-4ec0-ae32-71c5c3c686d3","recommendationId":"d9e8835c-8537-4840-8cc4-0a1d6460391e","outcome":"Approved","finalPayloadJson":"{\"reason\":\"Delivery confirmed by logistics; close recommended.\",\"requestId\":\"1d000000-0000-0000-0000-000000000006\"}","reason":"EmergencyCoordinator approves closure after verified delivery.","decidedBy":"0b000000-0000-0000-0000-000000000002","decidedAt":"2026-11-21T17:00:23.3246184Z"}
```

`POST /api/v1/requests/1d000000-0000-0000-0000-000000000006/transition` with
`to: "Closed"` and `decisionId: 765ae2ce-a7ea-4ec0-ae32-71c5c3c686d3` returned
HTTP 204. The final `GET` returned HTTP 200. Exact field values observed in
that response (the full request DTO is larger) were:

- `status: "Closed"`
- `closedAt: "2026-11-21T17:00:23.5463688"`
- last timeline entry:
  `{"id":"7e759c7c-cccc-4fa2-8f02-b6de543d0a92","fromStatus":"Fulfilled","toStatus":"Closed","actorDisplayName":"Coordinator User","reason":"Approved CloseCase decision 765ae2ce-a7ea-4ec0-ae32-71c5c3c686d3.","createdAt":"2026-11-21T17:00:23.5463688"}`

## Database verification (`sqlcmd`, exit code 0)

- `cases.requests`: request `1d000000-0000-0000-0000-000000000006` is `Closed`,
  `Food`, `Critical`, `closed_at = 2026-11-21 17:00:23.5463688`.
- `cases.request_status_history`: nine rows from `Submitted` through `Closed`,
  including the ResourceMatch recommendation/decision and the CloseCase
  decision in the reason text.
- `ai.recommendations` / `ai.decision_records`: two recommendations for the
  request (`ResourceMatch`, `CloseCase`), each with one `Approved` decision and
  its `final_payload_json`; `DECISION_COUNT_FOR_MATCH_RECO = 1`.
- `ai.recommendations` for the newly submitted request: two rows,
  `Classification` and `Priority`, both `Source = Rule`, `Status = Pending`.
- `common.outbox_messages`: `Request.Submitted` for the new request was
  `Processed` with `retry_count = 0`; six `Notification.Send` rows for the
  target request were `Processed` with `retry_count = 0`.
- `common.notifications`: twelve rows for the request (InApp + Email for
  `request.status.Recommended`, `Approved`, `Allocated`, `Dispatched`,
  `Fulfilled` and `Closed`), all `Sent`.
- `common.audit_events`: ten relevant rows including `Recommendation.Created`,
  `Request.StatusChanged` and `Decision.Recorded` with the expected actor ids.
- Append-only SQL guards present: `TR_recommendations_immutable` and
  `TR_decision_records_immutable`.

## Limitations observed during this run

- Swagger JSON generation returns HTTP 500 because `FilesController.Upload`
  uses `[FromForm]` with `IFormFile`; direct authenticated endpoints were used
  for all verification.
- `backend/db/91_seed_ops.sql` fails in this checkout with
  `Msg 208, Invalid object name 'res.resource_types'`; the M4 naming mismatch is
  documented in `docs/DEV3_REPORT.md` and was worked around only in the isolated
  fixture.
- The real M4 allocation endpoint fails after a valid, decision-authorized call
  with `InvalidOperationException: The configured execution strategy
  'SqlServerRetryingExecutionStrategy' does not support user-initiated
  transactions` from `OperationsController.Allocate`. That defect is outside
  Dev3 scope. The positive decision-gated case action in this run is the
  Critical `CloseCase` transition, which is owned by Dev3 and passed.
- A coordinate-bearing resource fixture causes the M4 resource matcher to make
  a live OSRM HTTP call, which stalled in this environment. The fixture used no
  coordinates and the matcher correctly returned `isApproximate: true`.
