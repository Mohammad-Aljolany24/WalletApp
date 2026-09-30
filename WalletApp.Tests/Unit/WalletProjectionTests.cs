using FluentAssertions;
using WalletApp.Core.Events;
using WalletApp.Core.Projections;
using WalletApp.Core.ReadModels;

namespace WalletApp.Tests.Unit;

public class WalletProjectionTests
{
    // ============================================
    // A fake read store for testing (in-memory)
    // ============================================
    private class FakeReadStore : IWalletReadStore
    {
        private readonly Dictionary<Guid, WalletReadModel> _store = new();

        public Task<WalletReadModel?> GetAsync(Guid walletId)
        {
            _store.TryGetValue(walletId, out var wallet);
            return Task.FromResult(wallet);
        }

        public Task SaveAsync(WalletReadModel wallet)
        {
            // Clone so test observations aren't affected by later mutations
            _store[wallet.WalletId] = new WalletReadModel
            {
                WalletId = wallet.WalletId,
                Balance = wallet.Balance
            };
            return Task.CompletedTask;
        }
    }

    // ============================================
    // DEPOSIT
    // ============================================

    [Fact]
    public async Task Deposit_CreatesNewReadModelRow()
    {
        var store = new FakeReadStore();
        var projection = new WalletProjection(store);
        var walletId = Guid.NewGuid();

        await projection.HandleAsync(new FundsDeposited
        {
            AggregateId = walletId,
            Amount = 100,
            OccurredAt = DateTime.UtcNow
        });

        var wallet = await store.GetAsync(walletId);
        wallet.Should().NotBeNull();
        wallet!.Balance.Should().Be(100);
    }

    [Fact]
    public async Task Deposit_OnExistingWallet_AddsToBalance()
    {
        var store = new FakeReadStore();
        var projection = new WalletProjection(store);
        var walletId = Guid.NewGuid();

        await projection.HandleAsync(new FundsDeposited
        {
            AggregateId = walletId,
            Amount = 100,
            OccurredAt = DateTime.UtcNow
        });

        await projection.HandleAsync(new FundsDeposited
        {
            AggregateId = walletId,
            Amount = 50,
            OccurredAt = DateTime.UtcNow
        });

        var wallet = await store.GetAsync(walletId);
        wallet!.Balance.Should().Be(150);
    }

    // ============================================
    // WITHDRAW
    // ============================================

    [Fact]
    public async Task Withdraw_SubtractsFromBalance()
    {
        var store = new FakeReadStore();
        var projection = new WalletProjection(store);
        var walletId = Guid.NewGuid();

        await projection.HandleAsync(new FundsDeposited
        {
            AggregateId = walletId,
            Amount = 100,
            OccurredAt = DateTime.UtcNow
        });

        await projection.HandleAsync(new FundsWithdrawn
        {
            AggregateId = walletId,
            Amount = 30,
            OccurredAt = DateTime.UtcNow
        });

        var wallet = await store.GetAsync(walletId);
        wallet!.Balance.Should().Be(70);
    }

    [Fact]
    public async Task Withdraw_OnNonExistentWallet_DoesNothing()
    {
        var store = new FakeReadStore();
        var projection = new WalletProjection(store);

        await projection.HandleAsync(new FundsWithdrawn
        {
            AggregateId = Guid.NewGuid(),
            Amount = 50,
            OccurredAt = DateTime.UtcNow
        });

        // No wallet should have been created
        var wallet = await store.GetAsync(Guid.NewGuid());
        wallet.Should().BeNull();
    }

    // ============================================
    // SEQUENCE
    // ============================================

    [Fact]
    public async Task MultipleEvents_ProduceCorrectFinalBalance()
    {
        var store = new FakeReadStore();
        var projection = new WalletProjection(store);
        var walletId = Guid.NewGuid();

        await projection.HandleAsync(new FundsDeposited
        {
            AggregateId = walletId, Amount = 100, OccurredAt = DateTime.UtcNow
        });
        await projection.HandleAsync(new FundsDeposited
        {
            AggregateId = walletId, Amount = 50, OccurredAt = DateTime.UtcNow
        });
        await projection.HandleAsync(new FundsWithdrawn
        {
            AggregateId = walletId, Amount = 30, OccurredAt = DateTime.UtcNow
        });

        var wallet = await store.GetAsync(walletId);
        wallet!.Balance.Should().Be(120);
    }

    // ============================================
    // UNKNOWN EVENT
    // ============================================

    [Fact]
    public async Task UnknownEventType_IsIgnored()
    {
        var store = new FakeReadStore();
        var projection = new WalletProjection(store);
        var walletId = Guid.NewGuid();

        // Some event the projection doesn't know about
        await projection.HandleAsync(new UnknownTestEvent
        {
            AggregateId = walletId,
            OccurredAt = DateTime.UtcNow
        });

        var wallet = await store.GetAsync(walletId);
        wallet.Should().BeNull();
    }

    // Test event used only in this test
    private class UnknownTestEvent : IEvent
    {
        public Guid AggregateId { get; set; }
        public DateTime OccurredAt { get; set; }
    }
}