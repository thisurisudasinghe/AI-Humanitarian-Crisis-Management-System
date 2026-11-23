"use client";

import Link from "next/link";
import { localizedErrorMessage } from "@/lib/i18n/errors";
import { useRef, useState, type FormEvent } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import { Button, Skeleton, Toast } from "@/components/ui";
import { FaqExamples, MAX_CHAT_MESSAGE_LENGTH, sendChatMessage } from "@/lib/aiChat";

import { useT } from "@/lib/i18n";
import { StaffRoles } from "@/lib/roles";

type ChatEntry = {
  id: number;
  role: "user" | "assistant";
  text: string;
  source?: string;
};

type Notice = { message: string; tone: "error" | "success" } | null;

export default function AiFaqChatPage() {
  const t = useT();
  const [question, setQuestion] = useState("");
  const [entries, setEntries] = useState<ChatEntry[]>([]);
  const [suggestions, setSuggestions] = useState<string[]>([]);
  const [sending, setSending] = useState(false);
  const [notice, setNotice] = useState<Notice>(null);
  const nextId = useRef(1);

  async function ask(rawQuestion: string) {
    const message = rawQuestion.trim();
    if (!message || sending) {
      return;
    }

    const userId = nextId.current++;
    setEntries((current) => [
      ...current,
      { id: userId, role: "user", text: message }
    ]);
    setQuestion("");
    setNotice(null);
    setSending(true);

    try {
      const answer = await sendChatMessage(message);
      setEntries((current) => [
        ...current,
        {
          id: nextId.current++,
          role: "assistant",
          text: answer.answer,
          source: answer.source
        }
      ]);
      setSuggestions(answer.suggestions);
      if (answer.notice) {
        setNotice({ message: answer.notice, tone: "success" });
      }
    } catch (error) {
      setNotice({ message: localizedErrorMessage(error, t), tone: "error" });
    } finally {
      setSending(false);
    }
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void ask(question);
  }

  return (
    <AuthGuard allowedRoles={StaffRoles}>
      <div className="ops-title">
        <div>
          <p>{t("operations.chat.eyebrow")}</p>
          <h1>{t("operations.chat.title")}</h1>
          <span>{t("operations.chat.subtitle")}</span>
        </div>
        <div>
          <Link className="btn btn-outline" href="/operations/ai">
            <Icon name="brain" /> {t("operations.shared.recommendationQueue")}
          </Link>
        </div>
      </div>

      <Toast
        message={notice?.message ?? null}
        tone={notice?.tone ?? "error"}
        onDismiss={() => setNotice(null)}
      />

      <section className="ops-card" style={{ marginBottom: 16 }}>
        <div
          className="notice"
          role="note"
          style={{ margin: 20, marginBottom: 0, padding: 14, borderLeft: "3px solid var(--blue)" }}
        >
          <Icon name="shield" size={16} />
          <span>
            <b>{t("operations.chat.noticeTitle")}</b>
            <br />
            {t("operations.chat.noticeBody")}
            <br />
            {t("operations.chat.noticeSafety")}
          </span>
        </div>

        <div style={{ padding: 20 }}>
          <b>{t("operations.chat.examples")}</b>
          <div className="actions" style={{ marginTop: 10 }}>
            {FaqExamples.map((example) => (
              <Button
                key={example}
                variant="secondary"
                onClick={() => setQuestion(example)}
                disabled={sending}
              >
                {example}
              </Button>
            ))}
          </div>
        </div>
      </section>

      <section className="ops-card">
        <div className="card-heading">
          <div>
            <h3>{t("operations.chat.conversation")}</h3>
            <p>{t("operations.chat.conversationSubtitle")}</p>
          </div>
        </div>

        <div style={{ padding: "0 20px 8px" }}>
          {entries.length === 0 ? (
            <div className="empty-state">
              {t("operations.chat.empty")}
            </div>
          ) : (
            <div style={{ display: "grid", gap: 12 }}>
              {entries.map((entry) => (
                <article className="notice" key={entry.id}>
                  <i style={{ background: entry.role === "user" ? "var(--blue)" : "var(--line)" }} />
                  <span style={{ flex: 1 }}>
                    <b>{entry.role === "user" ? t("operations.chat.you") : t("operations.chat.assistant")}</b>
                    <p>{entry.text}</p>
                    {entry.role === "assistant" && entry.source ? (
                      <small>{t("operations.shared.source", { source: entry.source })}</small>
                    ) : null}
                  </span>
                </article>
              ))}
              {sending ? (
                <div style={{ padding: 4 }}>
                  <Skeleton rows={2} />
                </div>
              ) : null}
            </div>
          )}
        </div>

        {suggestions.length > 0 ? (
          <div style={{ padding: "8px 20px" }}>
            <b>{t("operations.chat.followUps")}</b>
            <div className="actions" style={{ marginTop: 8 }}>
              {suggestions.map((suggestion, index) => (
                <Button
                  key={`${suggestion}-${index}`}
                  variant="secondary"
                  onClick={() => setQuestion(suggestion)}
                  disabled={sending}
                >
                  {suggestion}
                </Button>
              ))}
            </div>
          </div>
        ) : null}

        <form className="filters" onSubmit={submit} style={{ padding: 20 }}>
          <label className="field" style={{ flex: 1 }}>
            <span className="field__label">{t("operations.chat.questionLabel")}</span>
            <textarea
              className="input"
              rows={3}
              maxLength={MAX_CHAT_MESSAGE_LENGTH}
              value={question}
              onChange={(event) => setQuestion(event.target.value)}
              placeholder={t("operations.chat.questionPlaceholder")}
            />
            <span className="field__help">
              {t("operations.chat.maxLengthHelp", { max: MAX_CHAT_MESSAGE_LENGTH })}
            </span>
          </label>
          <Button type="submit" disabled={sending || !question.trim()}>
            {sending ? t("operations.chat.sending") : t("operations.chat.ask")}
          </Button>
        </form>
      </section>
    </AuthGuard>
  );
}
