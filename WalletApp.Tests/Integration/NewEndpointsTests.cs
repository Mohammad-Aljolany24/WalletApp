using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using WalletApp.Core.Auth;

namespace WalletApp.Tests.Integration;

public class NewEndpointsTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public NewEndpointsTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient AuthedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ============================================
    // GET /auth/me
    // ============================================

    [Fact]
    public async Task Me_ReturnsCurrentUserProfile()
    {
        var client = _factory.CreateClient();
        var token = await TestHelpers.RegisterAndLoginAsync(client, "me@test.com");
        var authed = AuthedClient(token);

        var response = await authed.GetAsync("/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        body!.Email.Should().Be("me@test.com");
        body.Role.Should().Be("User");
        body.IsVerified.Should().BeFalse();
        body.IsFrozen.Should().BeFalse();
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ============================================
    // GET /wallet/transactions
    // ============================================

    [Fact]
    public async Task Transactions_ReturnsEventsNewestFirst()
    {
        var client = _factory.CreateClient();
        var token = await TestHelpers.RegisterAndVerifyUserAsync(
            client, _factory.Services, "tx@test.com");
        var authed = AuthedClient(token);

        await authed.PostAsync("/wallet/deposit?amount=100", null);
        await authed.PostAsync("/wallet/deposit?amount=50", null);
        await authed.PostAsync("/wallet/withdraw?amount=30", null);

        var response = await authed.GetAsync("/wallet/transactions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var txs = await response.Content.ReadFromJsonAsync<List<TransactionResponse>>();

        txs.Should().HaveCount(3);
        txs![0].Type.Should().Be("Withdrawn");
        txs[0].Amount.Should().Be(30);
        txs[1].Type.Should().Be("Deposited");
        txs[1].Amount.Should().Be(50);
        txs[2].Type.Should().Be("Deposited");
        txs[2].Amount.Should().Be(100);
    }

    [Fact]
    public async Task Transactions_EmptyForNewUser()
    {
        var client = _factory.CreateClient();
        var token = await TestHelpers.RegisterAndLoginAsync(client, "empty-tx@test.com");
        var authed = AuthedClient(token);

        var response = await authed.GetAsync("/wallet/transactions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var txs = await response.Content.ReadFromJsonAsync<List<TransactionResponse>>();
        txs.Should().BeEmpty();
    }

    // ============================================
    // GET /admin/users
    // ============================================

    [Fact]
    public async Task AdminUsers_ReturnsAllUsers_NoPasswordHash()
    {
        var client = _factory.CreateClient();
        var adminToken = await TestHelpers.RegisterAdminAndLoginAsync(
            client, _factory.Services, "admin-list@test.com");
        var adminClient = AuthedClient(adminToken);

        // Create another user so there's something to list.
        await TestHelpers.RegisterAndLoginAsync(client, "listed-user@test.com");

        var response = await adminClient.GetAsync("/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("passwordHash", "password hashes must never leave the API");

        var users = await response.Content.ReadFromJsonAsync<List<AdminUserResponse>>();
        users.Should().Contain(u => u.Email == "admin-list@test.com");
        users.Should().Contain(u => u.Email == "listed-user@test.com");
    }

    [Fact]
    public async Task AdminUsers_NonAdmin_Returns403()
    {
        var client = _factory.CreateClient();
        var token = await TestHelpers.RegisterAndLoginAsync(client, "not-admin@test.com");
        var authed = AuthedClient(token);

        var response = await authed.GetAsync("/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ============================================
    // Response DTOs for tests
    // ============================================

    private record MeResponse(Guid Id, string Email, string Role, bool IsVerified, bool IsFrozen);

    private record TransactionResponse(string Type, decimal Amount, DateTime OccurredAt);

    private record AdminUserResponse(
        Guid Id, string Email, string Role,
        bool IsVerified, bool IsFrozen, DateTime CreatedAt);
}