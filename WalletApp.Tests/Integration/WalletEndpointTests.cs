using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;

namespace WalletApp.Tests.Integration;

public class WalletEndpointTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public WalletEndpointTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    // ============================================
    // AUTH REQUIREMENT
    // ============================================

    [Fact]
    public async Task Deposit_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/wallet/deposit?amount=100", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Balance_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/wallet/balance");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ============================================
    // FULL FLOW
    // ============================================

    [Fact]
    public async Task FullFlow_RegisterLoginDepositCheckBalance_WorksEndToEnd()
    {
        var client = _factory.CreateClient();

        var token = await TestHelpers.RegisterAndLoginAsync(
            client, "flow@test.com", "password123");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Deposit 100
        var depositResponse = await client.PostAsync("/wallet/deposit?amount=100", null);
        depositResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Check balance
        var balanceResponse = await client.GetAsync("/wallet/balance");
        balanceResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var balance = await balanceResponse.Content.ReadFromJsonAsync<BalanceResponse>();
        balance!.Balance.Should().Be(100);
    }

    [Fact]
    public async Task MultipleDeposits_AccumulateBalance()
    {
        var client = _factory.CreateClient();
        var token = await TestHelpers.RegisterAndLoginAsync(
            client, "multi@test.com", "password123");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        await client.PostAsync("/wallet/deposit?amount=100", null);
        await client.PostAsync("/wallet/deposit?amount=50", null);
        await client.PostAsync("/wallet/deposit?amount=25", null);

        var balanceResponse = await client.GetAsync("/wallet/balance");
        var balance = await balanceResponse.Content.ReadFromJsonAsync<BalanceResponse>();

        balance!.Balance.Should().Be(175);
    }

    [Fact]
    public async Task Withdraw_ReducesBalance()
    {
        var client = _factory.CreateClient();
        var token = await TestHelpers.RegisterAndLoginAsync(
            client, "withdraw@test.com", "password123");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        await client.PostAsync("/wallet/deposit?amount=100", null);
        await client.PostAsync("/wallet/withdraw?amount=30", null);

        var balanceResponse = await client.GetAsync("/wallet/balance");
        var balance = await balanceResponse.Content.ReadFromJsonAsync<BalanceResponse>();

        balance!.Balance.Should().Be(70);
    }

    [Fact]
    public async Task Withdraw_MoreThanBalance_ReturnsError()
    {
        var client = _factory.CreateClient();
        var token = await TestHelpers.RegisterAndLoginAsync(
            client, "insufficient@test.com", "password123");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        await client.PostAsync("/wallet/deposit?amount=50", null);

        var response = await client.PostAsync("/wallet/withdraw?amount=100", null);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ============================================
    // USER ISOLATION
    // ============================================

    [Fact]
    public async Task TwoUsers_HaveSeparateWallets()
    {
        // User 1 deposits 100
        var client1 = _factory.CreateClient();
        var token1 = await TestHelpers.RegisterAndLoginAsync(
            client1, "user1@test.com", "password123");
        client1.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token1);
        await client1.PostAsync("/wallet/deposit?amount=100", null);

        // User 2 deposits 50
        var client2 = _factory.CreateClient();
        var token2 = await TestHelpers.RegisterAndLoginAsync(
            client2, "user2@test.com", "password123");
        client2.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token2);
        await client2.PostAsync("/wallet/deposit?amount=50", null);

        // Verify each sees only their own balance
        var b1 = await (await client1.GetAsync("/wallet/balance"))
            .Content.ReadFromJsonAsync<BalanceResponse>();
        var b2 = await (await client2.GetAsync("/wallet/balance"))
            .Content.ReadFromJsonAsync<BalanceResponse>();

        b1!.Balance.Should().Be(100);
        b2!.Balance.Should().Be(50);
    }

    private record BalanceResponse(decimal Balance);
}