# Crisis Management System: SRS and System Development Plan

**Full title (from the coursework):** AI-Powered Humanitarian Crisis Management and Aid Coordination Platform
**Module:** SE205.3 Software Architecture, Group Coursework 2026
**Deadline:** 30 November 2026 | **Weight:** up to 40% of module | **Source of truth:** `SE205_3_Humanitarian_Crisis_Coursework_2026.docx`

> **Time check:** today is 13 November 2026, which leaves **17 days**. The plan below is deliberately lean and sequenced around that (see Part C, section 12).

**How to read the labels**

| Label | Meaning |
|---|---|
| **P1** | Mandatory. The document requires it in the minimum prototype, the Advanced/Novel list (§7, written as "must"), or the technical expectations |
| **P2** | Named in the scenario but not in the minimum prototype list. Build a lean version |
| **P3** | Optional / bonus (§3.4, §8). Only after all P1 and P2 are stable |
| **[A]** | An assumption of mine, not a coursework requirement (collected in section 15.2) |

---

# PART A: ANALYSIS OF THE COURSEWORK DOCUMENT

## A1. What the document actually demands

- A **group** coursework with two graded artefacts: a **working end-to-end prototype (45%)** and a **technical report (45%)**, plus **individual contribution (10%)** judged from Git history, features, tests and reflection.
- The focus is **software architecture**: coherent layering, justified decisions, design patterns, security, resilience, AI governance, integration and testing. It is explicitly *not* meant to be a CRUD app ("collection of disconnected screens" is called out in §10).
- Scope boundary: **no IoT, sensors, wearables or hardware telemetry.** Location and status come from users, forms and external mapping services.
- Critical humanitarian decisions must stay under **human control**. AI only recommends.

## A2. Mandatory technology (from the document, not chosen by me)

| Area | Document says | Source |
|---|---|---|
| Backend | **C# ASP.NET Web API**, RESTful, OOP and design patterns | §2, Task 2 |
| Architecture style | Team chooses modular monolith, SOA or microservices, **justified by FR/NFR** | §2 |
| Database | **Relational DB such as SQL Server** | §2.1, Technology Baseline |
| API docs | **Swagger/OpenAPI** | Task 2 |
| Auth | **JWT**, RBAC, secure password hashing, HTTPS | §2.3 |
| Client | HTML5, CSS3, JavaScript + a framework "such as React, Angular, or Vue" | Task 1 |
| Your mandated frontend | **React.js + Next.js** (your instruction; compatible with Task 1) | You |
| API testing | **Postman and/or Swagger** | §5.I |
| Version control | **Git**, with traceable individual contributions | §10 |
| Mandatory diagrams | Use Case, **Class (mandatory)**, Sequence (major workflow), Component | §5.II |
| Mandatory patterns | Repository, Dependency Injection, Factory, Strategy; Observer/Pub-Sub where justified | §5.II |

**What the document does not specify:** a formal coding-standards document, a .NET version, an ORM, an AI vendor, a mapping vendor, an email/SMS vendor, or a hosting platform. Those are team decisions that must be *justified in the report*. Where I propose one below it is tagged **[A]**.

## A3. Reconciliation notes (things to be aware of)

1. **Title:** your project name "Crisis Management System" is a short form. Use the full coursework title on the report cover and README so markers see a match.
2. **Next.js and the document:** the document lists React/Angular/Vue as examples ("such as"), so React + Next.js is fully compliant. Next.js adds server components and route handlers; the backend business logic must still live in ASP.NET, not in Next.js API routes (§10: business logic separated from presentation).
3. **Donor and Partner modules:** both appear in the scenario and the database list (§2.1), but the minimum prototype list (§5.I) does not include a Donor portal. Hence **P2**: the schema and a thin end-to-end flow exist, but no polish.
4. **"AI" scope:** the document says "AI-assisted feature(s)" (one is the minimum) but §7 forces several behaviours (duplicates, multi-constraint allocation, override and audit). Some can be classical algorithms behind an AI-service interface; this is acceptable if documented honestly.
5. **Offline-first** is a bonus (§8). Not planned.

## A4. Requirement inventory extracted from the document

