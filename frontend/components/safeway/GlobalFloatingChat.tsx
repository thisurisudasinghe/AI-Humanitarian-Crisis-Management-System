"use client";

import { useAuth } from "@/lib/auth";
import { ApiError, errorMessage } from "@/lib/api";
import {
  GlobalChatSuggestions,
  MAX_CHAT_MESSAGE_LENGTH,
  sendChatMessage
} from "@/lib/aiChat";
import { Icon } from "@/components/safeway/Icon";
import { usePathname } from "next/navigation";
import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type FormEvent,
  type KeyboardEvent as ReactKeyboardEvent
} from "react";

type ChatMessage = {
  id: string;
  role: "assistant" | "user";
  text: string;
  source?: string;
  notice?: string;
  createdAt: number;
};

const MAX_STORED_MESSAGES = 40;
const GUEST_STORAGE_KEY = "safeway.chat.v1.guest";
const DEDICATED_CHAT_ROUTES = new Set([
  "/operations/ai/chat",
  "/operations/ai/chat/"
]);

const WELCOME_TEXT =
  "Hi! I'm the SAFEWAY assistant. I can explain how to report an incident, find help, " +
  "understand shelters, resources, notifications, volunteering, donating and how human decisions work. " +
  "I provide guidance only - I cannot submit requests, make decisions, or confirm live operational status. " +
  "If anyone is in immediate danger, contact local emergency services now.";

function createWelcome(): ChatMessage {
  return {
    id: "welcome",
    role: "assistant",
    text: WELCOME_TEXT,
    source: "SAFEWAY",
    createdAt: 0
  };
}

