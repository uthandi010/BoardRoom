export type WorkspaceRole = "Member" | "Admin" | "Owner";

export interface AuthResponse {
  token: string;
  userId: number;
  name: string;
  email: string;
}

export interface WorkspaceSummary {
  id: number;
  name: string;
  myRole: WorkspaceRole;
  memberCount: number;
  boardCount: number;
}

export interface MemberSummary {
  userId: number;
  name: string;
  email: string;
  role: WorkspaceRole;
}

export interface BoardSummary {
  id: number;
  name: string;
  workspaceId: number;
}

export interface CardSummary {
  id: number;
  title: string;
  description: string;
  order: number;
  assigneeId: number | null;
  assigneeName: string | null;
}

export interface ColumnSummary {
  id: number;
  name: string;
  order: number;
  cards: CardSummary[];
}

export interface BoardDetail {
  id: number;
  name: string;
  workspaceId: number;
  columns: ColumnSummary[];
}
