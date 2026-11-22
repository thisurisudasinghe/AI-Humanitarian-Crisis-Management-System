# Report contribution — Member 4: Operations (D4-13 draft)

This section is ready to paste into the group report. Claims are reconciled with the merged working tree on 22 Nov 2026; the testing subsection distinguishes what was actually executed from what is still planned. The component diagram is `Member4_Component_Diagram.mmd`.

## 1. Operations component design

The operations slice is a vertical module inside the ASP.NET Core modular monolith. `OperationsController` exposes `/api/v1` endpoints and applies role/rate-limit attributes; it delegates to one application boundary, `IOperationsService`, implemented by `OperationsService`. All validation, transactions, cross-module calls, audit and outbox writes live in that service. `Cms.Domain` contains entities and the pure Haversine helper only; it has no EF or HTTP dependencies. `Cms.Infrastructure` contains EF configurations, the geo service, the three read-only matchers and adapters that expose the real implementations through the frozen shared contracts.

Cross-module dependencies are explicit interfaces: `IDecisionGate` (Member 3) approves decisions, `IRequestReader` supplies request summaries and `IRequestStatusService` (Member 2) advances request status, while `ICurrentUser`, `IAuditLogger` and `IOutbox` (Member 1) supply identity, audit and notifications. `FrozenOperationAdapters` prevents the foundation's fail-closed geo/matcher stubs from being resolved by other modules. The `/web` operations UI is Member 4's separate handoff app; the active integrated app is under `frontend/`.

Deliverable coverage: D4-01 schemas/EF entities and seeds; D4-02 geo service; D4-03 map components in `/web`; D4-04 resource APIs/matcher; D4-05 allocation guard; D4-06 dispatch lifecycle; D4-07 shelters; D4-08 volunteers/tasks; D4-09 shortage proposal; D4-10 assistance requests; D4-11 operations pages in `/web`; D4-12 seed, tests and Postman collection; D4-13 this report, diagram and reflection. Component diagram, allocation/mapping design and testing are expanded below.

## 2. Allocation and stock-guard design

The body of `POST /api/v1/allocations` contains **only** `decisionId`; the compulsory `Idempotency-Key` is read from the header (missing -> 400 `IDEMPOTENCY_KEY_REQUIRED`, longer than 256 characters -> validation error). The service first re-reads the decision record and returns 403 when it is missing or not Approved/Modified, then calls `IDecisionGate.RequireApprovedAsync(decisionId, ResourceMatch, requestId)`. `requestId`, `inventoryId` and `quantity` are parsed from the approved decision's `FinalPayloadJson`; the client cannot choose stock or quantity. A malformed payload fails closed with `INVALID_DECISION_PAYLOAD`.

A repeated key returns the existing allocation DTO before any further work (200, no second decrement). Otherwise one explicit transaction is opened (relational providers only; an in-memory-safe branch mirrors the logic). The guarded statement is:

```sql
UPDATE res.inventories
   SET available = available - @q,
       allocated = allocated + @q,
       updated_at = SYSUTCDATETIME()
 WHERE id = @inventoryId AND available >= @q;
```

Zero updated rows mean the stock disappeared: the transaction is rolled back and the API returns ProblemDetails 409 with `code: INSUFFICIENT_STOCK`. The allocation row carrying the unique `idempotency_key` and an outbox `Notification.Send` message are inserted inside the same transaction; a duplicate-key failure rolls back, re-reads by key and returns the existing row. The CHECK constraint `available >= 0` (and the other bucket CHECKs) is defence in depth.

Phase 3, the request status transition to `Allocated`, deliberately runs **after commit**. If it fails, the stock change and allocation remain committed, an `Allocation.StatusTransitionFailed` audit row records the operation, entity id, target status, error and retry hint, and the caller still receives the allocation. Recovery is explicit and requires no manual database edit: `GET /api/v1/operations/status-transitions` compares committed operations with current request status and `POST /api/v1/operations/status-transitions/{id}/retry` replays only `IRequestStatusService.TransitionAsync` for that allocation or dispatch id. Repeating the original POST with the same key is intentionally an idempotent read and does not re-run Phase 3, so the recovery endpoint is the documented path.

Shelter allocation uses the identical transaction shape: the `ShelterMatch` decision payload supplies shelter, request and household size; `UPDATE shelter.shelters SET occupancy = occupancy + @n ... WHERE id = @id AND approved = 1 AND occupancy + @n <= capacity` either succeeds or the transaction rolls back to 409 `SHELTER_FULL`. The `Occupancy <= Capacity` CHECK is the database-level backstop. Dispatch moves `Allocated -> Dispatched` and delivery confirmation moves `Dispatched -> Utilised`, both guarded by bucket-availability predicates and both re-verifying the decision gate before the transaction; `Available` is never touched again after allocation. Repeats return the existing row; illegal states return 409 `INVALID_TRANSITION`.

## 3. Matching design

All three rankers are read-only and deterministic (`Score` descending, then `TargetId` ascending), return score 0..1 and a plain-language rationale, and never reserve stock or capacity. Weights are documented in a single comment block in `OperationsService.cs`:

