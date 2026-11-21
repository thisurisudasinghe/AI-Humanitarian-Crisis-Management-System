"use client";

import Link from "next/link";
import { useRef, useState, type FormEvent } from "react";
import { AuthGuard } from "@/components/AuthGuard";
import { Icon } from "@/components/safeway/Icon";
import { Button, Skeleton, Toast } from "@/components/ui";
import { FaqExamples, sendChatMessage } from "@/lib/aiChat";
import { errorMessage } from "@/lib/api";
import { StaffRoles } from "@/lib/roles";

type ChatEntry = {
  id: number;
  role: "user" | "assistant";
  text: string;
  source?: string;
};

type Notice = { message: string; tone: "error" | "success" } | null;

export default function AiFaqChatPage() {
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
    } catch (error) {
      setNotice({ message: errorMessage(error), tone: "error" });
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
          <p>AI FAQ CHAT</p>
          <h1>Ask about the crisis workflow</h1>
          <span>Answers explain how the platform works. A person still makes every decision.</span>
        </div>
        <div>
          <Link className="btn btn-outline" href="/operations/ai">
            <Icon name="brain" /> Recommendation queue
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
            <b>Information only - this chat does not make or approve decisions.</b>
            <br />
            Only authorized human roles can record a decision or approve a critical action.
          </span>
        </div>

        <div style={{ padding: 20 }}>
          <b>Example questions</b>
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
            <h3>Conversation</h3>
            <p>Fixed-FAQ answers with an optional language-model upgrade</p>
          </div>
        </div>

        <div style={{ padding: "0 20px 8px" }}>
          {entries.length === 0 ? (
            <div className="empty-state">
              Choose an example question or type your own to start. The chat explains procedures
              only; it never changes a request or a decision.
            </div>
          ) : (
            <div style={{ display: "grid", gap: 12 }}>
              {entries.map((entry) => (
                <article className="notice" key={entry.id}>
                  <i style={{ background: entry.role === "user" ? "var(--blue)" : "var(--line)" }} />
                  <span style={{ flex: 1 }}>
                    <b>{entry.role === "user" ? "You" : "FAQ assistant"}</b>
                    <p>{entry.text}</p>
                    {entry.role === "assistant" && entry.source ? (
                      <small>Source: {entry.source}</small>
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
            <b>Follow-ups</b>
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
            <span className="field__label">Question</span>
            <textarea
              className="input"
              rows={3}
              value={question}
              onChange={(event) => setQuestion(event.target.value)}
              placeholder="Ask an FAQ question about the workflow"
            />
          </label>
          <Button type="submit" disabled={sending || !question.trim()}>
            {sending ? "Sending…" : "Ask"}
          </Button>
        </form>
      </section>
    </AuthGuard>
  );
}
