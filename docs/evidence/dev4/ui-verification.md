# Manual browser verification — 22 November 2026

Target: frontend `http://localhost:3000`, API `http://localhost:5210`, database `CmsDev4Verification`, in-app Chromium tab. Demo password `Demo1234!`. No console errors were observed on the passing pages below.

## Resources — `logistics@example.org`

- `/resources` loaded and API calls returned 200, including `GET /operations/status-transitions`.
- The recovery panel reports **No pending status transitions**; there are no invalid seeded `Verified -> Allocated` Retry buttons.
- The allocation dialog shows the generated Idempotency-Key and the reuse note: “This key is reused if the request fails, so retrying cannot allocate twice. Generate a new key only when starting a different allocation attempt.”
- Geocoder/API failures were separately covered by `live-api.log`.

## Shelters — `shelter@example.org`

- `/shelters` loaded and showed the three seeded shelters with occupancy/free places and Edit actions.
- `GET /shelters` and `GET /shelters/public` returned 200; public preview toggle worked.
- `GET /resources/types` returned 200 for this role; the create-type action remains unavailable.
- Shelter matching and shelter-linked assistance request actions rendered with their API calls.

## Volunteers — `volunteer@example.org` and `volunteercoordinator@example.org`

- Volunteer profile save posted `POST /volunteers/profile` successfully.
- Availability window submitted through the UI: `POST /volunteers/availability` returned **201** with `startsAt 2026-11-23T09:00:00` and `endsAt 2026-11-23T17:00:00`; both inputs cleared after success.
- The assigned “Dev4 end-to-end verification task COMPLETED” item is visible in My tasks.
- Coordinator queue loaded six volunteer records with Verify/Reject actions, volunteer matching, task creation and assignment controls.

## Map — `volunteercoordinator@example.org`

- `/operations/map` loaded **8 markers**: 3 shelters, 2 service centres and 3 areas from `GET /operations/areas`.
- Address-search failure falls back with `Request failed (GEOCODE_NOT_FOUND) Enter latitude and longitude manually.`; manual pin entry stays available.
- Clearing a coordinate input does not move the pin to `(0, 0)` because `MapPickerInner` ignores empty input.
- Anonymous `GET /geo/distance` and invalid-coordinate rejection are asserted in `live-api.log`.

## Auth failure handling

- With an invalid refresh token in `sessionStorage`, opening a guarded page cleared the token and redirected to `/login` with the SAFEWAY sign-in screen.

## Error handling

- Live API negative probes covering `422` invalid input, `400` missing Idempotency-Key, `403` unauthorised access and `409 SHELTER_FULL` are in `live-api.log`.
- `operations-api.ts` renders ProblemDetails messages without duplicating a code already present in the title.
