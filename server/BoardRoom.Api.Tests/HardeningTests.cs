using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Net.Http.Json;
using BoardRoom.Api.Dtos;
using BoardRoom.Api.Models;
using Xunit;

namespace BoardRoom.Api.Tests;

public class HardeningTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public HardeningTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly System.Text.Json.JsonSerializerOptions Json = TestClientExtensions.JsonOptions;

    private async Task<(HttpClient client, WorkspaceSummary workspace, AuthResponse owner)> CreateWorkspaceAsync(
        string suffix)
    {
        var client = _factory.CreateClient();
        var owner = await client.RegisterAsync($"Hard Owner {suffix}", $"hardowner{suffix}@example.com");
        client.AuthorizeAs(owner);
        var workspace = await (await client.PostAsJsonAsync(
            "/api/workspaces", new CreateWorkspaceRequest($"Hard {suffix}")))
            .Content.ReadFromJsonAsync<WorkspaceSummary>(Json);
        return (client, workspace!, owner);
    }

    private static async Task<AuthResponse> AddMemberAsync(
        HttpClient client, int workspaceId, string name, string email, WorkspaceRole role)
    {
        var user = await client.RegisterAsync(name, email);
        var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/members", new InviteMemberRequest(email, role));
        response.EnsureSuccessStatusCode();
        return user;
    }

    // ----- Validation -----

    [Fact]
    public async Task Register_WithNullFields_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(null, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithNullFields_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(null, null));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithOverlongName_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(new string('x', Limits.PersonName + 1), "longname@example.com", "Password123!"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invite_WithNullEmail_Returns400()
    {
        var (client, workspace, _) = await CreateWorkspaceAsync("V1");
        var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/members", new InviteMemberRequest(null, WorkspaceRole.Member));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateBoardAndCard_WithOverlongText_Returns400()
    {
        var (client, workspace, _) = await CreateWorkspaceAsync("V2");

        var longBoard = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/boards", new CreateBoardRequest(new string('b', Limits.EntityName + 1)));
        Assert.Equal(HttpStatusCode.BadRequest, longBoard.StatusCode);

        var board = await (await client.PostAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/boards", new CreateBoardRequest("Ok")))
            .Content.ReadFromJsonAsync<BoardSummary>();
        var detail = await client.GetFromJsonAsync<BoardDetail>($"/api/boards/{board!.Id}");
        var columnId = detail!.Columns[0].Id;

        var longTitle = await client.PostAsJsonAsync(
            $"/api/columns/{columnId}/cards", new CreateCardRequest(new string('t', Limits.CardTitle + 1), null, null));
        Assert.Equal(HttpStatusCode.BadRequest, longTitle.StatusCode);

        var longDescription = await client.PostAsJsonAsync(
            $"/api/columns/{columnId}/cards",
            new CreateCardRequest("T", new string('d', Limits.CardDescription + 1), null));
        Assert.Equal(HttpStatusCode.BadRequest, longDescription.StatusCode);
    }

    [Fact]
    public async Task GetBoard_AsNonMember_Returns403()
    {
        var (client, workspace, _) = await CreateWorkspaceAsync("V3");
        var board = await (await client.PostAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/boards", new CreateBoardRequest("Private")))
            .Content.ReadFromJsonAsync<BoardSummary>();

        var outsider = await client.RegisterAsync("Outsider V3", "outsiderv3@example.com");
        client.AuthorizeAs(outsider);

        var response = await client.GetAsync($"/api/boards/{board!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ----- Card ordering -----

    private async Task<(HttpClient client, BoardDetail board, List<CardSummary> cards)> BoardWithCardsAsync(
        string suffix, int count)
    {
        var (client, workspace, _) = await CreateWorkspaceAsync(suffix);
        var board = await (await client.PostAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/boards", new CreateBoardRequest("Order")))
            .Content.ReadFromJsonAsync<BoardSummary>();
        var detail = await client.GetFromJsonAsync<BoardDetail>($"/api/boards/{board!.Id}");
        var todo = detail!.Columns[0].Id;
        var cards = new List<CardSummary>();
        for (var i = 0; i < count; i++)
        {
            var card = await (await client.PostAsJsonAsync(
                $"/api/columns/{todo}/cards", new CreateCardRequest($"Card {i}", null, null)))
                .Content.ReadFromJsonAsync<CardSummary>();
            cards.Add(card!);
        }
        return (client, detail, cards);
    }

    private static async Task<List<string>> TitlesAsync(HttpClient client, int boardId, int columnId)
    {
        var detail = await client.GetFromJsonAsync<BoardDetail>($"/api/boards/{boardId}");
        return detail!.Columns.First(c => c.Id == columnId).Cards.Select(c => c.Title).ToList();
    }

    [Fact]
    public async Task Reordering_WithinColumn_MovesCardToDropIndex()
    {
        var (client, board, cards) = await BoardWithCardsAsync("O1", 3);
        var todo = board.Columns[0].Id;

        var response = await client.PatchAsJsonAsync(
            $"/api/cards/{cards[2].Id}", new UpdateCardRequest(null, null, null, 0, null));
        response.EnsureSuccessStatusCode();

        Assert.Equal(new[] { "Card 2", "Card 0", "Card 1" }, await TitlesAsync(client, board.Id, todo));
        var detail = await client.GetFromJsonAsync<BoardDetail>($"/api/boards/{board.Id}");
        Assert.Equal(new[] { 0, 1, 2 }, detail!.Columns[0].Cards.Select(c => c.Order));
    }

    [Fact]
    public async Task Moving_ToAnotherColumnAtIndex_ResequencesBothColumns()
    {
        var (client, board, cards) = await BoardWithCardsAsync("O2", 3);
        var todo = board.Columns[0].Id;
        var doing = board.Columns[1].Id;

        // Seed the target column with two cards, then drop Card 0 between them.
        await client.PatchAsJsonAsync($"/api/cards/{cards[1].Id}", new UpdateCardRequest(null, null, doing, null, null));
        await client.PatchAsJsonAsync($"/api/cards/{cards[2].Id}", new UpdateCardRequest(null, null, doing, null, null));
        var response = await client.PatchAsJsonAsync(
            $"/api/cards/{cards[0].Id}", new UpdateCardRequest(null, null, doing, 1, null));
        response.EnsureSuccessStatusCode();

        Assert.Equal(new[] { "Card 1", "Card 0", "Card 2" }, await TitlesAsync(client, board.Id, doing));
        Assert.Empty(await TitlesAsync(client, board.Id, todo));
    }

    [Fact]
    public async Task Moving_SourceColumnRemainsDense()
    {
        var (client, board, cards) = await BoardWithCardsAsync("O3", 3);
        var todo = board.Columns[0].Id;
        var done = board.Columns[2].Id;

        await client.PatchAsJsonAsync($"/api/cards/{cards[0].Id}", new UpdateCardRequest(null, null, done, 99, null));

        var detail = await client.GetFromJsonAsync<BoardDetail>($"/api/boards/{board.Id}");
        Assert.Equal(new[] { 0, 1 }, detail!.Columns.First(c => c.Id == todo).Cards.Select(c => c.Order));
        Assert.Equal(new[] { 0 }, detail.Columns.First(c => c.Id == done).Cards.Select(c => c.Order));
    }

    [Fact]
    public async Task NegativeOrder_Returns400()
    {
        var (client, _, cards) = await BoardWithCardsAsync("O4", 1);
        var response = await client.PatchAsJsonAsync(
            $"/api/cards/{cards[0].Id}", new UpdateCardRequest(null, null, null, -1, null));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ----- Member management -----

    [Fact]
    public async Task Owner_CanPromoteMemberToAdmin_AndDemoteAgain()
    {
        var (client, workspace, _) = await CreateWorkspaceAsync("M1");
        var member = await AddMemberAsync(client, workspace.Id, "Member M1", "memberm1@example.com", WorkspaceRole.Member);

        var promote = await client.PatchAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/members/{member.UserId}", new UpdateMemberRoleRequest(WorkspaceRole.Admin));
        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);
        var summary = await promote.Content.ReadFromJsonAsync<MemberSummary>(Json);
        Assert.Equal(WorkspaceRole.Admin, summary!.Role);

        var demote = await client.PatchAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/members/{member.UserId}", new UpdateMemberRoleRequest(WorkspaceRole.Member));
        Assert.Equal(HttpStatusCode.OK, demote.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotPromoteToAdmin_OrChangeAnAdmin()
    {
        var (client, workspace, _) = await CreateWorkspaceAsync("M2");
        var admin = await AddMemberAsync(client, workspace.Id, "Admin M2", "adminm2@example.com", WorkspaceRole.Admin);
        var otherAdmin = await AddMemberAsync(client, workspace.Id, "Admin2 M2", "admin2m2@example.com", WorkspaceRole.Admin);
        var member = await AddMemberAsync(client, workspace.Id, "Member M2", "memberm2@example.com", WorkspaceRole.Member);

        client.AuthorizeAs(admin);

        var promote = await client.PatchAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/members/{member.UserId}", new UpdateMemberRoleRequest(WorkspaceRole.Admin));
        Assert.Equal(HttpStatusCode.Forbidden, promote.StatusCode);

        var demoteAdmin = await client.PatchAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/members/{otherAdmin.UserId}", new UpdateMemberRoleRequest(WorkspaceRole.Member));
        Assert.Equal(HttpStatusCode.Forbidden, demoteAdmin.StatusCode);
    }

    [Fact]
    public async Task OwnerRole_CannotBeChangedOrRemoved_AndNoOneBecomesOwner()
    {
        var (client, workspace, owner) = await CreateWorkspaceAsync("M3");
        var admin = await AddMemberAsync(client, workspace.Id, "Admin M3", "adminm3@example.com", WorkspaceRole.Admin);
        var member = await AddMemberAsync(client, workspace.Id, "Member M3", "memberm3@example.com", WorkspaceRole.Member);
        client.AuthorizeAs(owner);

        var toOwner = await client.PatchAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/members/{member.UserId}", new UpdateMemberRoleRequest(WorkspaceRole.Owner));
        Assert.Equal(HttpStatusCode.BadRequest, toOwner.StatusCode);

        var demoteSelf = await client.PatchAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/members/{owner.UserId}", new UpdateMemberRoleRequest(WorkspaceRole.Member));
        Assert.Equal(HttpStatusCode.BadRequest, demoteSelf.StatusCode);

        var removeOwnerAsOwner = await client.DeleteAsync($"/api/workspaces/{workspace.Id}/members/{owner.UserId}");
        Assert.Equal(HttpStatusCode.BadRequest, removeOwnerAsOwner.StatusCode);

        client.AuthorizeAs(admin);
        var removeOwnerAsAdmin = await client.DeleteAsync($"/api/workspaces/{workspace.Id}/members/{owner.UserId}");
        Assert.Equal(HttpStatusCode.BadRequest, removeOwnerAsAdmin.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_Rules()
    {
        var (client, workspace, owner) = await CreateWorkspaceAsync("M4");
        var admin = await AddMemberAsync(client, workspace.Id, "Admin M4", "adminm4@example.com", WorkspaceRole.Admin);
        var admin2 = await AddMemberAsync(client, workspace.Id, "Admin2 M4", "admin2m4@example.com", WorkspaceRole.Admin);
        var member = await AddMemberAsync(client, workspace.Id, "Member M4", "memberm4@example.com", WorkspaceRole.Member);
        var member2 = await AddMemberAsync(client, workspace.Id, "Member2 M4", "member2m4@example.com", WorkspaceRole.Member);

        // Plain member cannot remove anyone.
        client.AuthorizeAs(member);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.DeleteAsync($"/api/workspaces/{workspace.Id}/members/{member2.UserId}")).StatusCode);

        // Admin cannot remove another Admin but can remove a Member.
        client.AuthorizeAs(admin);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.DeleteAsync($"/api/workspaces/{workspace.Id}/members/{admin2.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/workspaces/{workspace.Id}/members/{member2.UserId}")).StatusCode);

        // Owner can remove an Admin; the removed user loses access.
        client.AuthorizeAs(owner);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/workspaces/{workspace.Id}/members/{admin2.UserId}")).StatusCode);
        client.AuthorizeAs(admin2);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync($"/api/workspaces/{workspace.Id}/members")).StatusCode);

        // Unknown member.
        client.AuthorizeAs(owner);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.DeleteAsync($"/api/workspaces/{workspace.Id}/members/{member2.UserId}")).StatusCode);
    }

    // ----- Startup config -----

    [Fact]
    public void ShortJwtKey_OutsideDevelopment_FailsStartup()
    {
        using var factory = new KeyFactory("too-short");
        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("Jwt:Key", ex.ToString());
    }

    private sealed class KeyFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
    {
        private readonly string _key;

        public KeyFactory(string key) => _key = key;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Jwt:Key", _key);
        }
    }
}
