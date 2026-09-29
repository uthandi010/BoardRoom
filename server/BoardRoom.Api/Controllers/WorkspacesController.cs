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
}