| Group | Items extracted |
|---|---|
| Portals / dashboards (§1.1 A, B, F, G, H) | Citizen portal, coordination dashboard, shelter, volunteer, donor management |
| AI services (§1.1 C, §3.1) | Classification, urgency/priority, duplicates, resource matching, shelter recommendation, volunteer matching, demand forecast, anomaly detection, multilingual generation, crisis summary |
| Governance (§1.1 D, §7) | Approve/modify/reject/defer; record recommendation, confidence, decision, timestamp, user; role-appropriate approval; post-incident review; no unauthorised automation |
| Resources/logistics (§1.1 E) | Inventory, allocation, dispatch, shortages, conflicts, partner requests, alternatives |
| Communication (§1.1 I, §3.2) | Email, SMS, push (where supported), status updates, announcements, multilingual |
| Mapping (§1.1 J, §3.3) | Location capture, maps, geocoding, distance/route via external provider, coverage reporting |
| Data (§2.1) | 12 entity groups (users/orgs through KPIs and audit) |
| APIs (§2.2) | 13 API groups |
| Security (§2.3) | 10 mechanisms |
| Reliability (§2.4) | 6 mechanisms |
| Testing (Task 3) | Unit, integration, UAT with 7 listed scenarios |
| Advanced (§7) | 9 "must" behaviours |
| Deliverables (§5, §9) | Prototype, PDF report, demo video, DB scripts, README, ZIP |

Everything in this table is traced to an SRS requirement ID in section 16 (coverage matrix).

---

# PART B: SOFTWARE REQUIREMENTS SPECIFICATION

## 1. Introduction

### 1.1 Purpose
This SRS defines the requirements for the Crisis Management System (CMS), a multi-organisation platform for receiving, verifying, prioritising, assigning, fulfilling and auditing humanitarian assistance requests, with AI-assisted decision support under human control.

### 1.2 Intended audience
Group members (design and implementation), the module team (assessment), and the report's readers.

### 1.3 Definitions

| Term | Meaning |
|---|---|
| Crisis event | A declared emergency (flood, landslide, etc.) containing one or more operational areas |
| Assistance request | A request from an affected person for food, medicine, shelter, transport, accessibility help or other approved service |
| AI recommendation | A system-generated suggestion with confidence and rationale; never an action by itself |
| Decision record | The immutable record linking an AI recommendation to a human decision |
| HITL | Human-in-the-loop |
| Outbox | A DB table of pending events written in the same transaction as the business change, processed asynchronously |

### 1.4 References
The coursework document (primary), ASP.NET Core and EF Core documentation, OWASP ASVS/Top 10 (security checklist), WCAG 2.1 AA (accessibility target [A]).

## 2. Project Overview

CMS gives governments, NGOs, volunteers, donors, medical and logistics teams and affected communities a **shared operational picture** during a crisis. The core loop:

**Submit → Verify → AI-classify and score → Human review → Allocate (resource / shelter / volunteer) → Notify → Complete → Audit.**

This is also the UAT workflow required by Task 3, so the whole system is built around making this loop reliable and demonstrable.

## 3. Project Objectives

| # | Objective | Measurable by |
|---|---|---|
| O1 | Deliver the end-to-end request-to-closure workflow | UAT scenario passes (7 steps) |
| O2 | Keep humans in control of AI | 100% of AI-assisted decisions have a decision record; no endpoint auto-executes a critical action |
| O3 | Allocate scarce resources using multiple constraints | Shortage demo: 3 urgent requests, 1 limited resource, ranked and explained |
| O4 | Be resilient to third-party failure | Kill AI/map/email service → system keeps working with fallbacks |
| O5 | Be secure and auditable | JWT + RBAC + audit trail present and tested |
| O6 | Provide operational insight | Metrics: unresolved cases, avg response time, utilisation, occupancy, AI override rate |
| O7 | Produce a defensible architecture | Report with justified decisions, patterns and diagrams |

## 4. Project Scope

### 4.1 In scope (mandatory, P1)
Citizen portal; emergency coordination dashboard; resource/logistics; shelter management; volunteer management; administration; ASP.NET Web API; SQL Server; AI-assisted features; at least one communication integration; at least one mapping integration; Postman/Swagger testing; the §7 advanced behaviours.

