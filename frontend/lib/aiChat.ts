/**
 * Typed client for the informational FAQ chat (P2, FR-CIT-07).
 *
 * The endpoint answers questions only. It never creates, approves or rejects a
 * recommendation or decision, and it is not part of any critical-action path.
 */
import { apiJson } from "./api";

export interface ChatResponseDto {
  answer: string;
  source: string;
  suggestions: string[];
}

/** Fixed examples shown in the UI; they are prompts, not service responses. */
export const FaqExamples = [
  "How do I submit a request?",
  "How can I check the status of my request?",
  "Who makes the final decision on an allocation?"
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
    source: typeof response.source === "string" && response.source.trim()
      ? response.source
      : "FAQ",
    suggestions: Array.isArray(response.suggestions)
      ? response.suggestions.filter((item): item is string => typeof item === "string")
      : []
  };
}
