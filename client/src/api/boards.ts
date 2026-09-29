import { apiRequest } from "./client";
import type { BoardDetail, BoardSummary, ColumnSummary } from "../types";

export function listBoards(workspaceId: number): Promise<BoardSummary[]> {
  return apiRequest<BoardSummary[]>(`/api/workspaces/${workspaceId}/boards`);
}

export function createBoard(workspaceId: number, name: string): Promise<BoardSummary> {
  return apiRequest<BoardSummary>(`/api/workspaces/${workspaceId}/boards`, {
    method: "POST",
    body: { name },
  });
}

export function getBoard(boardId: number): Promise<BoardDetail> {
  return apiRequest<BoardDetail>(`/api/boards/${boardId}`);
}

export function createColumn(boardId: number, name: string): Promise<ColumnSummary> {
  return apiRequest<ColumnSummary>(`/api/boards/${boardId}/columns`, {
    method: "POST",
    body: { name },
  });
}