function createId(): string {
  if (typeof globalThis.crypto?.randomUUID === "function") {
    return globalThis.crypto.randomUUID();
  }

  return `msg-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

function userStorageKey(userId: string): string {
  return `safeway.chat.v1.user.${encodeURIComponent(userId)}`;
}

function isChatMessage(value: unknown): value is ChatMessage {
  if (!value || typeof value !== "object") {
    return false;
  }

  const candidate = value as Record<string, unknown>;
  return (
    typeof candidate.id === "string" &&
    (candidate.role === "assistant" || candidate.role === "user") &&
    typeof candidate.text === "string" &&
    candidate.text.length > 0
  );
}

function readMessages(storage: Storage, key: string): ChatMessage[] | null {
  const raw = storage.getItem(key);
  if (!raw) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) {
      return null;
    }

    const messages = parsed.filter(isChatMessage).map((message) => ({
      ...message,
      source: typeof message.source === "string" ? message.source : undefined,
      notice: typeof message.notice === "string" ? message.notice : undefined,
      createdAt: typeof message.createdAt === "number" ? message.createdAt : 0
    }));

    return messages.length > 0 ? messages.slice(-MAX_STORED_MESSAGES) : null;
  } catch {
    return null;
  }
}

function friendlyError(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 429) {
      return "Too many messages right now. Please wait a minute and try again.";
    }
    if (error.status === 401) {
      return "Your session has expired. Sign in again to continue using account-specific features.";
    }
    if (error.status >= 500) {
      return "The SAFEWAY assistant is temporarily unavailable. Please try again shortly.";
    }
  }

  const detail = errorMessage(error);
  return detail && detail !== "Something went wrong."
    ? detail
    : "Please check your connection and try again.";
}

function formatSource(source: string): string {
  if (source === "Ai") {
    return "AI response";
  }
  if (source === "Rule" || source === "FAQ") {
    return "Verified SAFEWAY FAQ";
  }
  if (source === "SAFEWAY") {
    return "Welcome";
  }
  return source;
}

/**
 * One global floating assistant. Mounted once in the root layout so it survives
 * client-side navigation. `/operations/ai/chat` keeps its dedicated full-page
 * UI, so the floating panel is suppressed there to avoid duplicate chat UIs.
 */
export function GlobalFloatingChat() {
  const pathname = usePathname();
  const { user, ready } = useAuth();

  const [open, setOpen] = useState(false);
  const [messages, setMessages] = useState<ChatMessage[]>(() => [createWelcome()]);
  const [suggestions, setSuggestions] = useState<string[]>([
    ...GlobalChatSuggestions
  ]);
  const [draft, setDraft] = useState("");
  const [sending, setSending] = useState(false);
  const [storageReady, setStorageReady] = useState(false);

  const launcherRef = useRef<HTMLButtonElement>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);
  const panelRef = useRef<HTMLElement>(null);
  const logRef = useRef<HTMLDivElement>(null);

  const userId = user?.userId ?? null;
  const storageKey = userId ? userStorageKey(userId) : GUEST_STORAGE_KEY;
  const isDedicatedRoute = pathname
    ? DEDICATED_CHAT_ROUTES.has(pathname)
    : false;

  useEffect(() => {
    if (!ready) {
      return;
    }

    setStorageReady(false);
    setSending(false);
    setSuggestions([...GlobalChatSuggestions]);

    try {
      if (userId) {
        window.sessionStorage.removeItem(GUEST_STORAGE_KEY);
      }

      const storage = userId ? window.localStorage : window.sessionStorage;
      setMessages(readMessages(storage, storageKey) ?? [createWelcome()]);
    } catch {
      setMessages([createWelcome()]);
    }

    setStorageReady(true);
  }, [ready, userId, storageKey]);

  useEffect(() => {
    if (!ready || !storageReady) {
      return;
    }

    try {
      const storage = userId ? window.localStorage : window.sessionStorage;
      storage.setItem(
        storageKey,
        JSON.stringify(messages.slice(-MAX_STORED_MESSAGES))
      );
    } catch {
      // Storage can be disabled or full. The in-memory conversation still works.
    }
  }, [messages, ready, storageReady, storageKey, userId]);

  const focusInput = useCallback(() => {
    window.requestAnimationFrame(() => inputRef.current?.focus());
  }, []);

  const openChat = useCallback(() => {
    setOpen(true);
    focusInput();
  }, [focusInput]);

  const closeChat = useCallback(() => {
    setOpen(false);
    window.requestAnimationFrame(() => launcherRef.current?.focus());
  }, []);

  useEffect(() => {
    if (!open) {
      return;
    }

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        closeChat();
      }
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [open, closeChat]);

  useEffect(() => {
    if (!open) {
      return;
    }

    const log = logRef.current;
    if (log) {
      log.scrollTop = log.scrollHeight;
    }
  }, [messages, open, sending]);

  const ask = useCallback(
    async (rawQuestion: string) => {
      const question = rawQuestion.trim();
      if (!question || sending) {
        return;
      }
      if (question.length > MAX_CHAT_MESSAGE_LENGTH) {
        const tooLongMessage: ChatMessage = {
          id: createId(),
          role: "assistant",
          text: `Please shorten your question to ${MAX_CHAT_MESSAGE_LENGTH} characters or fewer.`,
          source: "Error",
          createdAt: Date.now()
        };
        setMessages((current) =>
          [...current, tooLongMessage].slice(-MAX_STORED_MESSAGES)
        );
        return;
      }

      const userMessage: ChatMessage = {
        id: createId(),
        role: "user",
        text: question,
        createdAt: Date.now()
      };

      setMessages((current) =>
        [...current, userMessage].slice(-MAX_STORED_MESSAGES)
      );
      setDraft("");
      setSending(true);

      try {
        const response = await sendChatMessage(question);
        const assistantMessage: ChatMessage = {
          id: createId(),
          role: "assistant",
          text: response.answer,
          source: response.source,
          notice: response.notice,
          createdAt: Date.now()
        };

        setMessages((current) =>
          [...current, assistantMessage].slice(-MAX_STORED_MESSAGES)
        );
        setSuggestions(
          response.suggestions.length > 0
            ? response.suggestions.slice(0, 4)
            : [...GlobalChatSuggestions]
        );
      } catch (error) {
        const errorMessageText: ChatMessage = {
          id: createId(),
          role: "assistant",
          text:
            `I could not reach the SAFEWAY assistant. ${friendlyError(error)} ` +
            "You can still use the site normally, or try again shortly.",
          source: "Error",
          createdAt: Date.now()
        };

        setMessages((current) =>
          [...current, errorMessageText].slice(-MAX_STORED_MESSAGES)
        );
      } finally {
        setSending(false);
      }
    },
    [sending]
  );

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void ask(draft);
  }

  function clearChat() {
    try {
      const storage = userId ? window.localStorage : window.sessionStorage;
      storage.removeItem(storageKey);
    } catch {
      // Ignore unavailable storage.
    }

    setMessages([createWelcome()]);
    setSuggestions([...GlobalChatSuggestions]);
    setDraft("");
    focusInput();
  }

  function handlePanelKeyDown(event: ReactKeyboardEvent<HTMLElement>) {
    if (event.key === "Escape") {
      event.stopPropagation();
      closeChat();
      return;
    }

    if (event.key !== "Tab") {
      return;
    }

    const panel = panelRef.current;
    if (!panel) {
      return;
    }

    const focusable = Array.from(
      panel.querySelectorAll<HTMLElement>(
        'button:not([disabled]), textarea:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])'
      )
    );

    if (focusable.length === 0) {
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];

    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  if (isDedicatedRoute) {
    return null;
  }

  return (
    <>
      {open ? (
        <section
          ref={panelRef}
          id="safeway-global-chat-panel"
          className="safeway-chat-panel"
          role="dialog"
          aria-modal="false"
          aria-label="SAFEWAY assistant chat"
          onKeyDown={handlePanelKeyDown}
        >
          <header className="safeway-chat-header">
            <div className="safeway-chat-header__identity">
              <span className="safeway-chat-header__avatar" aria-hidden="true">
                <Icon name="brain" size={22} />
              </span>
              <div>
                <strong>SAFEWAY assistant</strong>
                <span>Guidance, with human-led decisions</span>
              </div>
            </div>
            <div className="safeway-chat-header__actions">
              <button
                type="button"
                className="safeway-chat-text-button"
                onClick={clearChat}
                aria-label="Clear conversation"
              >
                Clear
              </button>
              <button
                type="button"
                className="safeway-chat-icon-button"
                onClick={closeChat}
                aria-label="Minimize chat"
                title="Minimize chat"
              >
                <Icon name="close" size={18} />
              </button>
            </div>
          </header>

          <div className="safeway-chat-safety" role="note">
            <Icon name="shield" size={15} />
            <span>
              Information only - no decisions or live status. For immediate danger, contact local emergency services.
              Avoid sharing sensitive personal details in chat.
            </span>
          </div>

          <div
            ref={logRef}
            className="safeway-chat-log"
            role="log"
            aria-live="polite"
            aria-relevant="additions"
            aria-busy={sending}
          >
            {messages.map((message) => (
              <article
                key={message.id}
                className={`safeway-chat-message safeway-chat-message--${message.role}`}
              >
                <div className="safeway-chat-message__meta">
                  <b>
                    {message.role === "user"
                      ? "You"
                      : message.source === "Error"
                        ? "Assistant unavailable"
                        : "SAFEWAY assistant"}
                  </b>
                  {message.role === "assistant" &&
                  message.source &&
                  message.source !== "Error" ? (
                    <span>{formatSource(message.source)}</span>
                  ) : null}
                </div>
                <p>{message.text}</p>
                {message.notice ? (
                  <small className="safeway-chat-message__notice">
                    {message.notice}
                  </small>
                ) : null}
              </article>
            ))}

            {sending ? (
              <div className="safeway-chat-typing" role="status">
                <span />
                <span />
                <span />
                <span className="safeway-chat-sr-only">
                  SAFEWAY assistant is typing
                </span>
              </div>
            ) : null}
          </div>

          {suggestions.length > 0 ? (
            <div className="safeway-chat-suggestions" aria-label="Suggested questions">
              {suggestions.slice(0, 4).map((suggestion) => (
                <button
                  key={suggestion}
                  type="button"
                  className="safeway-chat-suggestion"
                  disabled={sending}
                  onClick={() => void ask(suggestion)}
                >
                  {suggestion}
                </button>
              ))}
            </div>
          ) : null}

          <form className="safeway-chat-form" onSubmit={submit}>
            <label className="safeway-chat-sr-only" htmlFor="safeway-chat-input">
              Your question
            </label>
            <textarea
              id="safeway-chat-input"
              ref={inputRef}
              rows={2}
              maxLength={MAX_CHAT_MESSAGE_LENGTH}
              value={draft}
              placeholder="Ask about reports, shelters, resources, volunteering…"
              aria-describedby="safeway-chat-input-help"
              onChange={(event) => setDraft(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter" && !event.shiftKey) {
                  event.preventDefault();
                  void ask(draft);
                }
              }}
            />
            <span id="safeway-chat-input-help" className="safeway-chat-sr-only">
              Informational questions only. Do not include sensitive personal details. Maximum {MAX_CHAT_MESSAGE_LENGTH} characters.
            </span>
            <button
              type="submit"
              className="safeway-chat-send"
              disabled={sending || !draft.trim()}
              aria-label="Send message"
              title="Send message"
            >
              <Icon name="arrow" size={18} />
            </button>
          </form>

          <footer className="safeway-chat-footer">
            Guidance only. Human staff make operational decisions. Avoid sharing sensitive personal details.
          </footer>
        </section>
      ) : null}

      <button
        ref={launcherRef}
        type="button"
        className={`safeway-chat-launcher${open ? " safeway-chat-launcher--open" : ""}`}
        onClick={() => (open ? closeChat() : openChat())}
        aria-expanded={open}
        aria-controls="safeway-global-chat-panel"
        aria-label={open ? "Minimize SAFEWAY assistant" : "Open SAFEWAY assistant"}
        title={open ? "Minimize SAFEWAY assistant" : "Ask SAFEWAY assistant"}
      >
        <Icon name={open ? "close" : "brain"} size={26} />
        {!open ? (
          <span className="safeway-chat-launcher__dot" aria-hidden="true" />
        ) : null}
      </button>
    </>
  );
}