- Resource: type/category fit 0.25, available quantity 0.20, distance 0.15, urgency 0.15, expiry 0.10, accessibility 0.10, org ownership 0.05. Zero-stock and expired inventories are excluded; partial fulfilment is labelled `partial: X of Y`; distance from Haversine sets `IsApproximate=true`.
- Shelter: availability 0.30, suitability 0.25, distance 0.25, services 0.10, org ownership 0.10. Only approved shelters with free places at least the household size are considered.
- Volunteer: skill overlap 0.45, availability fit 0.25, distance 0.20, request context 0.10. Only Verified, active volunteers with a current availability window are considered.

The shortage proposal ranks open requests by urgency, then accessibility need, then age, proposes partial fulfilment from the seeded five-pack stock, and explicitly reports `hasSideEffects: false` and `humanApprovalRequired: true`. The legacy `WeightedResourceMatchScoringStrategy` (60/25/15) remains in `Cms.Infrastructure/Services/Operations/` for its unit test but is not registered by `LogisticsModule`; the runtime uses `OperationsMatchers.cs`.

## 4. Mapping design

`GeoService` fronts Nominatim geocoding and OSRM routing through named `HttpClientFactory` clients with the team User-Agent, configured in `LogisticsModule`. Addresses are whitespace-normalised and the cache key is lower-cased. A successful geocode is cached in `IMemoryCache` for 24 hours; an empty result or any provider failure is cached as a soft miss for at most 1 minute, so a failed lookup is never cached for a day. Nominatim requests are spaced to one per second with a static `SemaphoreSlim` and an injectable `TimeProvider` (enabling a fake-clock test), and no full address is logged.

Distance first tries OSRM (`route/v1/driving`) and caches the successful km (2 decimals) and minutes for 24 hours with `IsApproximate=false`. On any failure it returns the pure Haversine distance with `Minutes=null` and `IsApproximate=true`, cached for 1 minute. Provider exceptions never escape to the caller: geocoding returns a null point (HTTP 404 `GEOCODE_NOT_FOUND`) and distance always returns a usable value. The anonymous UI map uses `MapPicker` for click-to-pin plus manual coordinate fallback and `MapView` for approved-shelter markers; all calls go through the API, never directly to third parties.

## 5. Testing and evidence

Executed on 22 Nov 2026 from `backend` with .NET SDK 11.0.100:

- `dotnet build Cms.sln` — **succeeded: 0 warnings, 0 errors**. Evidence: `docs/evidence/dev4/build.log`.
- `dotnet test Cms.sln --no-build` — **Test Run Successful: 561 total, 561 passed**. Evidence: `docs/evidence/dev4/test.log`.

Member 4-related tests currently in that suite: the older `OperationsGeoTests.cs` (same-point Haversine, Negombo–Colombo range, invalid coordinates), `Infrastructure/HaversineTests.cs` (approximate fallback shape against the stub), and `OperationsScoringTests.cs` (deterministic legacy scoring), plus 53 tests under `backend/tests/Cms.Tests/Operations/`: `OperationsGeoAdapterTests` (7 fake-HTTP geocode/OSRM caching, fallback, coordinate and User-Agent tests), `OperationsMatcherTests` (6), `OperationsServiceTests` (16), `OperationsValidationTests` (20) and `OperationsShortageTests` (4). These are genuine passes and the fake-HTTP tests now exercise the real `GeoService` cache/fallback paths, but they use in-memory EF and fake handlers: they do not exercise SQL transactions, LocalDB concurrency or real timer-driven throttling.

**Planned / NOT claimed as passed.** `sqllocaldb info` lists an `MSSQLLocalDB` instance, but no real-transaction concurrency harness is checked in and no race was executed for this draft. The following still need to be implemented, executed and attached as evidence before the remaining D4-12 acceptance items are claimed (items 1–5 should be repeated 20 times each):

1. Parallel allocations of the last stock unit: exactly one 201 and one 409 `INSUFFICIENT_STOCK`; `Available >= 0`.
2. Parallel shelter allocations of the last place: exactly one success and one 409 `SHELTER_FULL`; `Occupancy <= Capacity`.
3. Same `Idempotency-Key` sequential and concurrent: one allocation row, both callers receiving 200.
4. Simulated Phase 3 status failure: allocation committed, audit/hint present, `POST /operations/status-transitions/{id}/retry` completes the transition without touching stock.
5. Dispatch/delivery chain leaves `Available + Reserved + Allocated + Dispatched + Utilised` consistent.
6. Real-timer proof of the 1 req/s Nominatim throttle. The fake-HTTP tests already cover the 24 h success cache, <=1 min failure cache, OSRM-down Haversine fallback, configured User-Agent and no Nominatim call from `GetDistanceAsync`, but they do not measure elapsed request spacing.
7. Live Postman/Newman run of `Member4_Operations.postman_collection.json` and the shortage demo against `04`→`06`, `90` and `91` seeds.

No pass result, screenshot or log is asserted for those seven items. The Postman collection (41 requests; all 40 owned P1 endpoints plus a negative idempotency-key probe) includes tests for 409 `INSUFFICIENT_STOCK`/`SHELTER_FULL`, 403 decision/guard failures, ProblemDetails `code`, the no-side-effect shortage proposal and the anonymous PII-free public shelter projection; execution evidence is still to be captured.

## 6. Individual reflection

See `docs/Member4_INDIVIDUAL_REFLECTION.md`.
