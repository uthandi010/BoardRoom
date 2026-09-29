using System.Net;
using System.Net.Http.Json;
using BoardRoom.Api.Dtos;
using BoardRoom.Api.Models;
using Xunit;

namespace BoardRoom.Api.Tests;

public class BoardCardTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public BoardCardTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient client, WorkspaceSummary workspace, AuthResponse owner)> CreateWorkspaceAsync(
        string suffix)
    {
        var client = _factory.CreateClient();
        var owner = await client.RegisterAsync($"Board Owner {suffix}", $"boardowner{suffix}@example.com");
        client.AuthorizeAs(owner);

        var workspace = await (await client.PostAsJsonAsync(
            "/api/workspaces", new CreateWorkspaceRequest($"Workspace {suffix}")))
            .Content.ReadFromJsonAsync<WorkspaceSummary>(TestClientExtensions.JsonOptions);

        return (client, workspace!, owner);
    }

    [Fact]
    public async Task CreatingBoard_SeedsThreeDefaultColumns()
    {
        var (client, workspace, _) = await CreateWorkspaceAsync("A");

        var board = await (await client.PostAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/boards", new CreateBoardRequest("Sprint Board")))
            .Content.ReadFromJsonAsync<BoardSummary>();

        var detail = await client.GetFromJsonAsync<BoardDetail>($"/api/boards/{board!.Id}");

        Assert.NotNull(detail);
        Assert.Equal(3, detail!.Columns.Count);
        Assert.Equal(new[] { "To Do", "In Progress", "Done" }, detail.Columns.Select(c => c.Name));
    }

    [Fact]
    public async Task CreatingCards_AssignsIncreasingOrder()
    {
        var (client, workspace, _) = await CreateWorkspaceAsync("B");
        var board = await (await client.PostAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/boards", new CreateBoardRequest("Kanban")))
            .Content.ReadFromJsonAsync<BoardSummary>();
        var detail = await client.GetFromJsonAsync<BoardDetail>($"/api/boards/{board!.Id}");
        var todoColumnId = detail!.Columns.First(c => c.Name == "To Do").Id;

        var card1 = await (await client.PostAsJsonAsync(
            $"/api/columns/{todoColumnId}/cards", new CreateCardRequest("Write tests", null, null)))
            .Content.ReadFromJsonAsync<CardSummary>();
        var card2 = await (await client.PostAsJsonAsync(
            $"/api/columns/{todoColumnId}/cards", new CreateCardRequest("Ship feature", null, null)))
            .Content.ReadFromJsonAsync<CardSummary>();

        Assert.Equal(0, card1!.Order);
        Assert.Equal(1, card2!.Order);
    }

    [Fact]
    public async Task MovingCard_ToColumnOnAnotherBoard_IsRejected()
    {
        var (client, workspace, _) = await CreateWorkspaceAsync("C");

        var boardOne = await (await client.PostAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/boards", new CreateBoardRequest("Board One")))
            .Content.ReadFromJsonAsync<BoardSummary>();
        var boardTwo = await (await client.PostAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/boards", new CreateBoardRequest("Board Two")))
            .Content.ReadFromJsonAsync<BoardSummary>();

        var boardOneDetail = await client.GetFromJsonAsync<BoardDetail>($"/api/boards/{boardOne!.Id}");
        var boardTwoDetail = await client.GetFromJsonAsync<BoardDetail>($"/api/boards/{boardTwo!.Id}");

        var cardOnBoardOne = await (await client.PostAsJsonAsync(
            $"/api/columns/{boardOneDetail!.Columns[0].Id}/cards",
            new CreateCardRequest("Cross-board card", null, null)))
            .Content.ReadFromJsonAsync<CardSummary>();

        var response = await client.PatchAsJsonAsync(
            $"/api/cards/{cardOnBoardOne!.Id}",
            new UpdateCardRequest(null, null, boardTwoDetail!.Columns[0].Id, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AssigningCard_ToNonMember_IsRejected()
    {
        var (client, workspace, _) = await CreateWorkspaceAsync("D");
        var board = await (await client.PostAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/boards", new CreateBoardRequest("Board")))
            .Content.ReadFromJsonAsync<BoardSummary>();
        var detail = await client.GetFromJsonAsync<BoardDetail>($"/api/boards/{board!.Id}");
        var columnId = detail!.Columns[0].Id;

        var outsider = await client.RegisterAsync("Outsider D", "outsiderd@example.com");

        var response = await client.PostAsJsonAsync(
            $"/api/columns/{columnId}/cards",
            new CreateCardRequest("Assigned card", null, outsider.UserId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeletingCard_AsNonMember_IsForbidden()
    {
        var (client, workspace, _) = await CreateWorkspaceAsync("E");
        var board = await (await client.PostAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/boards", new CreateBoardRequest("Board")))
            .Content.ReadFromJsonAsync<BoardSummary>();
        var detail = await client.GetFromJsonAsync<BoardDetail>($"/api/boards/{board!.Id}");
        var columnId = detail!.Columns[0].Id;

        var card = await (await client.PostAsJsonAsync(
            $"/api/columns/{columnId}/cards", new CreateCardRequest("Delete me", null, null)))
            .Content.ReadFromJsonAsync<CardSummary>();

        var outsider = await client.RegisterAsync("Outsider E", "outsidere@example.com");
        client.AuthorizeAs(outsider);

        var response = await client.DeleteAsync($"/api/cards/{card!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ClearAssignee_RemovesExistingAssignment()
    {
        var (client, workspace, owner) = await CreateWorkspaceAsync("F");
        var board = await (await client.PostAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/boards", new CreateBoardRequest("Board")))
            .Content.ReadFromJsonAsync<BoardSummary>();
        var detail = await client.GetFromJsonAsync<BoardDetail>($"/api/boards/{board!.Id}");
        var columnId = detail!.Columns[0].Id;

        var card = await (await client.PostAsJsonAsync(
            $"/api/columns/{columnId}/cards", new CreateCardRequest("Assigned", null, owner.UserId)))
            .Content.ReadFromJsonAsync<CardSummary>();
        Assert.Equal(owner.UserId, card!.AssigneeId);

        var response = await client.PatchAsJsonAsync(
            $"/api/cards/{card.Id}",
            new UpdateCardRequest(null, null, null, null, null, ClearAssignee: true));

        var updated = await response.Content.ReadFromJsonAsync<CardSummary>();
        Assert.Null(updated!.AssigneeId);
    }
}
