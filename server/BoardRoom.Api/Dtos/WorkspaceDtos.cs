using BoardRoom.Api.Models;

namespace BoardRoom.Api.Dtos;

public record CreateWorkspaceRequest(string Name);

public record WorkspaceSummary(int Id, string Name, WorkspaceRole MyRole, int MemberCount, int BoardCount);

public record InviteMemberRequest(string Email, WorkspaceRole Role);

public record MemberSummary(int UserId, string Name, string Email, WorkspaceRole Role);
