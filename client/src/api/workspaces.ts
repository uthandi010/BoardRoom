import { apiRequest } from "./client";
import type { MemberSummary, WorkspaceRole, WorkspaceSummary } from "../types";

export function listWorkspaces(): Promise<WorkspaceSummary[]> {
  return apiRequest<WorkspaceSummary[]>("/api/workspaces");
}

export function createWorkspace(name: string): Promise<WorkspaceSummary> {
  return apiRequest<WorkspaceSummary>("/api/workspaces", { method: "POST", body: { name } });
}

export function listMembers(workspaceId: number): Promise<MemberSummary[]> {
  return apiRequest<MemberSummary[]>(`/api/workspaces/${workspaceId}/members`);
}

export function updateMemberRole(
  workspaceId: number,
  userId: number,
  role: WorkspaceRole
): Promise<MemberSummary> {
  return apiRequest<MemberSummary>(`/api/workspaces/${workspaceId}/members/${userId}`, {
    method: "PATCH",
    body: { role },
  });
}

export function removeMember(workspaceId: number, userId: number): Promise<void> {
  return apiRequest<void>(`/api/workspaces/${workspaceId}/members/${userId}`, { method: "DELETE" });
}

export function inviteMember(
  workspaceId: number,
  email: string,
  role: WorkspaceRole
): Promise<MemberSummary> {
  return apiRequest<MemberSummary>(`/api/workspaces/${workspaceId}/members`, {
    method: "POST",
    body: { email, role },
  });
}
