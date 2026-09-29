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
public class BoardsController : ControllerBase
{
    private static readonly string[] DefaultColumns = { "To Do", "In Progress", "Done" };

    private readonly BoardRoomContext _context;
    private readonly WorkspaceAuthorizationService _authz;

    public BoardsController(BoardRoomContext context, WorkspaceAuthorizationService authz)
    {
        _context = context;
        _authz = authz;
    }

    [HttpGet("workspaces/{workspaceId:int}/boards")]
    public async Task<ActionResult<List<BoardSummary>>> GetBoards(int workspaceId)
    {
        var membership = await _authz.FindMembershipAsync(workspaceId, User.GetUserId());
        if (membership is null)
        {
            return Forbid();
        }

        var boards = await _context.Boards
            .Where(b => b.WorkspaceId == workspaceId)
            .Select(b => new BoardSummary(b.Id, b.Name, b.WorkspaceId))
            .ToListAsync();

        return Ok(boards);
    }

    [HttpPost("workspaces/{workspaceId:int}/boards")]
    public async Task<ActionResult<BoardSummary>> CreateBoard(int workspaceId, CreateBoardRequest request)
    {
        var membership = await _authz.FindMembershipAsync(workspaceId, User.GetUserId());
        if (membership is null)
        {
            return Forbid();
        }
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Board name is required." });
        }

        var board = new Board { WorkspaceId = workspaceId, Name = request.Name.Trim() };
        _context.Boards.Add(board);
        await _context.SaveChangesAsync();

        // Seed a default set of columns so a freshly created board isn't empty.
        for (var i = 0; i < DefaultColumns.Length; i++)
        {
            _context.BoardColumns.Add(new BoardColumn { BoardId = board.Id, Name = DefaultColumns[i], Order = i });
        }
        await _context.SaveChangesAsync();

        return Ok(new BoardSummary(board.Id, board.Name, board.WorkspaceId));
    }

    [HttpGet("boards/{boardId:int}")]
    public async Task<ActionResult<BoardDetail>> GetBoard(int boardId)
    {
        var board = await _context.Boards
            .Include(b => b.Columns.OrderBy(c => c.Order))
            .ThenInclude(c => c.Cards.OrderBy(card => card.Order))
            .ThenInclude(card => card.Assignee)
            .FirstOrDefaultAsync(b => b.Id == boardId);

        if (board is null)
        {
            return NotFound();
        }

        var membership = await _authz.FindMembershipAsync(board.WorkspaceId, User.GetUserId());
        if (membership is null)
        {
            return Forbid();
        }

        var detail = new BoardDetail(
            board.Id,
            board.Name,
            board.WorkspaceId,
            board.Columns.Select(c => new ColumnSummary(
                c.Id,
                c.Name,
                c.Order,
                c.Cards.Select(card => new CardSummary(
                    card.Id, card.Title, card.Description, card.Order, card.AssigneeId, card.Assignee?.Name
                )).ToList()
            )).ToList()
        );

        return Ok(detail);
    }

    [HttpPost("boards/{boardId:int}/columns")]
    public async Task<ActionResult<ColumnSummary>> CreateColumn(int boardId, CreateColumnRequest request)
    {
        var board = await _context.Boards.FindAsync(boardId);
        if (board is null)
        {
            return NotFound();
        }

        var membership = await _authz.FindMembershipAsync(board.WorkspaceId, User.GetUserId());
        if (membership is null)
        {
            return Forbid();
        }
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Column name is required." });
        }

        var maxOrder = await _context.BoardColumns
            .Where(c => c.BoardId == boardId)
            .Select(c => (int?)c.Order)
            .MaxAsync() ?? -1;

        var column = new BoardColumn { BoardId = boardId, Name = request.Name.Trim(), Order = maxOrder + 1 };
        _context.BoardColumns.Add(column);
        await _context.SaveChangesAsync();

        return Ok(new ColumnSummary(column.Id, column.Name, column.Order, new List<CardSummary>()));
    }
}
