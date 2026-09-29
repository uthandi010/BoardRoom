import { useEffect, useState, useCallback } from "react";
import type { DragEvent, FormEvent } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, Plus, Trash2, User } from "lucide-react";
import { TopNav } from "../components/TopNav";
import { Modal } from "../components/Modal";
import * as boardsApi from "../api/boards";
import * as cardsApi from "../api/cards";
import * as workspacesApi from "../api/workspaces";
import type { BoardDetail, MemberSummary } from "../types";
import { ApiError } from "../api/client";

export function BoardPage() {
  const { boardId } = useParams<{ boardId: string }>();
  const id = Number(boardId);
  const navigate = useNavigate();

  const [board, setBoard] = useState<BoardDetail | null>(null);
  const [members, setMembers] = useState<MemberSummary[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [activeCardColumnId, setActiveCardColumnId] = useState<number | null>(null);
  const [draggedCard, setDraggedCard] = useState<{ cardId: number; fromColumnId: number } | null>(null);

  const loadBoard = useCallback(async () => {
    try {
      const detail = await boardsApi.getBoard(id);
      setBoard(detail);
      const memberList = await workspacesApi.listMembers(detail.workspaceId);
      setMembers(memberList);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Failed to load this board.");
    }
  }, [id]);

  useEffect(() => {
    loadBoard();
  }, [loadBoard]);

  const handleAddCard = async (title: string, description: string, assigneeId: number | null) => {
    if (activeCardColumnId === null) return;
    await cardsApi.createCard(activeCardColumnId, title, description, assigneeId);
    setActiveCardColumnId(null);
    await loadBoard();
  };

  const handleDeleteCard = async (cardId: number) => {
    try {
      await cardsApi.deleteCard(cardId);
      await loadBoard();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not delete that card.");
    }
  };

  const handleDragStart = (cardId: number, fromColumnId: number) => (event: DragEvent) => {
    setDraggedCard({ cardId, fromColumnId });
    event.dataTransfer.effectAllowed = "move";
  };

  const handleDrop = (toColumnId: number) => async (event: DragEvent) => {
    event.preventDefault();
    const dragged = draggedCard;
    setDraggedCard(null);
    if (!dragged || dragged.fromColumnId === toColumnId) {
      return;
    }
    try {
      await cardsApi.updateCard(dragged.cardId, { columnId: toColumnId });
      await loadBoard();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not move that card.");
    }
  };

  if (error) {
    return (
      <div className="page">
        <TopNav />
        <main className="page-content">
          <div className="form-error" role="alert">
            {error}
          </div>
        </main>
      </div>
    );
  }

  if (!board) {
    return (
      <div className="page">
        <TopNav />
        <main className="page-content">
          <p className="muted">Loading board...</p>
        </main>
      </div>
    );
  }

  return (
    <div className="page">
      <TopNav />
      <main className="page-content">
        <button
          type="button"
          className="btn btn-ghost back-link"
          onClick={() => navigate(`/workspaces/${board.workspaceId}`)}
        >
          <ArrowLeft size={16} />
          Back to workspace
        </button>

        <div className="page-header">
          <h1>{board.name}</h1>
        </div>

        <div className="board-columns">
          {board.columns.map((column) => (
            <div
              key={column.id}
              className="board-column"
              onDragOver={(event) => event.preventDefault()}
              onDrop={handleDrop(column.id)}
            >
              <div className="board-column-header">
                <h3>{column.name}</h3>
                <span className="muted">{column.cards.length}</span>
              </div>

              <div className="board-column-cards">
                {column.cards.map((card) => (
                  <div
                    key={card.id}
                    className="board-card-item"
                    draggable
                    onDragStart={handleDragStart(card.id, column.id)}
                  >
                    <div className="board-card-item-top">
                      <p>{card.title}</p>
                      <button
                        type="button"
                        className="icon-button"
                        onClick={() => handleDeleteCard(card.id)}
                        aria-label="Delete card"
                      >
                        <Trash2 size={14} />
                      </button>
                    </div>
                    {card.description && <p className="muted card-description">{card.description}</p>}
                    {card.assigneeName && (
                      <span className="assignee-chip">
                        <User size={12} />
                        {card.assigneeName}
                      </span>
                    )}
                  </div>
                ))}
              </div>

              <button
                type="button"
                className="btn btn-ghost add-card-button"
                onClick={() => setActiveCardColumnId(column.id)}
              >
                <Plus size={14} />
                Add card
              </button>
            </div>
          ))}
        </div>
      </main>

      {activeCardColumnId !== null && (
        <AddCardModal
          members={members}
          onClose={() => setActiveCardColumnId(null)}
          onSubmit={handleAddCard}
        />
      )}
    </div>
  );
}

interface AddCardModalProps {
  members: MemberSummary[];
  onClose: () => void;
  onSubmit: (title: string, description: string, assigneeId: number | null) => Promise<void>;
}

function AddCardModal({ members, onClose, onSubmit }: AddCardModalProps) {
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [assigneeId, setAssigneeId] = useState<string>("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    if (!title.trim()) {
      setError("Card title is required.");
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await onSubmit(title.trim(), description.trim(), assigneeId ? Number(assigneeId) : null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not create card.");
      setSubmitting(false);
    }
  };

  return (
    <Modal title="Add a card" onClose={onClose}>
      <form className="card-form" onSubmit={handleSubmit}>
        {error && (
          <div className="form-error" role="alert">
            {error}
          </div>
        )}
        <label className="field">
          <span>Title</span>
          <input type="text" value={title} onChange={(event) => setTitle(event.target.value)} autoFocus />
        </label>
        <label className="field">
          <span>Description</span>
          <textarea
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            rows={3}
          />
        </label>
        <label className="field">
          <span>Assignee</span>
          <select value={assigneeId} onChange={(event) => setAssigneeId(event.target.value)}>
            <option value="">Unassigned</option>
            {members.map((member) => (
              <option key={member.userId} value={member.userId}>
                {member.name}
              </option>
            ))}
          </select>
        </label>
        <button type="submit" className="btn btn-primary" disabled={submitting}>
          {submitting ? "Adding..." : "Add card"}
        </button>
      </form>
    </Modal>
  );
}
