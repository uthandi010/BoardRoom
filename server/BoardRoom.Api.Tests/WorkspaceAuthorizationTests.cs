using System.Net;
using System.Net.Http.Json;
using BoardRoom.Api.Dtos;
using BoardRoom.Api.Models;
using Xunit;

namespace BoardRoom.Api.Tests;

public class WorkspaceAuthorizationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public WorkspaceAuthorizationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreatingWorkspace_MakesCreatorTheOwner()
    {
        var client = _factory.CreateClient();
        var owner = await client.RegisterAsync("Owner One", "owner1@example.com");
        client.AuthorizeAs(owner);

        var response = await client.PostAsJsonAsync("/api/workspaces", new CreateWorkspaceRequest("Acme Inc"));
        response.EnsureSuccessStatusCode();
        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceSummary>(TestClientExtensions.JsonOptions);

        Assert.Equal(WorkspaceRole.Owner, workspace!.MyRole);
        Assert.Equal(1, workspace.MemberCount);
    }

    [Fact]
    public async Task NonMember_CannotSeeWorkspaceMembers()
    {
        var client = _factory.CreateClient();
        var owner = await client.RegisterAsync("Owner Two", "owner2@example.com");
        client.AuthorizeAs(owner);
        var workspace = await (await client.PostAsJsonAsync(
            "/api/workspaces", new CreateWorkspaceRequest("Private Co")))
            .Content.ReadFromJsonAsync<WorkspaceSummary>(TestClientExtensions.JsonOptions);

        var outsider = await client.RegisterAsync("Outsider", "outsider1@example.com");
        client.AuthorizeAs(outsider);

        var response = await client.GetAsync($"/api/workspaces/{workspace!.Id}/members");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Owner_CanInviteMember_AndThatMemberGainsAccess()
    {
        var client = _factory.CreateClient();
        var owner = await client.RegisterAsync("Owner Three", "owner3@example.com");
        client.AuthorizeAs(owner);
        var workspace = await (await client.PostAsJsonAsync(
            "/api/workspaces", new CreateWorkspaceRequest("Growing Co")))
            .Content.ReadFromJsonAsync<WorkspaceSummary>(TestClientExtensions.JsonOptions);

        var newMember = await client.RegisterAsync("New Member", "newmember1@example.com");

        var inviteResponse = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspace!.Id}/members",
            new InviteMemberRequest("newmember1@example.com", WorkspaceRole.Member));
        Assert.Equal(HttpStatusCode.OK, inviteResponse.StatusCode);

        client.AuthorizeAs(newMember);
        var response = await client.GetAsync($"/api/workspaces/{workspace.Id}/members");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var members = await response.Content.ReadFromJsonAsync<List<MemberSummary>>(TestClientExtensions.JsonOptions);
        Assert.Equal(2, members!.Count);
    }

    [Fact]
    public async Task PlainMember_CannotInviteOthers()
    {
        var client = _factory.CreateClient();
        var owner = await client.RegisterAsync("Owner Four", "owner4@example.com");
        client.AuthorizeAs(owner);
        var workspace = await (await client.PostAsJsonAsync(
            "/api/workspaces", new CreateWorkspaceRequest("Locked Down Co")))
            .Content.ReadFromJsonAsync<WorkspaceSummary>(TestClientExtensions.JsonOptions);

        var member = await client.RegisterAsync("Regular Member", "regularmember1@example.com");
        await client.PostAsJsonAsync(
            $"/api/workspaces/{workspace!.Id}/members",
            new InviteMemberRequest("regularmember1@example.com", WorkspaceRole.Member));

        var thirdPerson = await client.RegisterAsync("Third Person", "thirdperson1@example.com");
        client.AuthorizeAs(member);

        var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspace.Id}/members",
            new InviteMemberRequest("thirdperson1@example.com", WorkspaceRole.Member));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InvitingSomeoneAsOwner_IsRejected()
    {
        var client = _factory.CreateClient();
        var owner = await client.RegisterAsync("Owner Five", "owner5@example.com");
        client.AuthorizeAs(owner);
        var workspace = await (await client.PostAsJsonAsync(
            "/api/workspaces", new CreateWorkspaceRequest("Single Owner Co")))
            .Content.ReadFromJsonAsync<WorkspaceSummary>(TestClientExtensions.JsonOptions);

        await client.RegisterAsync("Would Be Owner", "wouldbeowner1@example.com");

        var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspace!.Id}/members",
            new InviteMemberRequest("wouldbeowner1@example.com", WorkspaceRole.Owner));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
