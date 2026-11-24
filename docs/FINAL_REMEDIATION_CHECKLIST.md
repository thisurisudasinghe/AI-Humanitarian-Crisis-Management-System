# SAFEWAY Final Remediation Checklist

Branch: `fix/coursework-remediation` (from `develop` at `0b6333520ea48daa23557c8a6532c7ab506fa8ef`)
Isolated test DB: `CmsDbOpsE2E` (never shared `CmsDb`)

## Status legend
- [x] Implemented and verified
- [~] Implemented but not fully verified
- [P] Partially implemented
- [ ] Missing / not started
- [B] Blocked by external dependency

## Baseline
- develop commit: `0b6333520ea48daa23557c8a6532c7ab506fa8ef`
- Backend tests: 663 passed (historical; re-run required after changes)
- API E2E: 80 passed (historical)
- i18n: 3247 keys, EN/SI/TA parity

## Fix classification (vs latest develop)

### FIX 01 — Unify AI decision services and auditing [x]
- Legacy `DecisionController` (api/decision) retired; AI.IDecisionService registration removed.
- All decision endpoints now use canonical Governance.IDecisionService.
- Authorization strengthened to `AuthorizedRoles.Deciders`.
- Audit records include recommendation ref, type, confidence, outcome, decider, timestamp, reason.

### FIX 02 — Secure AI human approval and execution [x]
- Typed payload validation: required fields, GUIDs, positive quantities, matching target request IDs, JSON structure, size limits.
- Approved/modified allocation decisions require valid executable payload; Rejected/Deferred clear payload.
- DB-enforced uniqueness: `UX_decision_records_recommendation_id` (verified on real SQL Server CmsDbOpsE2E, no duplicates).
- Concurrency: `DbUpdateException` on unique violation -> `ConflictException` -> HTTP 409 (only genuine duplicate-key conflicts).
- Atomicity: transaction wraps decision + status transition + audit; rollback on failure.
- Repeated execution prevention by decisionId in AllocateAsync and AllocateShelterAsync.
- Resource/shelter capacity rechecked at execution time.

### FIX 03 — Resource shortages and partial fulfilment [P]
- `OperationsService` shortage ranking exists; allocations track quantity.
- Need explicit requested/allocated/delivered/outstanding tracking and partial-fulfilment scenario test.
- Status: partially implemented.

### FIX 04 — Basic demand forecasting [ ] (NEW)
- No forecast endpoint exists.
- Plan: 7-day moving average for next-day request count; Food/Medicine; honest insufficient-data state.

### FIX 05 — AI-generated crisis briefings [ ] (NEW)
- No briefing endpoint exists.
- Plan: factual aggregates + LLM with deterministic fallback; no PII to provider.

### FIX 06 — Real notification/communication integration [P]
- SMTP sender, outbox processor, templates exist; SMS not implemented.
- Status: email queued/logged; actual delivery unverified (no credentials). SMS optional.

### FIX 07 — Map, geocoding, reverse geocoding [P]
- Leaflet/OSM, MapPicker, GeoService exist; marker icons improved.
- Status: mostly implemented; needs failure-scenario verification.

### FIX 08 — Multilingual AI processing [P]
- EN/SI/TA UI parity; AI classification uses English rule-based; Sinhala/Tamil handling unclear.
- Status: partially implemented; needs verification.

### FIX 09 — Shelter, partner coordination, reporting [P]
- Public shelter endpoint, assistance requests, allocations exist.
- Status: mostly implemented; needs two-org partner test.

### FIX 10 — Practical usability [P]
- Navigation simplified in PR #57; portal flows exist.
- Status: partially addressed; needs task-oriented review.

### FIX 11 — Full fresh verification [P]
- Historical results exist; fresh run required after changes.

### FIX 12 — Technical report and submission [ ] 
- Docs exist (FINAL_VERIFICATION, FINAL_UIUX_DELIVERY_REPORT); diagram sources partial.

### Additional — Volunteer enrollment and approval [P]
- Verification queue exists in volunteers page; profile submission required before verification.
- Need clearer next-step guidance and pending count prominence.

## Implementation plan (this session)
1. [x] Audit and checklist created.
2. [ ] Volunteer enrollment UX clarity (frontend).
3. [ ] Demand forecasting endpoint + minimal UI.
4. [ ] AI crisis briefing endpoint + minimal UI.
5. [ ] Fresh verification (i18n, typecheck, build, backend tests, E2E).
6. [ ] Commit, push, PR, merge.
7. [ ] Final report.

## Notes
- No DB changes planned beyond existing schema.
- Preserve PR #57 public design.
- Use isolated test DB only.
