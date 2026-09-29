using BoardRoom.Api.Data;
using BoardRoom.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BoardRoom.Api.Services;

/// <summary>
/// Centralizes the "is this user allowed to touch this workspace" check so
/// every controller enforces membership and role the same way.
/// </summary>
public class WorkspaceAuthorizationService
{
    private readonly BoardRoomContext _context;

    public WorkspaceAuthorizationService(BoardRoomContext context)
    {
        _context = context;
    }

    public Task<WorkspaceMember?> FindMembershipAsync(int workspaceId, int userId) =>
        _context.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId);

    /// <summary>
    /// Roles are ordered Member &lt; Admin &lt; Owner, so a higher role
    /// satisfies any requirement a lower role would.
    /// </summary>
    public bool Satisfies(WorkspaceRole actual, WorkspaceRole required) => actual >= required;
}