### 4.2 In scope, lean (P2)
Donor and donation management, partner-organisation workspace (view and respond to assistance requests), AI crisis summaries, multilingual notification generation, push/SMS channels (one extra channel at most).

### 4.3 Optional (P3)
Payment sandbox (§3.4), demand forecasting dashboard, optimisation-based allocation, explainable-AI visuals, similarity dashboard, volunteer recommendation engine, fraud dashboard, cross-org workspace, scenario simulation.

### 4.4 Out of scope
IoT, sensors, wearables, smart-city and hardware telemetry (explicit in the document); offline-first capture; native mobile apps; real payment processing with live money.

## 5. Stakeholders and User Roles

| Role (from document) | Organisation | Main purpose |
|---|---|---|
| Affected Person / Citizen | Public | Request help, track status, view alerts and shelters, give feedback |
| Emergency Coordinator | Lead org | Manage crises and areas, review/approve AI output, escalate |
| Incident / Case Manager | Lead/partner | Triage, merge, assign and close cases |
| Resource and Logistics Manager | Any | Inventory, allocation, dispatch, shortages, partner requests |
| Shelter Administrator | Shelter operator | Capacity, occupancy, facilities, allocations |
| Volunteer Coordinator | Any | Volunteer verification, scheduling, task assignment |
| Donor / Funding Officer | Donor org | Campaigns, donations, transparency reports |
| Partner Organisation Officer | Partner org | Coordinated work scoped to own organisation |
| System Administrator | Platform | Users, roles, organisations, audit, configuration |
| Volunteer (sub-actor, implied by §1.1G) | Any | Register, set availability, accept/complete tasks |
| AI service, Map service, Email/SMS service | External | Secondary (system) actors |

The full permission matrix is in Part C, section 2.

## 6. Functional Requirements

**Pri:** P1 / P2 / P3 as defined above. **Src** points to the coursework section.

### 6.1 Citizen Portal (FR-CIT)

| ID | Requirement | Pri | Src |
|---|---|---|---|
| FR-CIT-01 | Register, log in and log out securely | P1 | §1.1A |
| FR-CIT-02 | Create and edit a household/individual profile | P1 | §1.1A |
| FR-CIT-03 | Submit a request with category, urgency, description, location and optional evidence upload | P1 | §1.1A |
| FR-CIT-04 | Select from approved service types (food, medicine, shelter, transport, accessibility, other) | P1 | §1.1A |
| FR-CIT-05 | Track request status and history; receive status notifications | P1 | §1.1A |
| FR-CIT-06 | View verified public alerts, approved shelters, service centres and safety guidance (some without login) | P1 | §1.1A |
| FR-CIT-07 | Receive multilingual communication and use an AI support chat interface (FAQ-style, no critical decisions) | P2 | §1.1A |
| FR-CIT-08 | Submit feedback after assistance is delivered | P2 | §1.1A |

### 6.2 Emergency Coordination Dashboard (FR-COORD)

| ID | Requirement | Pri | Src |
|---|---|---|---|
| FR-COORD-01 | Create/manage crisis events and operational areas | P1 | §1.1B |
| FR-COORD-02 | Review incoming requests with supporting information | P1 | §1.1B |
| FR-COORD-03 | Classify, prioritise, escalate, assign, merge or close cases | P1 | §1.1B |
| FR-COORD-04 | Show AI urgency/priority recommendations with confidence and rationale | P1 | §1.1B |
| FR-COORD-05 | Monitor open, overdue, escalated and unresolved requests (SLA timers) | P1 | §1.1B |
| FR-COORD-06 | Coordinate with partner organisations (assign/share cases, see partner status) | P2 | §1.1B |
| FR-COORD-07 | Operational summaries, workload statistics and resource shortages | P1 | §1.1B, §7 |
| FR-COORD-08 | Full timeline of decisions and status changes per case | P1 | §1.1B |

### 6.3 AI Decision Support (FR-AI)

