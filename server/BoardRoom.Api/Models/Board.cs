namespace BoardRoom.Api.Models;

public class Board
{
    public int Id { get; set; }
    public int WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<BoardColumn> Columns { get; set; } = new List<BoardColumn>();
}

public class BoardColumn
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public Board? Board { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }

    public ICollection<CardItem> Cards { get; set; } = new List<CardItem>();
}

public class CardItem
{
    public int Id { get; set; }
    public int ColumnId { get; set; }
    public BoardColumn? Column { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Order { get; set; }
    public int? AssigneeId { get; set; }
    public User? Assignee { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
