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
[Route("api")]
public class CardsController : ControllerBase
{
    private readonly BoardRoomContext _context;
    private readonly WorkspaceAuthorizationService _authz;

    public CardsController(BoardRoomContext context, WorkspaceAuthorizationService authz)
    {
        _context = context;
        _authz = authz;
    }

    private async Task<Workspace?> GetWorkspaceForColumnAsync(int columnId)
    {
        var column = await _context.BoardColumns
            .Include(c => c.Board)
            .ThenInclude(b => b!.Workspace)
            .FirstOrDefaultAsync(c => c.Id == columnId);

        return column?.Board?.Workspace;
    }

    [HttpPost("columns/{columnId:int}/cards")]
    public async Task<ActionResult<CardSummary>> CreateCard(int columnId, CreateCardRequest request)
    {
        var column = await _context.BoardColumns.FindAsync(columnId);
        if (column is null)
        {
            return NotFound();
        }

        var workspace = await GetWorkspaceForColumnAsync(columnId);
        if (workspace is null)
        {
            return NotFound();
        }

        var membership = await _authz.FindMembershipAsync(workspace.Id, User.GetUserId());
        if (membership is null)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { message = "Card title is required." });
        }

        if (request.Title.Trim().Length > Limits.CardTitle)
        {
            return BadRequest(new { message = $"Card title must be at most {Limits.CardTitle} characters." });
        }

        if (request.Description is not null && request.Description.Trim().Length > Limits.CardDescription)
        {
            return BadRequest(new { message = $"Card description must be at most {Limits.CardDescription} characters." });
        }

        if (request.AssigneeId is int assigneeId)
        {
            var assigneeIsMember = await _context.WorkspaceMembers
                .AnyAsync(m => m.WorkspaceId == workspace.Id && m.UserId == assigneeId);
            if (!assigneeIsMember)
            {
                return BadRequest(new { message = "Assignee must be a member of this workspace." });
            }
        }

        var maxOrder = await _context.Cards
            .Where(c => c.ColumnId == columnId)
            .Select(c => (int?)c.Order)
            .MaxAsync() ?? -1;

        var card = new CardItem
        {
            ColumnId = columnId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            AssigneeId = request.AssigneeId,
            Order = maxOrder + 1,
        };
        _context.Cards.Add(card);
        await _context.SaveChangesAsync();

        var assigneeName = request.AssigneeId is null
            ? null
            : (await _context.Users.FindAsync(request.AssigneeId))?.Name;

        return Ok(new CardSummary(card.Id, card.Title, card.Description, card.Order, card.AssigneeId, assigneeName));
    }

    [HttpPatch("cards/{cardId:int}")]
    public async Task<ActionResult<CardSummary>> UpdateCard(int cardId, UpdateCardRequest request)
    {
        var card = await _context.Cards.Include(c => c.Column).FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null)
        {
            return NotFound();
        }

        var workspace = await GetWorkspaceForColumnAsync(card.ColumnId);
        if (workspace is null)
        {
            return NotFound();
        }

        var membership = await _authz.FindMembershipAsync(workspace.Id, User.GetUserId());
        if (membership is null)
        {
            return Forbid();
        }

        if (request.Title is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(new { message = "Card title cannot be blank." });
            }
            if (request.Title.Trim().Length > Limits.CardTitle)
            {
                return BadRequest(new { message = $"Card title must be at most {Limits.CardTitle} characters." });
            }
            card.Title = request.Title.Trim();
        }

        if (request.Description is not null)
        {
            if (request.Description.Trim().Length > Limits.CardDescription)
            {
                return BadRequest(new { message = $"Card description must be at most {Limits.CardDescription} characters." });
            }
            card.Description = request.Description.Trim();
        }

        if (request.Order is < 0)
        {
            return BadRequest(new { message = "Order cannot be negative." });
        }

        var sourceColumnId = card.ColumnId;
        var targetColumnId = request.ColumnId ?? sourceColumnId;
        var moved = targetColumnId != sourceColumnId;

        if (moved)
        {
            var targetColumn = await _context.BoardColumns
                .FirstOrDefaultAsync(c => c.Id == targetColumnId);

            if (targetColumn is null || targetColumn.BoardId != card.Column!.BoardId)
            {
                return BadRequest(new { message = "Target column must belong to the same board." });
            }
        }

        var reorder = moved || request.Order is not null;

        if (request.ClearAssignee)
        {
            card.AssigneeId = null;
        }
        else if (request.AssigneeId is int assigneeId)
        {
            var assigneeIsMember = await _context.WorkspaceMembers
                .AnyAsync(m => m.WorkspaceId == workspace.Id && m.UserId == assigneeId);
            if (!assigneeIsMember)
            {
                return BadRequest(new { message = "Assignee must be a member of this workspace." });
            }
            card.AssigneeId = assigneeId;
        }

        // Re-sequence the affected column(s) so Order stays a dense 0..n-1
        // sequence: request.Order is the drop index within the target column
        // (clamped to the end; omitted means append when changing column).
        await using var transaction = await _context.Database.BeginTransactionAsync();

        if (reorder)
        {
            var siblings = await _context.Cards
                .Where(c => c.ColumnId == targetColumnId && c.Id != card.Id)
                .OrderBy(c => c.Order).ThenBy(c => c.Id)
                .ToListAsync();

            var index = Math.Min(request.Order ?? siblings.Count, siblings.Count);
            siblings.Insert(index, card);
            card.ColumnId = targetColumnId;
            for (var i = 0; i < siblings.Count; i++)
            {
                siblings[i].Order = i;
            }

            if (moved)
            {
                var sourceCards = await _context.Cards
                    .Where(c => c.ColumnId == sourceColumnId && c.Id != card.Id)
                    .OrderBy(c => c.Order).ThenBy(c => c.Id)
                    .ToListAsync();
                for (var i = 0; i < sourceCards.Count; i++)
                {
                    sourceCards[i].Order = i;
                }
            }
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        var assigneeName = card.AssigneeId is null
            ? null
            : (await _context.Users.FindAsync(card.AssigneeId))?.Name;

        return Ok(new CardSummary(card.Id, card.Title, card.Description, card.Order, card.AssigneeId, assigneeName));
    }

    [HttpDelete("cards/{cardId:int}")]
    public async Task<IActionResult> DeleteCard(int cardId)
    {
        var card = await _context.Cards.FindAsync(cardId);
        if (card is null)
        {
            return NotFound();
        }

        var workspace = await GetWorkspaceForColumnAsync(card.ColumnId);
        if (workspace is null)
        {
            return NotFound();
        }

        var membership = await _authz.FindMembershipAsync(workspace.Id, User.GetUserId());
        if (membership is null)
        {
            return Forbid();
        }

        _context.Cards.Remove(card);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
