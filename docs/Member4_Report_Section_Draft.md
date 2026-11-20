# Report contribution — Member 4: Operations

> This is a report-ready technical draft for the Member 4 contribution. Replace the evidence notes with screenshots, actual SQL Server test outputs and your own reflection before submission. No test pass result is claimed unless you run it.

## 1. Operations component design

The Operations vertical slice is part of the system's modular-monolith architecture. The Next.js operations pages call ASP.NET Core `/api/v1` endpoints. Controllers validate transport input and role access, while EF Core persists domain entities in the `res`, `shelter`, `vol` and `geo` schemas. Mapping calls are isolated behind `IGeoService`; resource, shelter and volunteer matching are exposed through separate interfaces so the AI/governance module can request candidates without directly changing stock or capacity.

The principal responsibilities are:
- **M5 Resources:** resource types, inventory balances, matching candidates, allocation, dispatch, fulfilment and shortage proposals.
- **M6 Shelters:** approved shelter publication, capacity and occupancy, shelter matching and decision-gated household allocation.
- **M7 Volunteers:** volunteer profiles, verification, availability, task assignment, acceptance/decline and completion activity.
- **M10 Mapping:** geocoding, route distance, cache and approximate straight-line fallback.

The component diagram is provided in `Member4_Component_Diagram.mmd`.

## 2. Allocation and stock-guard design

The allocation API accepts an approved `decisionId` and an `Idempotency-Key` header. It reads the recommendation and decision record, requires an `Approve` or `Modify` outcome, checks that the recommendation is a `ResourceMatch`, and validates the request identifier in the approved final payload. Inventory identity and quantity are read from that payload rather than trusted from the client.

The inventory update is a conditional database statement:

```sql
UPDATE res.Inventories
SET Available = Available - @quantity,
    Allocated = Allocated + @quantity
WHERE Id = @inventoryId
  AND Available >= @quantity;
```

The update and allocation-row insert execute inside one database transaction. If zero rows are updated, the transaction is rolled back and the API returns `409 INSUFFICIENT_STOCK`. A unique index on `IdempotencyKey` prevents duplicate allocation rows for a repeated key. Dispatch transfers quantity from `Allocated` to `Dispatched`; delivery confirmation transfers it from `Dispatched` to `Utilised`.

Shelter allocation follows the same database-guard principle. The approved `ShelterMatch` decision supplies shelter identity, request identity and household size. The occupancy update succeeds only when `Occupancy + @householdSize <= Capacity`; otherwise the transaction is rolled back and the API returns `409 SHELTER_FULL`. The database check constraint `Occupancy <= Capacity` provides a second line of defence.

**Integration qualification:** the current checked-in Member 2 contract does not expose `IRequestStatusService`, so the current Member 4 controller does not transition request status after allocation/fulfilment. The controller performs a local decision outcome/type/payload check; it must be replaced by Member 3's `IDecisionGate` when merged so the decider-role policy is centrally enforced. These handoffs must be connected before end-to-end UAT.

## 3. Matching strategy

Resource matching uses an injectable Strategy implementation, `WeightedResourceMatchScoringStrategy`. It is deterministic and uses the fields currently present in the inventory entity:
- 60% available quantity ratio;
- 25% expiry suitability;
- 15% reorder-threshold health.

The matcher returns a score and human-readable rationale and does not reserve or allocate stock. Shelter recommendations rank approved shelters with free capacity. Volunteer recommendations include only active, verified volunteers with a current availability window.

**Data limitation:** the current checked-in request entity does not contain request coordinates, household size, accessibility requirements or requested resource quantity. The present matcher therefore cannot honestly calculate full distance/accessibility/household suitability. The shortage proposal temporarily assumes one unit per request and ranks by urgency then age; this is not sufficient for the required 3-urgent-requests shortage demo. Complete quantity and multi-constraint scoring after the shared request-summary contract is available.

## 4. Mapping design

`GeoService` isolates external mapping providers behind `IGeoService`. Geocoding uses Nominatim and caches successful results for 24 hours; failed lookups are cached for one minute. Requests are spaced to respect the Nominatim one-request-per-second guidance. Distance first calls OSRM and returns route distance and estimated minutes. If OSRM is unavailable or fails, the service calculates Haversine distance and returns `IsApproximate = true` with `Minutes = null`.

The UI uses Leaflet with OpenStreetMap tiles. `MapPicker` captures a selected coordinate; `MapView` displays the approved shelter markers. Manual latitude/longitude remains a fallback when geocoding is unavailable.

## 5. Testing and evidence

Automated unit tests added:
- Haversine distance for the same point;
- approximate Negombo-to-Colombo distance range;
- rejection of invalid latitude;
- deterministic weighted resource scoring.

Run from `backend`:

```powershell
dotnet test Cms.slnx
```

Run the SQL Server concurrency tests before claiming the advanced requirement is satisfied. The required evidence is:
1. Repeat a parallel allocation of the last available unit 20 times: exactly one succeeds and one receives `409 INSUFFICIENT_STOCK`; `Available` never becomes negative.
2. Repeat parallel shelter allocations for the last free place 20 times: exactly one succeeds and one receives `409 SHELTER_FULL`; `Occupancy` never exceeds `Capacity`.
3. Submit the same idempotency key sequentially and concurrently and confirm only one allocation row is created.
4. Disable OSRM and confirm Haversine fallback returns `IsApproximate=true` and `Minutes=null`.
5. Disable Nominatim and confirm geocoding fails softly without blocking other request workflows.

**Evidence to insert after execution:** Postman screenshots, SQL before/after inventory bucket values, shelter occupancy values, and test-run output. The .NET SDK and SQL Server/LocalDB were not available in the environment used to prepare this ZIP, so the new code has not been build- or runtime-verified here.

## 6. Individual reflection — personalize before submission

Use this as a starting point and rewrite it to accurately describe your own work:

> My contribution focused on the Operations modules: resources, shelters, volunteers and mapping. I worked across the domain model, API, database schema, external mapping adapter and Next.js operations interface. A key architectural concern was preventing concurrent operations from allocating more stock or shelter capacity than the database actually has. I used conditional SQL updates and transaction boundaries so that these constraints are enforced by the database rather than relying on UI checks. I also separated mapping and matching behind interfaces to keep external-provider failures and matching strategies isolated from the API controllers. The main integration challenge was aligning the operations workflow with the request, decision and identity contracts owned by the other team members. I would improve the implementation by completing the shared request-status and organisation-scoping contracts, then running the full SQL Server concurrency and end-to-end tests.

Replace this paragraph with your actual challenges, decisions, tests performed and lessons learned.
