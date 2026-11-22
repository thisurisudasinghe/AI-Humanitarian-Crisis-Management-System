# Member 4 file manifest

## Backend API
- `backend/src/Cms.Api/Controllers/OperationsController.cs`

## Application contracts
- `backend/src/Cms.Application/Contracts/Operations/GeoContracts.cs`

## Domain entities and rules
- `backend/src/Cms.Domain/Entities/Operations/ResourceType.cs`
- `backend/src/Cms.Domain/Entities/Operations/Inventory.cs`
- `backend/src/Cms.Domain/Entities/Operations/Allocation.cs`
- `backend/src/Cms.Domain/Entities/Operations/Dispatch.cs`
- `backend/src/Cms.Domain/Entities/Operations/Shelter.cs`
- `backend/src/Cms.Domain/Entities/Operations/ShelterAllocation.cs`
- `backend/src/Cms.Domain/Entities/Operations/AssistanceRequest.cs`
- `backend/src/Cms.Domain/Entities/Operations/Volunteer.cs`
- `backend/src/Cms.Domain/Entities/Operations/VolunteerAvailability.cs`
- `backend/src/Cms.Domain/Entities/Operations/VolunteerTask.cs`
- `backend/src/Cms.Domain/Entities/Operations/VolunteerActivity.cs`
- `backend/src/Cms.Domain/Entities/Mapping/GeoCache.cs`
- `backend/src/Cms.Domain/Operations/GeoDistance.cs`

## Infrastructure
- `backend/src/Cms.Infrastructure/Services/Operations/GeoService.cs`
- `backend/src/Cms.Infrastructure/Services/Operations/OperationsMatchers.cs`\n- `backend/src/Cms.Infrastructure/Services/Operations/WeightedResourceMatchScoringStrategy.cs`
- `backend/src/Cms.Infrastructure/Services/Operations/OperationsModuleExtensions.cs`

## Database scripts
- `backend/db/04_res.sql`
- `backend/db/05_shelter.sql`
- `backend/db/06_vol.sql`
- `backend/db/07_geo.sql`
- `backend/db/91_seed_ops.sql`

## Tests
- `backend/tests/Cms.Tests/OperationsGeoTests.cs`\n- `backend/tests/Cms.Tests/OperationsScoringTests.cs`

## Frontend
- `web/package.json`
- `web/tsconfig.json`
- `web/next-env.d.ts`
- `web/README.md`
- `web/lib/api.ts`
- `web/app/layout.tsx`
- `web/app/style.css`
- `web/app/(logistics)/logistics/page.tsx`
- `web/app/(logistics)/shelters/page.tsx`
- `web/app/(volunteer)/volunteer/page.tsx`
- `web/app/maps/page.tsx`
- `web/components/map/MapPicker.tsx`
- `web/components/map/MapView.tsx`

## Documentation and API test collection
- `docs/Member4_Operations_Contribution.md`
- `docs/Member4_Component_Diagram.mmd`
- `docs/Member4_File_Manifest.md`\n- `docs/Member4_Report_Section_Draft.md`
- `docs/postman/Member4_Operations.postman_collection.json`

## Existing files updated
- `backend/src/Cms.Infrastructure/Data/ApplicationDbContext.cs` — DbSets and schema mappings for Member 4 entities.
- `backend/src/Cms.Api/Program.cs` — registers `AddOperationsModule()`.
- `backend/tests/Cms.Tests/Cms.Tests.csproj` — references the Domain project for the new unit tests.
- `README.md` — documents the operations module, scripts and integration notes.

The existing Member 1, Member 2 and Member 3 source files were otherwise left unchanged.
