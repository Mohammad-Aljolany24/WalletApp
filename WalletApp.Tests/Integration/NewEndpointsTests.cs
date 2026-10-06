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

        var page = await response.Content
            .ReadFromJsonAsync<PagedResponse<TransactionResponse>>();

        page.Should().NotBeNull();
        page!.Items.Should().HaveCount(3);
        page.Items[0].Type.Should().Be("Withdrawn");
        page.Items[0].Amount.Should().Be(30);
        page.Items[1].Type.Should().Be("Deposited");
        page.Items[1].Amount.Should().Be(50);
        page.Items[2].Type.Should().Be("Deposited");
        page.Items[2].Amount.Should().Be(100);

        // Fewer items than the default limit (20) → no more pages.
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Transactions_EmptyForNewUser()
    {
        var client = _factory.CreateClient();
        var token = await TestHelpers.RegisterAndLoginAsync(client, "empty-tx@test.com");
        var authed = AuthedClient(token);

        var response = await authed.GetAsync("/wallet/transactions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await response.Content
            .ReadFromJsonAsync<PagedResponse<TransactionResponse>>();

        page.Should().NotBeNull();
        page!.Items.Should().BeEmpty();
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Transactions_Pagination_SecondPageExcludesFirstPage()
    {
        var client = _factory.CreateClient();
        var token = await TestHelpers.RegisterAndVerifyUserAsync(
            client, _factory.Services, "tx-page@test.com");
        var authed = AuthedClient(token);

        // Five deposits: 10, 20, 30, 40, 50 → newest first is 50, 40, 30, 20, 10
        await authed.PostAsync("/wallet/deposit?amount=10", null);
        await authed.PostAsync("/wallet/deposit?amount=20", null);
        await authed.PostAsync("/wallet/deposit?amount=30", null);
        await authed.PostAsync("/wallet/deposit?amount=40", null);
        await authed.PostAsync("/wallet/deposit?amount=50", null);

        // Page 1: limit=2 → newest two (50, 40) + cursor
        var page1Response = await authed.GetAsync("/wallet/transactions?limit=2");
        page1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page1 = await page1Response.Content
            .ReadFromJsonAsync<PagedResponse<TransactionResponse>>();

        page1.Should().NotBeNull();
        page1!.Items.Should().HaveCount(2);
        page1.Items[0].Amount.Should().Be(50);
        page1.Items[1].Amount.Should().Be(40);
        page1.NextCursor.Should().NotBeNullOrEmpty();

        // Page 2: fetch with cursor → next two (30, 20)
        var page2Response = await authed.GetAsync(
            $"/wallet/transactions?limit=2&cursor={Uri.EscapeDataString(page1.NextCursor!)}");
        page2Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page2 = await page2Response.Content
            .ReadFromJsonAsync<PagedResponse<TransactionResponse>>();

        page2.Should().NotBeNull();
        page2!.Items.Should().HaveCount(2);
        page2.Items[0].Amount.Should().Be(30);
        page2.Items[1].Amount.Should().Be(20);
        page2.NextCursor.Should().NotBeNullOrEmpty();

        // No overlap between pages
        var page1Occurred = page1.Items.Select(t => t.OccurredAt).ToHashSet();
        var page2Occurred = page2.Items.Select(t => t.OccurredAt).ToHashSet();
        page1Occurred.Overlaps(page2Occurred).Should().BeFalse();

        // Page 3: last item (10), no more cursor
        var page3Response = await authed.GetAsync(
            $"/wallet/transactions?limit=2&cursor={Uri.EscapeDataString(page2.NextCursor!)}");
        page3Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page3 = await page3Response.Content
            .ReadFromJsonAsync<PagedResponse<TransactionResponse>>();

        page3.Should().NotBeNull();
        page3!.Items.Should().HaveCount(1);
        page3.Items[0].Amount.Should().Be(10);
        page3.NextCursor.Should().BeNull();
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

        var page = await response.Content
            .ReadFromJsonAsync<PagedResponse<AdminUserResponse>>();

        page.Should().NotBeNull();
        page!.Items.Should().Contain(u => u.Email == "admin-list@test.com");
        page.Items.Should().Contain(u => u.Email == "listed-user@test.com");
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

    /// <summary>
    /// Mirrors the wire shape returned by the paginated endpoints.
    /// </summary>
    private record PagedResponse<T>(List<T> Items, string? NextCursor);
}