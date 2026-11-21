# Member 4 — Operations contribution

## Ownership
This contribution implements the Member 4 vertical slice in the workload plan: M5 Resources, M6 Shelters, M7 Volunteers and M10 Mapping. It deliberately leaves Member 1's foundation, Member 2's case workflow and Member 3's AI/governance code intact.

## Backend additions
- Resource types, inventory buckets, allocation and dispatch records, shelter capacity/occupancy, volunteer profiles/availability/tasks/activity and geocode cache entities.
- Operations API endpoints for inventory, approved-decision allocations, dispatch/delivery confirmation, shelters, volunteer task lifecycle, assistance requests, shortage proposals and mapping.
- Allocation is gated by an existing approved/modified `ResourceMatch` decision. Inventory is changed with a conditional SQL `UPDATE` inside a transaction. Shelter occupancy uses a conditional `UPDATE` guarded by capacity inside a transaction.
- Nominatim geocoding uses a 24-hour success cache and a one-minute failure cache. OSRM route distance is attempted first; Haversine is the fallback and returns `IsApproximate=true` and no travel minutes.
- Matchers return deterministic ranked candidates and do not write or reserve stock/capacity. Resource ranking uses an injectable weighted Strategy (60% available quantity, 25% expiry suitability, 15% reorder-threshold health).

## Frontend additions
A Next.js App Router starter under `/web` includes logistics, shelter, volunteer and map pages plus reusable Leaflet `MapPicker` and `MapView` components. The lightweight API helper is an integration bridge; merge it with Member 1's token-refreshing shared client when that shell is available.

## Database additions
- `backend/db/04_res.sql`
- `backend/db/05_shelter.sql`
- `backend/db/06_vol.sql`
- `backend/db/07_geo.sql`
- `backend/db/91_seed_ops.sql`

The current repository contains `00_foundation.sql` and `98_foreign_keys.sql`, but does not contain Member 2's `90_seed_scenario.sql` or a completed Member 1 seed-users script. The limited inventory seed uses a stable five-unit food-pack stock record; coordinate its resource type and GUID with Member 2 before the shortage demonstration. The seeded volunteer records use fixed user GUIDs and must be associated with actual seeded Volunteer users before testing self-service login.

## Integration contracts / items to confirm with the team
1. The shared contract describes `IRequestReader`, `IRequestStatusService`, `IDecisionGate`, `ICurrentUser` and `IAuditLogger`; the checked-in repository currently has different interfaces (`ICurrentUserService`, `IAuditService`) and no `IRequestStatusService`/`IDecisionGate`. The current controller verifies the approved decision against `DecisionRecords` and `Recommendations`, and does not independently transition the case status. Integrate the status transition through Member 2's service once that contract is merged.
2. The checked-in `Request` entity does not contain `Lat`, `Lng`, household size or accessibility needs. Therefore the matchers currently return deterministic inventory/shelter/volunteer candidates using available stock, free capacity and verified availability, while explicitly describing missing location/need signals. Complete weighted distance/accessibility scoring after the request summary contract is available.
3. The current project already contains EF Core migrations, while the planning document lists plain SQL as an assumption pending team agreement. This contribution adds the agreed Member 4 SQL scripts without deleting or rewriting existing migrations. Confirm database setup order with Member 1.
4. Organisation-scoped authorization needs the `orgId` identity claim / current-user contract. The current `ICurrentUserService` does not expose it. Assistance-request endpoints are role-gated but need the agreed org-scope service before production/UAT.

## Tests and verification
`Cms.Tests/OperationsGeoTests.cs` covers Haversine same-point, approximate distance and invalid-coordinate cases. SQL concurrency tests still need to be run against SQL Server/LocalDB; the execution environment used to prepare this ZIP did not have the .NET SDK available, so no successful `dotnet build` or `dotnet test` result is claimed.


## Additional functional limitations to resolve before final UAT
- The shortage proposal currently assumes one unit per request because the committed Request entity has no requested-quantity field; urgency and age are used, but accessibility/vulnerability ranking still needs the Member 2 request contract.
- Allocation and shelter-allocation currently validate decision outcome/type/payload in the Member 4 controller. Replace this local check with Member 3's `IDecisionGate` when that interface is merged so decider-role policy is centrally enforced.
- Assistance-request notifications through the outbox and strict source/target organisation scoping are not complete because the checked-in `ICurrentUserService` has no `OrgId` and the agreed `IOutbox` contract differs from the checked-in one.
