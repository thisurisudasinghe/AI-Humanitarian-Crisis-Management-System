# SAFEWAY Coursework Compliance Matrix

Repository: `AI-Humanitarian-Crisis-Management-System`  
Branch reviewed: `final/professionalization-compliance` (based on `develop` at `2e00edb2e17eac26eda43f0ba1f3cd7c9b0e3357` before this pass)  
Source of truth: `C:\Users\User\Documents\Y2 S2\SA\SE205.3_Humanitarian_Crisis_Coursework_2026.docx` (extracted to `C:\Users\User\AppData\Local\Temp\opencode\coursework.txt` during review)

Status legend:

- **PASS** - implemented and supported by repository/test/evidence references.
- **PARTIAL** - implemented in part, or evidence is incomplete for a full claim.
- **MISSING** - not implemented or no evidence.

This matrix is evidence-based. Items are not marked PASS without implementation or evidence references.

## §1.1 Core Platform Components and Features

| Requirement | Implementation | API/UI | Test/Evidence | Demo | Status |
|---|---|---|---|---|---|
| A. Registration/authentication | `backend/src/Cms.Api/Controllers/AuthController.cs`, `backend/src/Cms.Infrastructure/Auth/*`, `frontend/app/login/page.tsx`, `frontend/app/register/page.tsx` | `/api/v1/auth/register`, `/login`, `/refresh`, `/me` | `backend/tests/Cms.Tests/Integration/AuthFlowTests.cs`; `docs/evidence/final-integration-20261122.md` registration/login rows | Register or login with demo account | PASS |
| A. Household/individual profile | `backend/src/Cms.Api/Controllers/HouseholdsController.cs`, `frontend/app/(app)/account/page.tsx` | `GET/PUT /api/v1/households/me`; Account page household form for citizens | `backend/tests/Cms.Tests/Cases/HouseholdServiceTests.cs` | Login as citizen, open Account, save household profile | PASS |
| A. Submit assistance request with category/urgency/description/location/evidence | `frontend/components/safeway/ReportFlow.tsx`, `frontend/lib/crisis.ts`, `backend/src/Cms.Api/Controllers/RequestsController.cs` | `POST /api/v1/requests`, `POST /api/v1/requests/{id}/attachments`; `/report` | `backend/tests/Cms.Tests/Cases/RequestServiceTests.cs`, `AttachmentServiceTests.cs`; final integration evidence request creation 201 | Citizen submits report with coordinates and optional evidence | PASS |
| A. Request food/medicine/shelter/transport/accessibility/other | `frontend/components/safeway/ReportFlow.tsx`, `frontend/lib/crisis.ts`, `backend/src/Cms.Domain/Enums/Cases/Category.cs` | Report category selector maps to backend enum values | `RequestServiceTests.cs` category validation; UI typecheck/build in final verification | Select each service category in report flow | PASS |
| A. Track status and notifications | `frontend/app/(app)/my-reports/page.tsx`, `frontend/app/(app)/notifications/page.tsx`, `backend/src/Cms.Api/Controllers/NotificationsController.cs` | `/api/v1/requests/mine`, `/api/v1/notifications`; My Reports/Notifications UI | `NotificationEndpointTests.cs`; final integration evidence status/inbox rows | Citizen checks My Reports and Notifications | PASS |
| A. Public alerts/approved shelters/service centres/safety guidance | `backend/src/Cms.Api/Controllers/PublicController.cs`, `frontend/components/safeway/PublicCaseUpdates.tsx`, `frontend/app/(public)/find-assistance/page.tsx`, `frontend/app/(app)/operations/map/page.tsx` | `/api/v1/public/alerts`, `/api/v1/public/service-centres`, `/api/v1/shelters/public`, map endpoints | `CrisisServiceTests.cs`; Postman Member4 public shelters; live map checks in final evidence | Open Find Assistance / operations map | PARTIAL (public alerts/service centres wired; approved shelter public listing is staff/map oriented and can be made more explicit) |
| A. Multilingual communication and AI support interface | `backend/src/Cms.Api/Controllers/AiController.cs`, `backend/src/Cms.Infrastructure/Services/AI/AiChatService.cs`, `frontend/app/(app)/operations/ai/chat/page.tsx` | `/api/v1/ai/chat`; FAQ/chat UI | `AiChatServiceTests.cs`, `NotificationTemplateTests.cs` | Staff opens AI FAQ; citizen-facing AI UI remains limited | PARTIAL (staff FAQ and en/es notification templates exist; citizen-facing AI support interface is not fully exposed) |
| A. Feedback after assistance | `backend/src/Cms.Api/Controllers/RequestsController.cs`, `frontend/app/(app)/my-reports/page.tsx` | `POST /api/v1/requests/{id}/feedback` | Final integration evidence feedback 200; request service allows Fulfilled/Closed | Citizen submits feedback on closed/fulfilled report | PASS |
| B. Create/manage crises and operational areas | `backend/src/Cms.Api/Controllers/CrisesController.cs`, `AreasController.cs`, `frontend/app/(app)/operations/crises/*` | `/api/v1/crises`, `/api/v1/areas` | `CrisisServiceTests.cs`; seed scenario; final integration evidence | Coordinator opens Operations > Crises | PASS |
| B. Review incoming requests/supporting information | `RequestsController.cs`, `frontend/app/(app)/operations/crises/[id]/page.tsx`, `frontend/app/(app)/my-reports/page.tsx` | Request list/detail/attachments endpoints | Final integration evidence staff queue 200 and UI parity | Staff reviews queue and request detail | PASS |
| B. Classify/prioritise/escalate/assign/merge/close | `RequestService.cs`, `RequestStatusService.cs`, `RequestStateMachine.cs` | Verify/assign/triage/status endpoints | `RequestStateMachineTests.cs`, `RequestStatusServiceTests.cs`; final integration transitions | Coordinator verifies, assigns, triages, closes | PASS |
| B. AI urgency/priority with confidence and rationale | `backend/src/Cms.Infrastructure/Services/AI/RuleBasedProvider.cs`, `RecommendationService.cs` | `/api/v1/recommendations/*`, match endpoints | `RuleBasedProviderTests.cs`; final integration match/recommendation 200 | Staff requests recommendations for request | PASS |
| B. Monitor open/overdue/escalated/unresolved | `RequestsController.cs`, `KpiService.cs`, operations UI | Queue filters and `/api/v1/kpis` | `KpiEndpointTests.cs`, `KpiServiceTests.cs` (this pass) | Open KPI dashboard and request queue | PASS |
| B. Coordinate across partner organisations | `OperationsController.cs`, `OperationsService.cs` assistance requests | `/api/v1/assistance-requests` | `OperationsServiceTests.cs`; Postman Member4 collection | Create/respond assistance request | PARTIAL (API present; not a prominent end-to-end UI flow) |
| B. Operational summaries/workload/resource shortages | `KpiService.cs`, operations/resources pages | `/api/v1/kpis`, `/api/v1/shortages/proposal` | `KpiServiceTests.cs`; `OperationsShortageTests.cs` | Open KPI and resources shortage panel | PASS (core metrics live; advanced workload visualisation limited) |
| B. Complete timeline | `RequestService.cs` status history, UI case detail | Request detail/timeline endpoints | Final integration timeline 9 entries | Open case detail timeline | PASS |
| C. Automatic classification/priority scoring | `RuleBasedProvider.cs` | Recommendation endpoints | `RuleBasedProviderTests.cs` | Submit request and inspect recommendation | PASS |
| C. Duplicate/near-duplicate detection | `RuleBasedProvider.cs`, `RequestSubmittedHandler.cs` | Recommendation records/candidate rationale | `RuleBasedProviderTests.cs` | Submit similar requests | PASS |
| C. Resource-to-request matching | `OperationsService.cs`, `OperationsMatchers.cs` | `/api/v1/recommendations/match/resources/{id}` | Final integration match 200; operations tests | Run resource match | PASS |
| C. Shelter recommendation | `OperationsService.cs` | `/api/v1/recommendations/match/shelters/{id}` | Final integration shelter match 200 | Run shelter match | PASS |
| C. Volunteer-task matching | `OperationsService.cs` | `/api/v1/recommendations/match/volunteers/{id}` | Final integration volunteer match 200 | Run volunteer match | PASS |
| C. Demand forecasting for selected resource categories | No dedicated forecasting service/UI found | - | - | - | MISSING (advanced dashboard optional; basic forecasting not implemented) |
| C. Suspicious/anomalous request detection | `RuleBasedProvider.cs`, donation flagging services | Recommendation/donation flag endpoints | `RuleBasedProviderTests.cs`, donation tests | Review anomaly/suspicious flags | PASS |
| C. Multilingual response/notification generation | `NotificationTemplates.cs`, `02_notif.sql` | Notification template language | `NotificationTemplateTests.cs` | Send notification in supported language | PARTIAL (en/es templates; full UI localization incomplete) |
| C. AI-generated crisis summaries | `CrisisService.GetSummaryAsync`, crisis UI | Crisis summary endpoint/UI | `CrisisServiceTests.cs` | Open crisis detail summary | PASS |
| D. Approve/modify/reject/defer AI recommendations | `DecisionService.cs`, `DecisionRolePolicy.cs` | `POST /api/v1/recommendations/{id}/decision` | `DecisionServiceTests.cs`, governance endpoint tests; final integration 403/201/409 | Staff records decision | PASS |
| D. Record recommendation, confidence, human decision, timestamp, responsible user | `DecisionRecord` entity, `DecisionService.cs` audit payload now includes confidence | Decision/audit endpoints | `DecisionServiceTests.cs` updated for confidence; audit evidence | Approve recommendation, inspect audit | PASS |
| D. Role-appropriate approval for critical actions | `DecisionRolePolicy.cs`, `DecisionGate.cs` | Decision/allocation endpoints | Final integration citizen 403/logistics 201 | Attempt decision as citizen/staff | PASS |
| D. Post-incident review | `PostIncidentReviewService.cs`, operations decisions UI | `/api/v1/post-incident-review`, `/reviews/decisions` | `PostIncidentReviewServiceTests.cs` | Open decisions review | PASS |
| D. Prevent unauthorised automation | `DecisionGate.cs`, FAQ chat informational | Allocation/dispatch gated by human decision | `DecisionGateTests.cs`, `AiChatServiceTests.cs` | Attempt allocation without decision | PASS |
| E. Manage resource types/inventory | `OperationsController.cs`, resources UI | `/api/v1/resources/types`, `/inventories` | Dev4 SQL guards/API evidence; operations tests | Logistics manages inventory | PASS |
| E. Track quantity/availability/allocation/dispatch/utilisation/replenishment | `OperationsService.cs`, resource schema | Allocation/dispatch/confirm endpoints | Final integration allocation/replay/dispatch; SQL guards | Allocate and dispatch stock | PASS |
| E. Multi-constraint resource matching | `OperationsMatchers.cs` | Match endpoints | `OperationsMatcherTests.cs` | Run resource match | PASS |
| E. Shortages/conflicts | `OperationsService.cs` shortages + insufficient stock | `/api/v1/shortages/proposal`, allocation 409 | `OperationsShortageTests.cs`; final integration 409 | Attempt over-allocation | PARTIAL (shortage proposal and 409 exist; full partial-fulfilment execution workflow limited) |
| E. Partner assistance requests | `OperationsService.cs`, assistance endpoints | `/api/v1/assistance-requests` | `OperationsServiceTests.cs` | Create assistance request | PARTIAL |
| E. Alternative allocation recommendations | Match endpoints return ranked candidates | Match APIs | Final integration match 200 | View ranked match results | PASS |
| F. Register/manage shelters and centres | `OperationsController.cs`, shelters UI | `/api/v1/shelters` | Dev4 evidence, operations tests | Shelter admin creates/edits shelter | PASS |
| F. Capacity/occupancy/facilities/accessibility/services | Shelter entity/DTO/UI | Shelter endpoints | SQL shelter guard 20/20 evidence | View shelter details | PASS |
| F. Allocate households by capacity/suitability | `OperationsService.cs` | Shelter allocation endpoint | Final integration shelter allocation 201 | Approve shelter match and allocate | PASS |
| F. Prevent over-allocation | DB CHECK + guarded update | Shelter allocation conflict | SQL guard, final integration `SHELTER_FULL`/conflict evidence | Attempt over-capacity allocation | PASS |
| F. Request more resources when insufficient | Assistance request endpoints | `/api/v1/assistance-requests` | `OperationsServiceTests.cs` | Shelter requests assistance | PARTIAL (API present) |
| G. Volunteer registration/skills/availability | Volunteer endpoints/UI | `/api/v1/volunteers/profile`, `/availability` | Operations tests, final integration | Volunteer saves profile/availability | PASS |
| G. Task assignment/acceptance | Volunteer task endpoints | `/api/v1/volunteer-tasks` | Final integration create/assign 201/200 | Coordinator assigns task | PASS |
| G. Verification and RBAC | `OperationsController.cs`, volunteer verification | Verify volunteer endpoint | Operations tests; role checks | Coordinator verifies volunteer | PASS |
| G. AI-assisted volunteer-task matching | Match endpoint | Volunteer match endpoint | Final integration 200 | Run volunteer match | PASS |
| G. Activity/completion records | `OperationsService.cs` | Complete task endpoint | `OperationsServiceTests.cs` | Volunteer completes task | PASS |
| H. Donor/donation management | `DonationsController.cs`, `DonationService.cs`, donations UI | `/api/v1/donors`, `/campaigns`, `/donations` | Donation endpoint tests; final integration 201s | Create donor/campaign/donation | PASS |
| I. Email notifications | `EmailNotificationChannel.cs`, log sender | Outbox/notification pipeline | `EmailNotificationHandlerTests.cs`; final integration email rows sent | Trigger notification with processor enabled | PASS (demo uses log sender; no real email) |
| I. In-app notifications | `InAppNotificationChannel.cs`, notifications UI | `/api/v1/notifications` | `NotificationEndpointTests.cs`; final integration | Citizen opens Notifications | PASS |
| I. SMS notifications | No SMS channel | - | - | - | MISSING (optional where supported; not implemented) |
| I. Push notifications | No push channel | - | - | - | MISSING (optional where supported; not implemented) |
| I. Emergency announcements | `AnnouncementsController.cs`, `AnnouncementService.cs` | `/api/v1/announcements` | `AnnouncementServiceTests.cs` | Admin/coordinator publishes announcement | PASS |
| I. Multilingual communication support | Notification templates en/es | Notification template records | `NotificationTemplateTests.cs` | Create/send notification with language | PARTIAL |
| J. Location capture for requests | `ReportFlow.tsx` with MapPicker, lat/lng required | `/report` | Frontend typecheck/build; manual map interaction | Citizen selects point on map | PASS |
| J. Interactive maps for shelters/service centres/areas | `frontend/app/(app)/operations/map/page.tsx`, map components | `/api/v1/operations/areas`, shelters/service centres | Final integration map checks | Staff opens Operations map | PASS |
| J. Distance/route through external mapping provider | `GeoService`, OSRM/Nominatim integration | `/api/v1/geo/distance`, `/geo/geocode` | Final integration: distance 200, geocode 403 -> 404 | Call geo endpoints | PARTIAL (distance works; geocoding provider blocked) |
| J. Location-aware resource matching | `OperationsMatchers.cs` distance scoring | Match endpoints | Operations matcher tests | Run match with coordinates | PASS |
| J. Geographical reporting/service coverage | Map + KPI area breakdowns | `/api/v1/kpis`, operations map | `KpiServiceTests.cs` area breakdowns | View KPI area tables | PARTIAL (area breakdowns live; advanced coverage analytics limited) |

