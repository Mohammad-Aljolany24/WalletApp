using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using WalletApp.Core.Auth;


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



       public static async Task<string> RegisterAndVerifyUserAsync(
        HttpClient client,
        IServiceProvider services,
        string email = "test@test.com",
        string password = "password123")
    {
        await client.PostAsJsonAsync("/auth/register", new { email, password });

        var response = await client.PostAsJsonAsync("/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

        // Flip IsVerified out-of-band, since there's no public endpoint for it.
        using var scope = services.CreateScope();
        var userStore = scope.ServiceProvider.GetRequiredService<IUserStore>();
        var user = await userStore.GetByEmailAsync(email);
        user!.IsVerified = true;
        await userStore.SaveAsync(user);

        return result!.Token;
    }

    public static async Task<string> RegisterAdminAndLoginAsync(
    HttpClient client,
    IServiceProvider services,
    string email = "admin@test.com",
    string password = "password123")
{
    await client.PostAsJsonAsync("/auth/register", new { email, password });

    // Promote to Admin out-of-band, then log in fresh so the token carries the role claim.
    using (var scope = services.CreateScope())
    {
        var userStore = scope.ServiceProvider.GetRequiredService<IUserStore>();
        var user = await userStore.GetByEmailAsync(email);
        user!.Role = "Admin";
        await userStore.SaveAsync(user);
    }

    var response = await client.PostAsJsonAsync("/auth/login", new { email, password });
    response.EnsureSuccessStatusCode();

    var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
    return result!.Token;
}


    private record LoginResponse(string Token);

}