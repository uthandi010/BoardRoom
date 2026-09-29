import { useEffect, useState, useCallback } from "react";
import type { FormEvent } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Plus, ArrowLeft } from "lucide-react";
import { TopNav } from "../components/TopNav";
import * as boardsApi from "../api/boards";
import * as workspacesApi from "../api/workspaces";
import { useAuth } from "../auth/AuthContext";
import type { BoardSummary, MemberSummary, WorkspaceRole } from "../types";
import { ApiError } from "../api/client";

export function WorkspacePage() {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const id = Number(workspaceId);
  const navigate = useNavigate();
  const { user } = useAuth();

  const [boards, setBoards] = useState<BoardSummary[] | null>(null);
  const [members, setMembers] = useState<MemberSummary[] | null>(null);
  const [newBoardName, setNewBoardName] = useState("");
  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteRole, setInviteRole] = useState<WorkspaceRole>("Member");
  const [error, setError] = useState<string | null>(null);
  const [inviteError, setInviteError] = useState<string | null>(null);

  const loadAll = useCallback(async () => {
    try {
      const [boardList, memberList] = await Promise.all([
        boardsApi.listBoards(id),
        workspacesApi.listMembers(id),
      ]);
      setBoards(boardList);
      setMembers(memberList);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to load this workspace.");
    }
  }, [id]);

  useEffect(() => {
    loadAll();
  }, [loadAll]);

  const myMembership = members?.find((m) => m.userId === user?.userId);
  const canInvite = myMembership?.role === "Owner" || myMembership?.role === "Admin";

  const handleCreateBoard = async (event: FormEvent) => {
    event.preventDefault();
    if (!newBoardName.trim()) return;

    try {
      const board = await boardsApi.createBoard(id, newBoardName.trim());
      setBoards((current) => (current ? [...current, board] : [board]));
      setNewBoardName("");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not create board.");
    }
  };

  const handleInvite = async (event: FormEvent) => {
    event.preventDefault();
    if (!inviteEmail.trim()) return;

    setInviteError(null);
    try {
      const member = await workspacesApi.inviteMember(id, inviteEmail.trim(), inviteRole);
      setMembers((current) => (current ? [...current, member] : [member]));
      setInviteEmail("");
    } catch (err) {
      setInviteError(err instanceof ApiError ? err.message : "Could not invite that person.");
    }
  };

  return (
    <div className="page">
      <TopNav />
      <main className="page-content">
        <button type="button" className="btn btn-ghost back-link" onClick={() => navigate("/workspaces")}>
          <ArrowLeft size={16} />
          All workspaces
        </button>

        {error && (
          <div className="form-error" role="alert">
            {error}
          </div>
        )}

        <div className="workspace-layout">
          <section>
            <div className="page-header">
              <h1>Boards</h1>
            </div>

            <form className="inline-form" onSubmit={handleCreateBoard}>
              <input
                type="text"
                placeholder="New board name"
                value={newBoardName}
                onChange={(event) => setNewBoardName(event.target.value)}
              />
              <button type="submit" className="btn btn-primary">
                <Plus size={16} />
                Create board
              </button>
            </form>

            {boards === null ? (
              <p className="muted">Loading boards...</p>
            ) : boards.length === 0 ? (
              <p className="muted">No boards yet. Create the first one above.</p>
            ) : (
              <div className="card-grid">
                {boards.map((board) => (
                  <button
                    key={board.id}
                    type="button"
                    className="board-card"
                    onClick={() => navigate(`/boards/${board.id}`)}
                  >
                    <h3>{board.name}</h3>
                  </button>
                ))}
              </div>
            )}
          </section>

          <section className="members-panel">
            <div className="page-header">
              <h2>Members</h2>
            </div>

            {members === null ? (
              <p className="muted">Loading members...</p>
            ) : (
              <ul className="member-list">
                {members.map((member) => (
                  <li key={member.userId}>
                    <div>
                      <strong>{member.name}</strong>
                      <span className="muted">{member.email}</span>
                    </div>
                    <span className={`role-badge role-${member.role.toLowerCase()}`}>{member.role}</span>
                  </li>
                ))}
              </ul>
            )}

            {canInvite && (
              <form className="invite-form" onSubmit={handleInvite}>
                <h3>Invite someone</h3>
                <p className="muted">
                  They need to already have a BoardRoom account with this email.
                </p>
                {inviteError && (
                  <div className="form-error" role="alert">
                    {inviteError}
                  </div>
                )}
                <input
                  type="email"
                  placeholder="teammate@example.com"
                  value={inviteEmail}
                  onChange={(event) => setInviteEmail(event.target.value)}
                />
                <select
                  value={inviteRole}
                  onChange={(event) => setInviteRole(event.target.value as WorkspaceRole)}
                >
                  <option value="Member">Member</option>
                  <option value="Admin">Admin</option>
                </select>
                <button type="submit" className="btn btn-secondary">
                  Invite
                </button>
              </form>
            )}
          </section>
        </div>
      </main>
    </div>
  );
}
