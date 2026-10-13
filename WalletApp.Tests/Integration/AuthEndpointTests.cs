using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace WalletApp.Tests.Integration;

public class AuthEndpointTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthEndpointTests(CustomWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    // ============================================
    // REGISTER
    // ============================================

    [Fact]
    public async Task Register_WithValidData_ReturnsUserIdAndEmail()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = "newuser@test.com",
            password = "password123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        body.Should().NotBeNull();
        body!.Email.Should().Be("newuser@test.com");
        body.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsError()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = "dup@test.com",
            password = "password123"
        });

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = "dup@test.com",
            password = "password123"
        });

       response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ============================================
    // LOGIN
    // ============================================

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAccessAndRefreshTokens()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = "login@test.com",
            password = "password123"
        });

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "login@test.com",
            password = "password123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
        body.AccessTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsError()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "ghost@test.com",
            password = "password123"
        });

       response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsError()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = "wrongpass@test.com",
            password = "password123"
        });

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "wrongpass@test.com",
            password = "wrong_password"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ============================================
    // REFRESH
    // ============================================

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewPairAndRotates()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = "refresh@test.com",
            password = "password123"
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "refresh@test.com",
            password = "password123"
        });
        var original = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = original!.RefreshToken
        });

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var next = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>();

        next!.AccessToken.Should().NotBeNullOrEmpty();
        next.RefreshToken.Should().NotBeNullOrEmpty();
        next.RefreshToken.Should().NotBe(original.RefreshToken, "rotation must issue a new token");
    }

    [Fact]
    public async Task Refresh_WithRevokedToken_RevokesWholeFamily()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = "theft@test.com",
            password = "password123"
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "theft@test.com",
            password = "password123"
        });
        var original = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // First rotation — succeeds, old token is now revoked.
        var firstRefresh = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = original!.RefreshToken
        });
        firstRefresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = await firstRefresh.Content.ReadFromJsonAsync<LoginResponse>();

        // Replay the original (now-revoked) token — theft signal.
        var replay = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = original.RefreshToken
        });
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The whole family is now dead — the rotated token is also invalid.
        var afterTheft = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = rotated!.RefreshToken
        });
        afterTheft.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithBogusToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = "not-a-real-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ============================================
    // LOGOUT
    // ============================================

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = "logout@test.com",
            password = "password123"
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "logout@test.com",
            password = "password123"
        });
        var tokens = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var logoutResponse = await _client.PostAsJsonAsync("/api/v1/auth/logout", new
        {
            refreshToken = tokens!.RefreshToken
        });
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Refresh must now fail.
        var refreshAfterLogout = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = tokens.RefreshToken
        });
        refreshAfterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Response shapes
    private record RegisterResponse(Guid Id, string Email);
    private record LoginResponse(
        string AccessToken,
        string RefreshToken,
        DateTime AccessTokenExpiresAt);
}