| ID | Requirement | Pri | Src |
|---|---|---|---|
| FR-AI-01 | Auto-classify request category | P1 | §1.1C |
| FR-AI-02 | Score urgency/priority with confidence and a human-readable rationale | P1 | §1.1C |
| FR-AI-03 | Detect duplicate/near-duplicate requests and **link as candidates, never auto-delete** | P1 | §1.1C, §7 |
| FR-AI-04 | Recommend resource-to-request matches using multiple constraints | P1 | §1.1C, §7 |
| FR-AI-05 | Recommend shelters by availability and suitability | P1 | §1.1C |
| FR-AI-06 | Recommend volunteer-to-task matches (skills, availability, location) | P2 | §1.1C |
| FR-AI-07 | Flag suspicious/anomalous requests for human review | P2 | §1.1C |
| FR-AI-08 | Generate multilingual responses/notifications (human-approved for critical messages) | P2 | §1.1C |
| FR-AI-09 | Generate crisis summaries for authorised coordinators | P2 | §1.1C |
| FR-AI-10 | Demand forecasting for selected resource categories | P3 | §1.1C |
| FR-AI-11 | Every AI feature degrades gracefully to a rule-based fallback when the AI service is unavailable | P1 | §7 |

### 6.4 Human-in-the-Loop Governance (FR-GOV)

| ID | Requirement | Pri | Src |
|---|---|---|---|
| FR-GOV-01 | Authorised staff can **approve, modify, reject or defer** any AI recommendation | P1 | §1.1D |
| FR-GOV-02 | For each AI-assisted decision, store: original recommendation, confidence, human decision, timestamp, responsible user | P1 | §1.1D, §7 |
| FR-GOV-03 | Critical actions (e.g., allocating scarce resources, closing a critical case, mass announcements) require role-appropriate approval | P1 | §1.1D |
| FR-GOV-04 | No code path executes a critical action from an AI output without a recorded human decision | P1 | §1.1D |
| FR-GOV-05 | Post-incident review screen listing AI decisions, overrides and outcomes | P1 | §1.1D |

### 6.5 Resource and Logistics (FR-RES)

| ID | Requirement | Pri | Src |
|---|---|---|---|
| FR-RES-01 | Manage resource types and inventories (food, medicine, water, vehicles, personnel, other) | P1 | §1.1E |
| FR-RES-02 | Track quantity, availability, reserved, allocated, dispatched, utilised, replenishment threshold | P1 | §1.1E |
| FR-RES-03 | Match resources to requests using multiple constraints (type, quantity, distance/route, urgency, expiry, accessibility, org ownership) | P1 | §1.1E, §7 |
| FR-RES-04 | Handle shortages: when several urgent requests compete, rank them, allocate by priority, partially fulfil, and explain | P1 | §1.1E, §7 |
| FR-RES-05 | Suggest alternative allocations if the preferred resource is unavailable | P1 | §1.1E |
| FR-RES-06 | Raise a request for assistance to a partner organisation | P2 | §1.1E |
| FR-RES-07 | Record dispatch and delivery confirmation | P1 | §1.1E |

### 6.6 Shelter Management (FR-SHE)

| ID | Requirement | Pri | Src |
|---|---|---|---|
| FR-SHE-01 | Register/manage shelters and assistance centres | P1 | §1.1F |
| FR-SHE-02 | Maintain capacity, occupancy, facilities, accessibility, service availability | P1 | §1.1F |
| FR-SHE-03 | Allocate households by capacity and suitability | P1 | §1.1F |
| FR-SHE-04 | **Prevent over-allocation** (enforced in the DB transaction, not just the UI) | P1 | §1.1F |
| FR-SHE-05 | Request more resources when capacity/supplies are insufficient | P1 | §1.1F |

### 6.7 Volunteer Management (FR-VOL)

| ID | Requirement | Pri | Src |
|---|---|---|---|
| FR-VOL-01 | Volunteer registration with skill profile | P1 | §1.1G |
| FR-VOL-02 | Availability scheduling | P1 | §1.1G |
| FR-VOL-03 | Task creation, assignment, acceptance/decline | P1 | §1.1G |
| FR-VOL-04 | Verification workflow and role-based access | P1 | §1.1G |
| FR-VOL-05 | AI-assisted volunteer-task matching (see FR-AI-06) | P2 | §1.1G |
| FR-VOL-06 | Activity tracking and completion records | P1 | §1.1G |

### 6.8 Donor and Donation Management (FR-DON)