## §2 Backend Services and Data Architecture

| Requirement | Implementation | API/UI | Test/Evidence | Demo | Status |
|---|---|---|---|---|---|
| ASP.NET Web API backend | `backend/src/Cms.Api` | Swagger/REST endpoints | Build/test evidence | Run API | PASS |
| Modular architecture justified | `README.md`, `docs/Crisis_Management_System_SRS_and_Development_Plan.md` | - | Documentation | Review README/SRS | PASS |
| SQL Server relational schema | `backend/db/*.sql`, EF Core context | DB initialization scripts | Final integration fresh DB 8 schemas/40 tables/27 FKs | Run SQL scripts | PASS |
| Required data domains present | `backend/src/Cms.Domain/Entities`, `backend/db/*` | APIs across modules | Schema + endpoint tests | Inspect DB/API | PASS (core domains present; some advanced P2/P3 domains partial) |
| JWT, RBAC, password hashing | Auth services, middleware, policies | `/auth/*`, protected endpoints | `AuthFlowTests.cs`, `SecurityBaselineTests.cs` | Login and role-check | PASS |
| HTTPS/HSTS/validation/sanitisation | `Program.cs`, middleware, ProblemDetails | All APIs | `SecurityBaselineTests.cs` | Run in dev/prod config | PASS |
| Secure file upload | `AttachmentService.cs`, `LocalFileStore.cs` | attachment/file endpoints | `AttachmentServiceTests.cs`, `FileUploadTests.cs`, `LocalFileStoreTests.cs` | Upload evidence | PASS (1 MB cap enforced consistently) |
| Audit logging | `AuditLogger`, audit endpoints, append-only triggers | `/api/v1/audit-events` | Audit tests, final integration 11 events | Review audit trail | PASS |
| Sensitive-data protection | AES-GCM email, masking, PII scrub | Auth/AI/audit flows | `SecurityNotes`, `AiChatServiceTests.cs` | Inspect masked audit | PASS |
| Rate limiting/abuse protection | `Program.cs` rate limiters | auth/public/upload policies | `RateLimitingTests.cs` | Exceed rate limit | PASS |
| External service resilience | AI fallback, geo fallback, outbox retries | AI/geo/notifications | Final integration geocode 403 handling; outbox tests | Disable provider | PASS |
| Transactions/duplicate prevention/recovery | Outbox, DB constraints, idempotency | Outbox/allocation flows | `OutboxServiceTests.cs`, final integration replay | Replay outbox/allocation | PASS |

