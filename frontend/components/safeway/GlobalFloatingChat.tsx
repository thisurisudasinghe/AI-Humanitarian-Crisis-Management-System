"use client";

import { useAuth } from "@/lib/auth";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { ApiError } from "@/lib/api";
import { sendChatMessage } from "@/lib/aiChat";
import { useI18n } from "@/lib/i18n";
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
const DEDICATED_CHAT_ROUTES = new Set(["/operations/ai/chat", "/operations/ai/chat/"]);

function suggestionsFor(locale: string): string[] {
  if (locale === "si") return ["හදිසි අවස්ථාවක් වාර්තා කරන්නේ කෙසේද?", "ආරක්ෂිත ස්ථානයක් හෝ සහාය සොයාගන්නේ කෙසේද?", "ස්වේච්ඡාවෙන් හෝ පරිත්‍යාග කරන්නේ කෙසේද?", "අවසන් තීරණය ගන්නේ කවුද?"];
  if (locale === "ta") return ["அவசர நிலையை எப்படி புகாரளிப்பது?", "தங்குமிடம் அல்லது உதவியை எப்படி கண்டுபிடிப்பது?", "தன்னார்வம் அல்லது நன்கொடை எப்படி செய்வது?", "இறுதி முடிவை யார் எடுப்பது?"];
  return ["How do I report an emergency?", "How do I find shelter or assistance?", "How do I volunteer or donate?", "Who makes the final decision?"];
}

function createWelcome(text: string): ChatMessage {
  return { id: "welcome", role: "assistant", text, source: "SAFEWAY", createdAt: 0 };
}

