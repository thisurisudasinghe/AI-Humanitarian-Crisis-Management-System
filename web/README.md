# Operations frontend (Member 4)

Next.js App Router UI for logistics, shelter operations, volunteer workspace and mapping (D4-11).
This is the Member 4 `/web` handoff app required by the assignment. The active integrated shell is
`frontend/`; merging these routes into it is a team integration decision.

## Run

1. Install Node.js 20+.
2. `cd web && npm install`
3. Set `NEXT_PUBLIC_API_BASE_URL` to the ASP.NET API origin (default `https://localhost:7001`).
4. `npm run dev`
5. Verify types with `npm run lint` and production build with `npm run build`.

## Routes

- `/logistics` — inventory filters, stock/utilisation, match rationale with approximate badges,
  allocate dialog (body is `decisionId` only; a fresh `Idempotency-Key` is generated per attempt and
  reused on retry), and the read-only shortage proposal with a human-approval banner.
- `/shelters` — staff list, capacity/occupancy bars, create/edit with `MapPicker`, approval,
  public preview and the guarded shelter-allocation dialog (`SHELTER_FULL` explained).
- `/volunteer` — self-service profile and availability, own tasks with accept/decline/complete,
  and the coordinator verification queue and task-assignment form.
- `/maps` — `MapView` with approved shelters, public service centres and staff operational areas,
  plus a `MapPicker` example. Each data source reports its own error (for example a 403 on areas)
  instead of silently emptying the map.

## Components

`components/map/MapPicker.tsx` and `MapView.tsx` are the stable D4-03 public components. Both use
`next/dynamic(..., { ssr: false })` wrappers so Leaflet is never imported by a server-rendered module.
Their client implementations live in `MapPickerInner.tsx` and `MapViewInner.tsx`.

Usage example (Member 2 request form):

```tsx
const MapPicker = dynamic(() => import("../../components/map/MapPicker"), {
  ssr: false,
});
<MapPicker
  value={{ lat, lng }}
  onChange={({ lat, lng, address }) => {
    setLat(String(lat));
    setLng(String(lng));
    setAddress(address ?? "");
  }}
/>;
```

```tsx
const MapView = dynamic(() => import("../../components/map/MapView"), {
  ssr: false,
});
<MapView
  markers={[{ id, lat, lng, label: "Negombo centre", type: "service-centre" }]}
/>;
```

`lib/api.ts` surfaces RFC 9457 `ProblemDetails` as `ApiError` with `status`, `code` and
`userMessage`, so 403/409/422 responses are rendered instead of crashing a page. All server-side
guards remain authoritative; the UI only prevents accidental double submits.