## §3 External Integrations

| Requirement | Implementation | API/UI | Test/Evidence | Demo | Status |
|---|---|---|---|---|---|
| AI classification/priority/duplicate/matching | `RuleBasedProvider.cs`, `OperationsMatchers.cs` | Recommendation endpoints | AI/operations tests, final integration | Run recommendation | PASS |
| Multilingual text processing / response generation | Notification templates, AI chat | Notifications/chat | `NotificationTemplateTests.cs`, `AiChatServiceTests.cs` | Review template/chat | PARTIAL |
| Email service | SMTP/log sender abstraction | Notification delivery | `EmailNotificationHandlerTests.cs`, final integration log sender | Enable outbox with empty SMTP host | PASS (log-only in demo) |
| SMS service | Not implemented | - | - | - | MISSING (optional where supported) |
| Push service | Not implemented | - | - | - | MISSING (optional where supported) |
| Mapping provider | OSRM/Nominatim integration | `/api/v1/geo/*`, map | Final integration distance 200, geocode 403 mapped | Use map/geocode | PARTIAL |
| Geocoding/reverse geocoding | Geo endpoints/services | `/geo/geocode`, `/geo/reverse-geocode` | Geocode limitation documented | Call geocode endpoint | PARTIAL (external 403) |
| Distance/route calculation | OSRM/Haversine | `/geo/distance`, `/geo/route` | Final integration 200 | Call distance endpoint | PASS |
| Payment integration | Not implemented | - | - | - | N/A (optional) |

