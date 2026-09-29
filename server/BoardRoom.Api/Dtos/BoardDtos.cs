namespace BoardRoom.Api.Dtos;

public record CreateBoardRequest(string Name);
public record BoardSummary(int Id, string Name, int WorkspaceId);

public record CreateColumnRequest(string Name);
public record CardSummary(int Id, string Title, string Description, int Order, int? AssigneeId, string? AssigneeName);
public record ColumnSummary(int Id, string Name, int Order, List<CardSummary> Cards);
public record BoardDetail(int Id, string Name, int WorkspaceId, List<ColumnSummary> Columns);

public record CreateCardRequest(string Title, string? Description, int? AssigneeId);

public record UpdateCardRequest(
    string? Title,
    string? Description,
    int? ColumnId,
    int? Order,
    int? AssigneeId,
    bool ClearAssignee = false
);
