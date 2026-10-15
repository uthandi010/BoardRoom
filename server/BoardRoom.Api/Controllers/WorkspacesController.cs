using BoardRoom.Api.Auth;
using BoardRoom.Api.Data;
using BoardRoom.Api.Dtos;
using BoardRoom.Api.Models;
using BoardRoom.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoardRoom.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workspaces")]
public class WorkspacesController : ControllerBase
{
    private readonly BoardRoomContext _context;
    private readonly WorkspaceAuthorizationService _authz;

    public WorkspacesController(BoardRoomContext context, WorkspaceAuthorizationService authz)
    {
        _context = context;
        _authz = authz;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkspaceSummary>>> GetMyWorkspaces()
    {
        var userId = User.GetUserId();

        var summaries = await _context.WorkspaceMembers
            .Where(m => m.UserId == userId)
            .Select(m => new WorkspaceSummary(
                m.Workspace!.Id,
                m.Workspace.Name,
                m.Role,
                m.Workspace.Members.Count,
                m.Workspace.Boards.Count))
            .ToListAsync();

        return Ok(summaries);
    }

    [HttpPost]
    public async Task<ActionResult<WorkspaceSummary>> Create(CreateWorkspaceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Workspace name is required." });
        }
        if (request.Name.Trim().Length > Limits.EntityName)
        {
            return BadRequest(new { message = $"Workspace name must be at most {Limits.EntityName} characters." });
        }

        var userId = User.GetUserId();
        var workspace = new Workspace { Name = request.Name.Trim(), OwnerId = userId };
        workspace.Members.Add(new WorkspaceMember { UserId = userId, Role = WorkspaceRole.Owner });

        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync();

        return Ok(new WorkspaceSummary(workspace.Id, workspace.Name, WorkspaceRole.Owner, 1, 0));
    }

    [HttpGet("{workspaceId:int}/members")]
    public async Task<ActionResult<List<MemberSummary>>> GetMembers(int workspaceId)
    {
        var membership = await _authz.FindMembershipAsync(workspaceId, User.GetUserId());
        if (membership is null)
        {
            return Forbid();
        }

        var members = await _context.WorkspaceMembers
            .Where(m => m.WorkspaceId == workspaceId)
            .Select(m => new MemberSummary(m.UserId, m.User!.Name, m.User.Email, m.Role))
            .ToListAsync();

        return Ok(members);
    }

    [HttpPost("{workspaceId:int}/members")]
    public async Task<ActionResult<MemberSummary>> InviteMember(int workspaceId, InviteMemberRequest request)
    {
        var membership = await _authz.FindMembershipAsync(workspaceId, User.GetUserId());
        if (membership is null)
        {
            return Forbid();
        }
        if (!_authz.Satisfies(membership.Role, WorkspaceRole.Admin))
        {
            return Forbid();
        }

        if (request.Role == WorkspaceRole.Owner)
        {
            return BadRequest(new { message = "A workspace can only have one owner." });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        if (!Enum.IsDefined(request.Role))
        {
            return BadRequest(new { message = "Invalid role." });
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var invitedUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (invitedUser is null)
        {
            return NotFound(new { message = "No account found with that email. They need to register first." });
        }

        var alreadyMember = await _context.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == invitedUser.Id);
        if (alreadyMember)
        {
            return Conflict(new { message = "That user is already a member of this workspace." });
        }

        var newMember = new WorkspaceMember
        {
            WorkspaceId = workspaceId,
            UserId = invitedUser.Id,
            Role = request.Role,
        };
        _context.WorkspaceMembers.Add(newMember);
        await _context.SaveChangesAsync();

        return Ok(new MemberSummary(invitedUser.Id, invitedUser.Name, invitedUser.Email, newMember.Role));
    }

    [HttpPatch("{workspaceId:int}/members/{userId:int}")]
    public async Task<ActionResult<MemberSummary>> UpdateMemberRole(
        int workspaceId, int userId, UpdateMemberRoleRequest request)
    {
        var actor = await _authz.FindMembershipAsync(workspaceId, User.GetUserId());
        if (actor is null || !_authz.Satisfies(actor.Role, WorkspaceRole.Admin))
        {
            return Forbid();
        }

        if (!Enum.IsDefined(request.Role))
        {
            return BadRequest(new { message = "Invalid role." });
        }
        if (request.Role == WorkspaceRole.Owner)
        {
            return BadRequest(new { message = "A workspace can only have one owner." });
        }

        var target = await _context.WorkspaceMembers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId);
        if (target is null)
        {
            return NotFound(new { message = "That user is not a member of this workspace." });
        }

        if (target.Role == WorkspaceRole.Owner)
        {
            return BadRequest(new { message = "The workspace owner's role cannot be changed." });
        }

        // Only the Owner may promote to Admin or change an Admin's role.
        var touchesAdmin = request.Role == WorkspaceRole.Admin || target.Role == WorkspaceRole.Admin;
        if (touchesAdmin && actor.Role != WorkspaceRole.Owner)
        {
            return Forbid();
        }

        target.Role = request.Role;
        await _context.SaveChangesAsync();

        return Ok(new MemberSummary(target.UserId, target.User!.Name, target.User.Email, target.Role));
    }

    [HttpDelete("{workspaceId:int}/members/{userId:int}")]
    public async Task<IActionResult> RemoveMember(int workspaceId, int userId)
    {
        var actor = await _authz.FindMembershipAsync(workspaceId, User.GetUserId());
        if (actor is null || !_authz.Satisfies(actor.Role, WorkspaceRole.Admin))
        {
            return Forbid();
        }

        var target = await _context.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId);
        if (target is null)
        {
            return NotFound(new { message = "That user is not a member of this workspace." });
        }

        if (target.Role == WorkspaceRole.Owner)
        {
            return BadRequest(new { message = "The workspace owner cannot be removed." });
        }

        // Only the Owner may remove an Admin.
        if (target.Role == WorkspaceRole.Admin && actor.Role != WorkspaceRole.Owner)
        {
            return Forbid();
        }

        // Unassign their cards in this workspace so no card points at a non-member.
        var assignedCards = await _context.Cards
            .Where(c => c.AssigneeId == userId && c.Column!.Board!.WorkspaceId == workspaceId)
            .ToListAsync();
        foreach (var card in assignedCards)
        {
            card.AssigneeId = null;
        }

        _context.WorkspaceMembers.Remove(target);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
