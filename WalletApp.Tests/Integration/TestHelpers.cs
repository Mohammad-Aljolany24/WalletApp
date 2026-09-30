using System.Net.Http.Json;

namespace WalletApp.Tests.Integration;

public static class TestHelpers
{
    public static async Task<string> RegisterAndLoginAsync(
        HttpClient client,
        string email = "test@test.com",
        string password = "password123")
    {
        await client.PostAsJsonAsync("/auth/register", new { email, password });

        var response = await client.PostAsJsonAsync("/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return result!.Token;
    }

    private record LoginResponse(string Token);
}