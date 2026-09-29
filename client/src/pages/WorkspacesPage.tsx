import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { Plus, Users, LayoutGrid } from "lucide-react";
import { TopNav } from "../components/TopNav";
import * as workspacesApi from "../api/workspaces";
import type { WorkspaceSummary } from "../types";
import { ApiError } from "../api/client";

export function WorkspacesPage() {
  const navigate = useNavigate();
  const [workspaces, setWorkspaces] = useState<WorkspaceSummary[] | null>(null);
  const [newName, setNewName] = useState("");
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    workspacesApi
      .listWorkspaces()
      .then(setWorkspaces)
      .catch((err) => setError(err instanceof ApiError ? err.message : "Failed to load workspaces."));
  }, []);

  const handleCreate = async (event: FormEvent) => {
    event.preventDefault();
    if (!newName.trim()) return;

    setCreating(true);
    setError(null);
    try {
      const workspace = await workspacesApi.createWorkspace(newName.trim());
      setWorkspaces((current) => (current ? [...current, workspace] : [workspace]));
      setNewName("");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not create workspace.");
    } finally {
      setCreating(false);
    }
  };

  return (
    <div className="page">
      <TopNav />
      <main className="page-content">
        <div className="page-header">
          <h1>Your workspaces</h1>
          <p>A workspace is a shared space for a team. Each one has its own boards and members.</p>
        </div>

        <form className="inline-form" onSubmit={handleCreate}>
          <input
            type="text"
            placeholder="New workspace name"
            value={newName}
            onChange={(event) => setNewName(event.target.value)}
          />
          <button type="submit" className="btn btn-primary" disabled={creating}>
            <Plus size={16} />
            Create workspace
          </button>
        </form>

        {error && (
          <div className="form-error" role="alert">
            {error}
          </div>
        )}

        {workspaces === null ? (
          <p className="muted">Loading workspaces...</p>
        ) : workspaces.length === 0 ? (
          <p className="muted">You're not in any workspace yet. Create one to get started.</p>
        ) : (
          <div className="card-grid">
            {workspaces.map((workspace) => (
              <button
                key={workspace.id}
                type="button"
                className="workspace-card"
                onClick={() => navigate(`/workspaces/${workspace.id}`)}
              >
                <div className="workspace-card-top">
                  <h2>{workspace.name}</h2>
                  <span className={`role-badge role-${workspace.myRole.toLowerCase()}`}>
                    {workspace.myRole}
                  </span>
                </div>
                <div className="workspace-card-stats">
                  <span>
                    <LayoutGrid size={14} /> {workspace.boardCount} board
                    {workspace.boardCount === 1 ? "" : "s"}
                  </span>
                  <span>
                    <Users size={14} /> {workspace.memberCount} member
                    {workspace.memberCount === 1 ? "" : "s"}
                  </span>
                </div>
              </button>
            ))}
          </div>
        )}
      </main>
    </div>
  );
}