function createId(): string {
  if (typeof globalThis.crypto?.randomUUID === "function") return globalThis.crypto.randomUUID();
  return `msg-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

function userStorageKey(userId: string): string {
  return `safeway.chat.v1.user.${encodeURIComponent(userId)}`;
}

function isChatMessage(value: unknown): value is ChatMessage {
  if (!value || typeof value !== "object") return false;
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
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) return null;
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

export function GlobalFloatingChat() {
  const pathname = usePathname();
  const { user, ready } = useAuth();
  const { locale, t } = useI18n();

  const [open, setOpen] = useState(false);
  const [messages, setMessages] = useState<ChatMessage[]>(() => [createWelcome(t("chat.welcome"))]);
  const [suggestions, setSuggestions] = useState<string[]>(() => suggestionsFor(locale));
  const [draft, setDraft] = useState("");
  const [sending, setSending] = useState(false);
  const [storageReady, setStorageReady] = useState(false);

  const launcherRef = useRef<HTMLButtonElement>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);
  const panelRef = useRef<HTMLElement>(null);
  const logRef = useRef<HTMLDivElement>(null);

  const userId = user?.userId ?? null;
  const storageKey = userId ? userStorageKey(userId) : GUEST_STORAGE_KEY;
  const isDedicatedRoute = pathname ? DEDICATED_CHAT_ROUTES.has(pathname) : false;

  useEffect(() => {
    if (!ready) return;
    setStorageReady(false);
    setSending(false);
    setSuggestions(suggestionsFor(locale));
    try {
      if (userId) window.sessionStorage.removeItem(GUEST_STORAGE_KEY);
      const storage = userId ? window.localStorage : window.sessionStorage;
      const loaded = readMessages(storage, storageKey);
      if (loaded) {
        setMessages(loaded.map((m) => (m.id === "welcome" ? { ...m, text: t("chat.welcome") } : m)));
      } else {
        setMessages([createWelcome(t("chat.welcome"))]);
      }
    } catch {
      setMessages([createWelcome(t("chat.welcome"))]);
    }
    setStorageReady(true);
  }, [ready, userId, storageKey]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    setSuggestions((prev) => (prev.length === 0 || prev === suggestionsFor("en") || prev === suggestionsFor("si") || prev === suggestionsFor("ta") ? suggestionsFor(locale) : prev));
    setMessages((prev) => prev.map((m) => (m.id === "welcome" ? { ...m, text: t("chat.welcome") } : m)));
  }, [locale, t]);

  useEffect(() => {
    if (!ready || !storageReady) return;
    try {
      const storage = userId ? window.localStorage : window.sessionStorage;
      storage.setItem(storageKey, JSON.stringify(messages.slice(-MAX_STORED_MESSAGES)));
    } catch {
      // ignore
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
    if (!open) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") closeChat();
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [open, closeChat]);

  useEffect(() => {
    if (!open) return;
    const log = logRef.current;
    if (log) log.scrollTop = log.scrollHeight;
  }, [messages, open, sending]);

  const friendlyError = useCallback((error: unknown): string => {
    if (error instanceof ApiError) {
      if (error.status === 429) return t("chat.errorRateLimit");
      if (error.status === 401) return t("chat.errorExpired");
      if (error.status >= 500) return t("chat.errorServer");
    }
    const detail = localizedErrorMessage(error, t);
    return detail && detail !== "Something went wrong." ? detail : t("chat.errorConnection");
  }, [t]);

  const formatSource = useCallback((source: string): string => {
    if (source === "Ai") return t("chat.sourceAi");
    if (source === "Rule" || source === "FAQ") return t("chat.sourceFaq");
    if (source === "SAFEWAY") return t("chat.sourceWelcome");
    return source;
  }, [t]);

  const ask = useCallback(async (rawQuestion: string) => {
    const question = rawQuestion.trim();
    if (!question || sending) return;
    const userMessage: ChatMessage = { id: createId(), role: "user", text: question, createdAt: Date.now() };
    setMessages((current) => [...current, userMessage].slice(-MAX_STORED_MESSAGES));
    setDraft("");
    setSending(true);
    try {
      const response = await sendChatMessage(question, locale);
      const assistantMessage: ChatMessage = {
        id: createId(),
        role: "assistant",
        text: response.answer,
        source: response.source,
        notice: response.notice,
        createdAt: Date.now()
      };
      setMessages((current) => [...current, assistantMessage].slice(-MAX_STORED_MESSAGES));
      setSuggestions(response.suggestions.length > 0 ? response.suggestions.slice(0, 4) : suggestionsFor(locale));
    } catch (error) {
      const errorMessageText: ChatMessage = {
        id: createId(),
        role: "assistant",
        text: t("chat.errorUnavailable", { detail: friendlyError(error) }),
        source: "Error",
        createdAt: Date.now()
      };
      setMessages((current) => [...current, errorMessageText].slice(-MAX_STORED_MESSAGES));
    } finally {
      setSending(false);
    }
  }, [sending, locale, t, friendlyError]);

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void ask(draft);
  }

  function clearChat() {
    try {
      const storage = userId ? window.localStorage : window.sessionStorage;
      storage.removeItem(storageKey);
    } catch {
      // ignore
    }
    setMessages([createWelcome(t("chat.welcome"))]);
    setSuggestions(suggestionsFor(locale));
    setDraft("");
    focusInput();
  }

  function handlePanelKeyDown(event: ReactKeyboardEvent<HTMLElement>) {
    if (event.key === "Escape") {
      event.stopPropagation();
      closeChat();
      return;
    }
    if (event.key !== "Tab") return;
    const panel = panelRef.current;
    if (!panel) return;
    const focusable = Array.from(
      panel.querySelectorAll<HTMLElement>(
        'button:not([disabled]), textarea:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])'
      )
    );
    if (focusable.length === 0) return;
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

  if (isDedicatedRoute) return null;

  return (
    <>
      {open ? (
        <section
          ref={panelRef}
          id="safeway-global-chat-panel"
          className="safeway-chat-panel"
          role="dialog"
          aria-modal="false"
          aria-label={t("chat.title")}
          onKeyDown={handlePanelKeyDown}
        >
          <header className="safeway-chat-header">
            <div className="safeway-chat-header__identity">
              <span className="safeway-chat-header__avatar" aria-hidden="true">
                <Icon name="brain" size={22} />
              </span>
              <div>
                <strong>{t("chat.title")}</strong>
                <span>{t("chat.subtitle")}</span>
              </div>
            </div>
            <div className="safeway-chat-header__actions">
              <button type="button" className="safeway-chat-text-button" onClick={clearChat} aria-label={t("chat.clearAria")}>
                {t("chat.clear")}
              </button>
              <button type="button" className="safeway-chat-icon-button" onClick={closeChat} aria-label={t("chat.minimizeAria")} title={t("chat.minimizeAria")}>
                <Icon name="close" size={18} />
              </button>
            </div>
          </header>

          <div className="safeway-chat-safety" role="note">
            <Icon name="shield" size={15} />
            <span>{t("chat.safety")}</span>
          </div>

          <div ref={logRef} className="safeway-chat-log" role="log" aria-live="polite" aria-relevant="additions" aria-busy={sending}>
            {messages.map((message) => (
              <article key={message.id} className={`safeway-chat-message safeway-chat-message--${message.role}`}>
                <div className="safeway-chat-message__meta">
                  <b>{message.role === "user" ? t("chat.you") : message.source === "Error" ? t("chat.unavailable") : t("chat.title")}</b>
                  {message.role === "assistant" && message.source && message.source !== "Error" ? <span>{formatSource(message.source)}</span> : null}
                </div>
                <p>{message.text}</p>
                {message.notice ? <small className="safeway-chat-message__notice">{message.notice}</small> : null}
              </article>
            ))}
            {sending ? (
              <div className="safeway-chat-typing" role="status">
                <span /><span /><span />
                <span className="safeway-chat-sr-only">{t("chat.typing")}</span>
              </div>
            ) : null}
          </div>

          {suggestions.length > 0 ? (
            <div className="safeway-chat-suggestions" aria-label={t("chat.title")}>
              {suggestions.slice(0, 4).map((suggestion) => (
                <button key={suggestion} type="button" className="safeway-chat-suggestion" disabled={sending} onClick={() => void ask(suggestion)}>
                  {suggestion}
                </button>
              ))}
            </div>
          ) : null}

          <form className="safeway-chat-form" onSubmit={submit}>
            <label className="safeway-chat-sr-only" htmlFor="safeway-chat-input">{t("chat.inputLabel")}</label>
            <textarea
              id="safeway-chat-input"
              ref={inputRef}
              rows={2}
              maxLength={2000}
              value={draft}
              placeholder={t("chat.placeholder")}
              onChange={(event) => setDraft(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter" && !event.shiftKey) {
                  event.preventDefault();
                  void ask(draft);
                }
              }}
            />
            <button type="submit" className="safeway-chat-send" disabled={sending || !draft.trim()} aria-label={t("chat.send")} title={t("chat.send")}>
              <Icon name="arrow" size={18} />
            </button>
          </form>

          <footer className="safeway-chat-footer">{t("chat.footer")}</footer>
        </section>
      ) : null}

      <button
        ref={launcherRef}
        type="button"
        className={`safeway-chat-launcher${open ? " safeway-chat-launcher--open" : ""}`}
        onClick={() => (open ? closeChat() : openChat())}
        aria-expanded={open}
        aria-controls="safeway-global-chat-panel"
        aria-label={open ? t("chat.minimize") : t("chat.open")}
        title={open ? t("chat.minimize") : t("chat.open")}
      >
        <Icon name={open ? "close" : "brain"} size={26} />
        {!open ? <span className="safeway-chat-launcher__dot" aria-hidden="true" /> : null}
      </button>
    </>
  );
}
