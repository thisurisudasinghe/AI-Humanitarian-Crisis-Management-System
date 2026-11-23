/**
 * Typed client for the informational SAFEWAY assistant (P2, FR-CIT-07).
 *
 * The endpoint answers questions only. It never creates, approves or rejects a
 * recommendation or decision, and it is not part of any critical-action path.
 * The same endpoint serves the global floating widget and the staff FAQ page.
 */
import { apiJson } from "./api";

export interface ChatResponseDto {
  answer: string;
  source: string;
  suggestions: string[];
  /** Empty when the configured AI provider answered successfully. */
  notice?: string;
  /** Always true for the informational chat endpoint. */
  decidesNothing?: boolean;
}

/** Fixed examples shown in the staff FAQ page; they are prompts, not service responses. */
export const FaqExamples = [
  "How do I submit a request?",
  "How can I check the status of my request?",
  "Who makes the final decision on an allocation?"
] as const;

/** Suggested prompts for the global floating assistant across public and signed-in pages. */
export const GlobalChatSuggestions = [
  "How do I report an emergency?",
  "How do I find shelter or assistance?",
  "How do I volunteer or donate?",
  "Who makes the final decision?"
] as const;

/**
 * Sends one FAQ question. `suggestions` is optional on older responses, so it
 * is normalised to an array here rather than assuming the planned shape.
 */
export async function sendChatMessage(message: string): Promise<ChatResponseDto> {
  const response = await apiJson<Partial<ChatResponseDto> | null>(
    "/api/v1/ai/chat",
    "POST",
    { message }
  );

  if (typeof response?.answer !== "string" || !response.answer.trim()) {
    throw new Error("The chat service returned an empty answer.");
  }

  return {
    answer: response.answer,
    source:
      typeof response.source === "string" && response.source.trim()
        ? response.source
        : "FAQ",
    suggestions: Array.isArray(response.suggestions)
      ? response.suggestions.filter((item): item is string => typeof item === "string")
      : [],
    notice:
      typeof response.notice === "string" && response.notice.trim()
        ? response.notice
        : "",
    decidesNothing: response.decidesNothing !== false
  };
}