| ID | Requirement | Pri | Src |
|---|---|---|---|
| FR-DON-01 | Donor registration and campaign participation | P2 | §1.1H |
| FR-DON-02 | Record monetary and material donations (manual entry; no real payments) | P2 | §1.1H |
| FR-DON-03 | Categorise and allocate donations to campaigns/crises | P2 | §1.1H |
| FR-DON-04 | Campaign and expenditure tracking | P2 | §1.1H |
| FR-DON-05 | Donor transparency report | P2 | §1.1H |
| FR-DON-06 | Flag suspicious donations/allocations for human review (rule-based) | P2 | §1.1H |
| FR-DON-07 | Payment gateway sandbox | P3 | §3.4 |

### 6.9 Communication (FR-COM)

| ID | Requirement | Pri | Src |
|---|---|---|---|
| FR-COM-01 | Email notifications (at least one external channel is mandatory) | P1 | §1.1I, §5.I |
| FR-COM-02 | In-app notifications for status updates, assignments, shelter allocation | P1 | §1.1I |
| FR-COM-03 | Emergency announcements to targeted audiences (approval required) | P1 | §1.1I |
| FR-COM-04 | SMS notifications | P3 | §1.1I |
| FR-COM-05 | Push notifications where supported | P3 | §1.1I |
| FR-COM-06 | Multilingual templates | P2 | §1.1I |
| FR-COM-07 | **No duplicate notification** if the same event is processed twice (idempotency) | P1 | §7 |
| FR-COM-08 | Failed notifications are retried and tracked; user flow is not blocked | P1 | §2.4, §7 |

### 6.10 Mapping and Location (FR-MAP)

| ID | Requirement | Pri | Src |
|---|---|---|---|
| FR-MAP-01 | Capture location on requests (map pin, address search, GPS where the browser allows) | P1 | §1.1J |
| FR-MAP-02 | Interactive map of shelters, service centres, operational areas | P1 | §1.1J |
| FR-MAP-03 | Geocoding/reverse geocoding and distance/route calculation via an external provider | P1 | §3.3 |
| FR-MAP-04 | Location-aware resource matching | P1 | §1.1J |
| FR-MAP-05 | Basic geographical reporting / coverage analysis (requests per area, shelters per area) | P2 | §1.1J |
| FR-MAP-06 | If the provider fails, fall back to cached results or straight-line (Haversine) distance and show "approximate" | P1 | §7 |

### 6.11 Administration and Partner (FR-ADM, FR-PTN)

| ID | Requirement | Pri | Src |
|---|---|---|---|
| FR-ADM-01 | Manage users, roles, permissions and organisations | P1 | §2.1, §5.I |
| FR-ADM-02 | View and search audit events and administrative activity | P1 | §2.1, §2.3 |
| FR-ADM-03 | View operational KPIs (unresolved, avg response time, utilisation, occupancy, AI override rate) | P1 | §7 |
| FR-ADM-04 | Configure approved service types, categories, thresholds | P1 | §1.1A |
| FR-PTN-01 | Partner officer sees only own organisation's data plus cases shared with it | P2 | §1.1B |
| FR-PTN-02 | Partner officer accepts/rejects a partner assistance request | P2 | §1.1E |

### 6.12 Request lifecycle (state model)

`Submitted → Verified (or Rejected) → Triaged → Recommended → Approved → Allocated → Dispatched/InProgress → Fulfilled → Closed`, with side paths `Escalated`, `Merged` (duplicates), `OnHold` and `Reopened`. Every transition writes a `RequestStatusHistory` row and an audit event. Invalid transitions are rejected by a state-machine guard in the domain layer.

## 7. Non-Functional Requirements

