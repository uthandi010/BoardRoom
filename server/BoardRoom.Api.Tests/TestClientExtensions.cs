using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BoardRoom.Api.Dtos;

namespace BoardRoom.Api.Tests;

public static class TestClientExtensions
{
    /// <summary>
    /// The API serializes enums (e.g. WorkspaceRole) as strings. The
    /// System.Net.Http.Json helpers use JsonSerializerOptions.Default on the
    /// client side unless told otherwise, so response DTOs that contain an
    /// enum need this passed explicitly when deserializing.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<AuthResponse> RegisterAsync(
        this HttpClient client, string name, string email, string password = "Password123!")
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest(name, email, password));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!;
    }

    public static void AuthorizeAs(this HttpClient client, AuthResponse auth)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
    }
}
