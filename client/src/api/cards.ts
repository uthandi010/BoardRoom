import { apiRequest } from "./client";
import type { CardSummary } from "../types";

export function createCard(
  columnId: number,
  title: string,
  description: string,
  assigneeId: number | null
): Promise<CardSummary> {
  return apiRequest<CardSummary>(`/api/columns/${columnId}/cards`, {
    method: "POST",
    body: { title, description, assigneeId },
  });
}

export interface UpdateCardInput {
  title?: string;
  description?: string;
  columnId?: number;
  order?: number;
  assigneeId?: number | null;
  clearAssignee?: boolean;
}

export function updateCard(cardId: number, input: UpdateCardInput): Promise<CardSummary> {
  return apiRequest<CardSummary>(`/api/cards/${cardId}`, {
    method: "PATCH",
    body: {
      title: input.title ?? null,
      description: input.description ?? null,
      columnId: input.columnId ?? null,
      order: input.order ?? null,
      assigneeId: input.assigneeId ?? null,
      clearAssignee: input.clearAssignee ?? false,
    },
  });
}

export function deleteCard(cardId: number): Promise<void> {
  return apiRequest<void>(`/api/cards/${cardId}`, { method: "DELETE" });
}
