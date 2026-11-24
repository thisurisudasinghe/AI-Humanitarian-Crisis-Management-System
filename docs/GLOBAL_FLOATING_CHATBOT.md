# Global floating SAFEWAY assistant

## Integration

- Component: `frontend/components/safeway/GlobalFloatingChat.tsx`
- Styles: `frontend/app/global-chatbot.css`
- Single mount: `frontend/app/layout.tsx`, inside `AuthProvider` and beside `{children}`
- Dedicated route reuse: the widget is suppressed on `/operations/ai/chat`, which keeps its existing full-page FAQ chat. No new chatbot page exists.

The root layout persists across App Router client navigation, so one widget instance remains open and available on public, authentication, citizen, operations, admin and error routes.

## Backend

- Endpoint: `POST /api/v1/ai/chat`
- Controller: `backend/src/Cms.Api/Controllers/AiController.cs`
- Service: `backend/src/Cms.Infrastructure/Services/AI/AiChatService.cs` via `IAiChatService`
- The action is `[AllowAnonymous]` and uses the existing `public` rate-limit policy (30 requests/minute/IP). Decision-support and governance endpoints remain authenticated and role-restricted.
- No new controller, AI service, LLM provider, conversation entity, table or migration was added.

## AI provider configuration

All configuration stays on the backend. Use environment variables; do not put keys in frontend code or committed settings files.

| Environment variable | Purpose |
|---|---|
| `Ai__Chat__Endpoint` | HTTPS endpoint for the optional informational answer service. When absent, the backend returns curated, verified FAQ answers with a clear `notice`. |
| `Ai__Llm__ApiKey` | Optional bearer credential sent only by the backend to the configured endpoint. |
| `Ai__Llm__Endpoint` | Existing separate endpoint used by decision-support AI. It is not used by the floating chat. |

A minimal request/response contract for `Ai__Chat__Endpoint`:

```json
{ "question": "...", "instructions": "SAFEWAY informational guardrails" }
```

```json
{ "answer": "...", "suggestions": ["Follow-up question"] }
```

The backend enforces a 5-second timeout, PII scrubbing, 2,000-character input and 4,000-character answer caps, response validation, FAQ fallback and an always-present safety disclaimer. The LLM request includes explicit instructions not to claim live availability, inventory or operational status and not to make decisions.

## Conversation state

- Guests: `sessionStorage` key `safeway.chat.v1.guest`.
- Signed-in users: `localStorage` key scoped by encoded user ID, `safeway.chat.v1.user.<userId>`.
- On sign-in, guest state is discarded from view; on user changes, only that user's key is loaded. Clear Chat removes the active key.
- The backend is stateless: no conversation is transmitted to another user or persisted server-side. This is a deliberate limit because no authorized chat-history store exists.

## Safety

- The assistant answers questions only. It cannot submit, approve, reject, allocate, dispatch or close anything.
- Immediate-danger questions prioritise local emergency services.
- Live shelter availability, inventory quantities and operational status are never claimed; the assistant links users to authoritative SAFEWAY pages and human staff.
- The backend scrubs obvious PII before optional AI calls, enforces 2,000-character input / 4,000-character answer limits, validates provider responses, applies a defensive no-autonomous-action guard, and always appends a safety disclaimer.
- Sensitive details should be entered only in official SAFEWAY forms, not in chat.