## §4, §5, §7, §9, §10 Deliverables and Advanced Requirements

| Requirement | Implementation/Evidence | Status |
|---|---|---|
| Responsive client, role-based UI, validation, auth workflow, error feedback | Next.js frontend, AuthGuard, form validation, ProblemDetails mapping | PASS |
| Accessibility/understandability | Labels/roles/aria in components; modal/toast improvements in this pass | PARTIAL (no completed formal WCAG/usability audit) |
| Dashboard visualisations | KPI dashboard, operations map, resources/shelters dashboards | PASS (some advanced cards remain simple) |
| Usability testing and evidence-based improvements | `docs/USABILITY_TEST.md` exists but is not a completed participant study | MISSING |
| RESTful backend, OOP/design patterns, DB/auth/AI/3rd-party, logging/validation/resilience, Swagger | Controllers/services/DI/factory/strategy/outbox; Swagger enabled in dev | PASS |
| Unit/API/business logic/priority/allocation/auth tests | xUnit integration and unit tests; targeted run in this pass passed | PASS (exact final full-suite count recorded in `docs/FINAL_VERIFICATION.md`) |
| Integration testing frontend-backend/DB/AI/notification/mapping | Final integration evidence and test suites | PASS/PARTIAL (mapping geocode provider limited) |
| UAT scenario 7-step workflow | Automated/API/live evidence exists; participant UAT form not completed | PARTIAL |
| Prototype minimum components | Citizen, coordination, resources, shelters, volunteers, admin, AI, communication, mapping present | PASS |
| Postman and/or Swagger | `docs/postman/*`, Swagger in Development | PASS |
| Report artifacts (diagrams, design decisions, evidence, individual contributions) | Diagrams/docs exist; final report PDF not in repo | PARTIAL/MISSING (PDF/submission packaging external) |
| AI recommendations reviewable/overridable by authorised staff | Decision workflow and RBAC | PASS |
| AI-assisted decision audit record includes recommendation, confidence, human action, timestamp | DecisionService now includes confidence in `Decision.Recorded` | PASS |
| Duplicate requests identified without auto-deletion | RuleBasedProvider candidate-only rationale | PASS |
| Multi-constraint allocation | Operations matchers weighted constraints | PASS |
| Shortage scenario competing urgent requests | Shortage proposal + 409 insufficient stock; no full partial-allocation execution flow | PARTIAL |
| Notification dedup on repeated event processing | Outbox idempotency + unique notification index; replay evidence | PASS |
| Temporary external AI/mapping/communication failure handling | Fallbacks, 404 mapping, dead-letter/retry | PASS |
| At least one major async/event-driven workflow documented | Transactional outbox + processor docs/evidence | PASS |
| Operational metrics exposed | KPI service now computes live case/resource/shelter/AI metrics and area breakdowns | PASS |
| Git traceability and individual contribution | Git history/PRs/docs present; final report must consolidate per-member contribution | PARTIAL |
| Third-party services documented with limitations/privacy/fallback | README/SECURITY/DEV3 report/evidence | PASS |

## Priority Gaps (this pass)

1. **Complete formal UAT/usability evidence** - `docs/UAT_CORE_LOOP.md` and `docs/USABILITY_TEST.md` still need participant execution records.
2. **Demand forecasting** - basic forecasting endpoint/UI is not implemented; document clearly or implement minimal read-only forecast.
3. **SMS/push** - not implemented; optional where supported. Do not claim implemented.
4. **Final report PDF and submission ZIP/link packaging** - external/submission artifact, not fully represented in repo.
5. **Geocoding provider 403** - external limitation; keep documented and retain fallbacks.
