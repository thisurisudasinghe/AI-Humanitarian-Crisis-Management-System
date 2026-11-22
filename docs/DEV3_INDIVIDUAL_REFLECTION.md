# Individual reflection — Member 3 (AI, Governance & Notifications)

The most important lesson from this module was that resilience is a design
property, not a test added at the end. Building the outbox first forced every
later producer—request submission, announcements and notifications—to make one
transactional write and let a processor handle delivery. That kept the citizen
submit path fast and gave the system one place for retries, idempotency and
dead-letter visibility.

I found the human-in-the-loop boundary clarifying. AI and rule output is stored
as a recommendation and never performs a critical action. The exact decider
matrix is enforced in the governance service, with controller RBAC in front of
it; `SystemAdmin` deliberately has no break-glass decision right. Making the
recommendation and decision records append-only removed an ambiguous `Decided`
row mutation and made the decision record the single source of truth.

The hardest part was reconciling the frozen PDF contract with the team's evolved
V4 SQL conventions. Schema names (`common` rather than `notif`), snake_case
columns and `Processed`/`DeadLettered` statuses differ from the PDF wording, but
the behaviour and public API are preserved. I documented these as accepted
conventions instead of rewriting other developers' scripts.

Testing changed my implementation in useful ways. Failure-injection tests exposed
that "AI failure does not block submit" needed evidence at the
`RequestSubmittedHandler` boundary, not just inside the provider selector. The
notification handler also needed an audit event on successful delivery and the
announcement audience needed role **or** area fan-out to match the requirement.

If I repeated the module, I would introduce the append-only guard on
recommendations on day one and keep a single shared RBAC constants file from the
start. The final state is integration-ready: the API, UI, SQL scripts, Postman
collection, tests and demo evidence all reflect the same contract, and no
destructive Git operation was used.
