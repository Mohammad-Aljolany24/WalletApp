using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WalletApp.Core.Auth;

namespace WalletApp.Tests.Integration;

public class AuthorizationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public AuthorizationTests(CustomWebApplicationFactory<Program> factory)
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

    private async Task<Guid> GetUserIdAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userStore = scope.ServiceProvider.GetRequiredService<IUserStore>();
        var user = await userStore.GetByEmailAsync(email);
        return user!.Id;
    }

    private async Task FreezeUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userStore = scope.ServiceProvider.GetRequiredService<IUserStore>();
        var user = await userStore.GetByEmailAsync(email);
        user!.IsFrozen = true;
        await userStore.SaveAsync(user);
    }

    // ============================================
    // AccountNotFrozen policy (deposit)
    // ============================================

    [Fact]
    public async Task UnverifiedUser_CanDeposit()
    {
        // KYC gates withdrawal, not deposit. Unverified users can fund their account.
        var client = _factory.CreateClient();
        var token = await TestHelpers.RegisterAndLoginAsync(client, "unverified-deposit@test.com");
        var authed = AuthedClient(token);

        var response = await authed.PostAsync("/wallet/deposit?amount=100", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FrozenUser_CannotDeposit()
    {
        var client = _factory.CreateClient();
        var token = await TestHelpers.RegisterAndLoginAsync(client, "frozen-deposit@test.com");
        await FreezeUserAsync("frozen-deposit@test.com");

        var authed = AuthedClient(token);
        var response = await authed.PostAsync("/wallet/deposit?amount=100", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ============================================
    // CanTrade policy (withdraw)
    // ============================================

    [Fact]
    public async Task UnverifiedUser_CannotWithdraw()
    {
        var client = _factory.CreateClient();
        var token = await TestHelpers.RegisterAndLoginAsync(client, "unverified-withdraw@test.com");
        var authed = AuthedClient(token);

        await authed.PostAsync("/wallet/deposit?amount=100", null);

        var response = await authed.PostAsync("/wallet/withdraw?amount=50", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task VerifiedUser_CanWithdraw()
    {
        var client = _factory.CreateClient();
        var token = await TestHelpers.RegisterAndVerifyUserAsync(
            client, _factory.Services, "verified-withdraw@test.com");
        var authed = AuthedClient(token);

        await authed.PostAsync("/wallet/deposit?amount=100", null);
        var response = await authed.PostAsync("/wallet/withdraw?amount=50", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FreezingUser_TakesEffectImmediately_NoRelogin()
    {
        // User is verified and withdraws successfully...
        var client = _factory.CreateClient();
        var token = await TestHelpers.RegisterAndVerifyUserAsync(
            client, _factory.Services, "freeze-immediate@test.com");
        var authed = AuthedClient(token);

        await authed.PostAsync("/wallet/deposit?amount=100", null);
        var firstWithdraw = await authed.PostAsync("/wallet/withdraw?amount=10", null);
        firstWithdraw.StatusCode.Should().Be(HttpStatusCode.OK);

        // ...then an admin freezes them mid-session.
        await FreezeUserAsync("freeze-immediate@test.com");

        // Same token, next request — must now be rejected.
        var secondWithdraw = await authed.PostAsync("/wallet/withdraw?amount=10", null);
        secondWithdraw.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FrozenUser_CanStillSeeBalance()
    {
        // Freeze gates money movement, not read access.
        var client = _factory.CreateClient();
        var token = await TestHelpers.RegisterAndLoginAsync(client, "frozen-balance@test.com");
        await FreezeUserAsync("frozen-balance@test.com");

        var authed = AuthedClient(token);
        var response = await authed.GetAsync("/wallet/balance");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ============================================
    // IsAdmin policy (admin endpoints)
    // ============================================

    [Fact]
    public async Task NonAdmin_CannotVerifyUsers()
    {
        var client = _factory.CreateClient();
        var token = await TestHelpers.RegisterAndLoginAsync(client, "nonadmin@test.com");
        var authed = AuthedClient(token);

        var targetId = await GetUserIdAsync("nonadmin@test.com");

        var response = await authed.PostAsync($"/admin/users/{targetId}/verify", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NonAdmin_CannotFreezeUsers()
    {
        var client = _factory.CreateClient();
        var token = await TestHelpers.RegisterAndLoginAsync(client, "nonadmin-freeze@test.com");
        var authed = AuthedClient(token);

        var targetId = await GetUserIdAsync("nonadmin-freeze@test.com");

        var response = await authed.PostAsync($"/admin/users/{targetId}/freeze", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_CanVerifyUser()
    {
        var client = _factory.CreateClient();

        // Register the target and the admin.
        await TestHelpers.RegisterAndLoginAsync(client, "verify-target@test.com");
        var adminToken = await TestHelpers.RegisterAdminAndLoginAsync(
            client, _factory.Services, "admin-verify@test.com");

        var targetId = await GetUserIdAsync("verify-target@test.com");
        var adminClient = AuthedClient(adminToken);

        var response = await adminClient.PostAsync($"/admin/users/{targetId}/verify", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Confirm the flag actually flipped in the DB.
        using var scope = _factory.Services.CreateScope();
        var userStore = scope.ServiceProvider.GetRequiredService<IUserStore>();
        var updated = await userStore.GetByIdAsync(targetId);
        updated!.IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task Admin_CanFreezeUser()
    {
        var client = _factory.CreateClient();

        await TestHelpers.RegisterAndLoginAsync(client, "freeze-target@test.com");
        var adminToken = await TestHelpers.RegisterAdminAndLoginAsync(
            client, _factory.Services, "admin-freeze@test.com");

        var targetId = await GetUserIdAsync("freeze-target@test.com");
        var adminClient = AuthedClient(adminToken);

        var response = await adminClient.PostAsync($"/admin/users/{targetId}/freeze", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var userStore = scope.ServiceProvider.GetRequiredService<IUserStore>();
        var updated = await userStore.GetByIdAsync(targetId);
        updated!.IsFrozen.Should().BeTrue();
    }

    [Fact]
    public async Task Admin_VerifyUnknownUser_Returns404()
    {
        var client = _factory.CreateClient();
        var adminToken = await TestHelpers.RegisterAdminAndLoginAsync(
            client, _factory.Services, "admin-404@test.com");
        var adminClient = AuthedClient(adminToken);

        var response = await adminClient.PostAsync($"/admin/users/{Guid.NewGuid()}/verify", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ============================================
    // Full end-to-end admin workflow
    // ============================================

    [Fact]
    public async Task FullWorkflow_AdminVerifiesUser_UserCanThenWithdraw()
    {
        var client = _factory.CreateClient();

        // 1. User registers and deposits. Withdraw is blocked.
        var userToken = await TestHelpers.RegisterAndLoginAsync(client, "workflow@test.com");
        var userClient = AuthedClient(userToken);

        await userClient.PostAsync("/wallet/deposit?amount=100", null);
        var blockedWithdraw = await userClient.PostAsync("/wallet/withdraw?amount=30", null);
        blockedWithdraw.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 2. Admin verifies the user.
        var adminToken = await TestHelpers.RegisterAdminAndLoginAsync(
            client, _factory.Services, "workflow-admin@test.com");
        var adminClient = AuthedClient(adminToken);

        var userId = await GetUserIdAsync("workflow@test.com");
        var verifyResponse = await adminClient.PostAsync($"/admin/users/{userId}/verify", null);
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Same user token — next withdraw succeeds, no re-login needed.
        var allowedWithdraw = await userClient.PostAsync("/wallet/withdraw?amount=30", null);
        allowedWithdraw.StatusCode.Should().Be(HttpStatusCode.OK);

        var balance = await (await userClient.GetAsync("/wallet/balance"))
            .Content.ReadFromJsonAsync<BalanceResponse>();
        balance!.Balance.Should().Be(70);
    }

    private record BalanceResponse(decimal Balance);
}