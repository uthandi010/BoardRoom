namespace BoardRoom.Api.Models;

/// <summary>
/// A user's role within a single workspace. Roles are per-workspace, not
/// global, so the same person can be an Owner of one workspace and a
/// Member of another.
/// </summary>
public enum WorkspaceRole
{
    Member = 0,
    Admin = 1,
    Owner = 2,
}