| ID | Category | Requirement | Pri | Src |
|---|---|---|---|---|
| NFR-SEC-01 | Security | JWT auth with short-lived access tokens and refresh tokens | P1 | §2.3 |
| NFR-SEC-02 | Security | RBAC enforced at API **and** business-logic level | P1 | §2.3 |
| NFR-SEC-03 | Security | Passwords hashed with a slow salted hash (PBKDF2/BCrypt/Argon2); never stored or logged in plain text | P1 | §2.3 |
| NFR-SEC-04 | Security | HTTPS everywhere; HSTS; secure cookies | P1 | §2.3 |
| NFR-SEC-05 | Security | Server-side validation and sanitisation of all input; parameterised queries only | P1 | §2.3 |
| NFR-SEC-06 | Security | Secure file upload (type/size allow-list, content sniffing, random names, outside web root, no execution) | P1 | §2.3 |
| NFR-SEC-07 | Security | Sensitive-data protection (field encryption for contact/medical details, masking in logs, least-privilege views) | P1 | §2.3 |
| NFR-SEC-08 | Security | Rate limiting and abuse protection on public/auth endpoints | P1 | §2.3 |
| NFR-REL-01 | Reliability | Timeouts, retries (with back-off) and circuit breaker on every external call | P1 | §2.4 |
| NFR-REL-02 | Reliability | Idempotent processing (no duplicate processing/notifications) | P1 | §2.4, §7 |
| NFR-REL-03 | Reliability | DB transactions around allocation, occupancy and inventory changes | P1 | §2.4 |
| NFR-REL-04 | Reliability | Failed async jobs retried, then dead-lettered and visible to admin | P1 | §2.4 |
| NFR-REL-05 | Reliability | Partially completed workflows have explicit statuses and a recovery path | P1 | §2.4 |
| NFR-PER-01 | Performance | Typical list/read API p95 under 500 ms on demo data; AI calls never block the submit request (async) | P2 [A] | derived |
| NFR-USA-01 | Usability | Responsive on desktop, tablet and mobile | P1 | Task 1 |
| NFR-USA-02 | Usability | Accessible UI (semantic HTML, keyboard navigation, labels, contrast; WCAG 2.1 AA target) | P1 | Task 1 |
| NFR-USA-03 | Usability | Clear error handling and user feedback; client-side validation | P1 | Task 1 |
| NFR-USA-04 | Usability | Usability testing with evidence-based improvements (even 3 to 5 testers, documented) | P1 | Task 1 |
| NFR-MNT-01 | Maintainability | Layered architecture; business logic separated from presentation and infrastructure | P1 | §10 |
| NFR-MNT-02 | Maintainability | Consistent API naming, validation, status codes and error format | P1 | §10 |
| NFR-MNT-03 | Maintainability | OOP principles, DI, documented patterns | P1 | Task 2 |
| NFR-AUD-01 | Auditability | Append-only audit log of decisions, admin actions and data access to sensitive records | P1 | §2.3, §7 |
| NFR-SCA-01 | Scalability | Design for scale-out in the report: stateless API, outbox/queue abstraction, indexes, a path from modular monolith to services | P1 (report) | §5.II |
| NFR-TST-01 | Testability | Unit, integration and UAT tests; a repeatable seed scenario | P1 | Task 3, §10 |
| NFR-DOC-01 | Documentation | Swagger/OpenAPI for all endpoints; README; DB scripts | P1 | Task 2, §9 |
| NFR-PRV-01 | Privacy | Minimise personal data sent to third-party AI (strip names, phone, exact address); document it | P1 | §10 |

## 8. System Modules and Features

| # | Module | Key features | Primary roles |
|---|---|---|---|
| M1 | Identity and Access | Register/login, JWT + refresh, RBAC, org scoping | All |
| M2 | Crisis and Case Management | Crisis events, areas, requests, state machine, timeline, merge | Coordinator, Case Manager |
| M3 | AI Decision Support | Classification, scoring, duplicates, matching, summary, fallbacks | System (used by staff) |
| M4 | Governance and Audit | Recommendation review, decision records, audit log, post-incident review | Coordinator, Admin |
| M5 | Resource and Logistics | Inventory, reservation, allocation, shortage engine, dispatch, partner requests | Logistics Manager |
| M6 | Shelter Management | Shelters, capacity, allocation, over-allocation guard | Shelter Admin |
| M7 | Volunteer Management | Profiles, availability, tasks, matching | Volunteer Coordinator |
| M8 | Donation Management (lean) | Donors, campaigns, donations, allocations, report | Donor Officer |
| M9 | Notification Service | Outbox, channel factory, idempotency, retry, templates | System |
| M10 | Mapping and Location | Geocoding, routing, cache, fallback, map layers | System, all UIs |
| M11 | Reporting and KPIs | Metrics, dashboards, exports | Coordinator, Admin |
| M12 | Administration | Users, orgs, roles, configuration, audit viewer | Admin |
