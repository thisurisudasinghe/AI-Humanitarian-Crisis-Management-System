# Operations frontend (Member 4)

Next.js App Router UI for logistics, shelter operations, volunteer workspace and mapping.

## Run
1. Install Node.js 20+.
2. `cd web && npm install`
3. Set `NEXT_PUBLIC_API_BASE_URL` to the ASP.NET API origin (default `https://localhost:7001`).
4. `npm run dev`

The pages use the shared API route convention `/api/v1`. `lib/api.ts` is a lightweight integration client; merge it with Member 1's token-refreshing `web/lib/api.ts` when the platform shell is integrated. The volunteer task listing endpoint is an integration TODO because the current API exposes task actions but not a scoped "my tasks" read endpoint.
