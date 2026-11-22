# Final Integration Evidence — 2026-11-22

Branch: `feat/public-ui-polish`  
Environment: isolated LocalDB database `CmsDb_IntegrationTest`; API on `http://127.0.0.1:5211`; frontend on `http://127.0.0.1:3101` (built with `NEXT_PUBLIC_API_URL` pointing to 5211). The shared `CmsDb` was not modified.

## Environment / safety

- Schema created only from the canonical `backend/db` scripts, in README order, excluding stale `07_geo.sql`: `00_common`, `01_cases`, `02_notif`, `03_ai`, `04_res`, `05_shelter`, `06_vol`, `08_don`, `98_foreign_keys`, `99_seed_users`. All exited 0; no EF migration was run.
- Synthetic data limited to `90_seed_scenario.sql` and `91_seed_ops.sql` (both exited 0) plus one registered test citizen and test workflow records.
- API started with `Outbox__ProcessorEnabled=false`, `Smtp__Host` empty (log sender), `Cors__AllowedOrigins__0=http://127.0.0.1:3101`. Startup log confirmed: `Outbox processor is disabled by configuration.`
- Result: 8 schemas, 40 tables (11 in `cases`), 27 foreign keys, 0 disabled constraints. `GET /api/health` returned `200 {"status":"Healthy",...}`.

## Critical workflow evidence

| Step | Evidence | Result |
|---|---|---|
| Citizen registration | `POST /api/v1/auth/register` → 201; `/auth/me` → 200; `PUT /households/me` → 200; duplicate registration → 409 | PASS |
| Citizen login / profile | Demo role logins → 200 with correct roles; inactive user → 401 | PASS |
| Request submission | `POST /api/v1/requests` → 201 with new id; citizen detail → `Submitted` | PASS |
| Review / prioritisation | Transition `Verified` → 204, assign → 204, `Triaged` → 204; staff queue and filters → 200 | PASS |
| AI recommendation | `GET /recommendations/match/resources/{id}` → 200 ranked candidates; `POST /recommendations/match` (`ResourceMatch`, `ShelterMatch`, `VolunteerMatch`) → 200 | PASS |
| Human approval / override | Citizen decision attempt → 403; logistics approval → 201; repeated decision → 409; status became `Approved` | PASS |
| Resource allocation | `POST /allocations` → 201; idempotent replay with same key → 201 with same allocation id; insufficient stock → `409 INSUFFICIENT_STOCK` | PASS |
| Shelter allocation | Approved `ShelterMatch` decision → 201; `POST /shelters/allocations` → 201 | PASS |
| Status completion | `Allocated`, `Dispatched`, `Fulfilled`, `Closed` → 204 each; invalid transition → `409 INVALID_TRANSITION`; timeline → 9 entries | PASS |
| Notifications / outbox | 9 `Notification.Send` and 1 `Request.Submitted` outbox rows with distinct idempotency keys; 0 inbox rows and 0 dead letters while processor disabled | PASS (processor intentionally off) |
| Audit | `GET /audit-events?EntityId=...` → 11 matching events (`Request.Submitted`, `Request.Assigned`, `Request.StatusChanged`, `Request.FeedbackSubmitted`) | PASS |
| Feedback | `POST /requests/{id}/feedback` → 200 with new id | PASS |
| Volunteer management | Task create → 201; assign → 200 | PASS |
| Donations | Donor, campaign, monetary donation → 201 each; KPI export → 200 | PASS |
| Maps / geo | `GET /geo/distance` → 200; Nominatim geocode returned 403 externally and API mapped it to `404 GEOCODE_NOT_FOUND` (external-provider limitation) | PARTIAL |
| RBAC / security | Anonymous protected endpoints → 401; citizen on staff/admin routes → 403; `role-check` 200 for staff only; invalid category → `422 VALIDATION_FAILED` | PASS |
| Frontend/API integration | UI login → `/dashboard`; `/my-reports` showed 13 reports matching DB; no console errors | PASS |

## Tests

- Targeted xUnit (outbox processor, email handler, request-submitted AI handler, match service, notification endpoints): 21 passed, 0 failed.
- Full backend suite (`dotnet test --no-build -c Debug --framework net10.0`): **586 passed, 0 failed, 0 skipped**.
- `npm --prefix frontend run typecheck`: passed.
- `npm --prefix frontend run build`: passed, 34 routes.
- Earlier filtered integration suites: 440 passed, 0 failed.

## Fixes / limitations

- Fixed stale `docs/TESTING.md` SQL command list: it omitted `01_cases.sql`, `04_res.sql`, `05_shelter.sql`, `06_vol.sql`, and the pre-API seed ordering — the likely cause of the incomplete default `CmsDb` that produced `Invalid object name 'cases.requests'`.
- No application source code changes were required; the previously documented M4 allocation transaction defect did not reproduce in this build.
- Live citizen inbox delivery and outbox-driven duplicate detection remain unverified in this session because the outbox processor was disabled by configuration as required; both are covered by targeted xUnit tests and persisted outbox rows.
- External geocoding failed with HTTP 403 from Nominatim; the API handled it without a 500.
