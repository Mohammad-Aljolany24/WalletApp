using FluentAssertions;
using WalletApp.Core.Aggregates;
using WalletApp.Core.Events;

namespace WalletApp.Tests.Unit;

public class WalletTests
{
    // Helper to create a fresh wallet with a known ID
    private static Wallet NewWallet(Guid id) =>
        Wallet.Rehydrate(id, new List<IEvent>());

    // ============================================
    // DEPOSIT
    // ============================================

    [Fact]
    public void Deposit_WithValidAmount_IncreasesBalance()
    {
        var wallet = NewWallet(Guid.NewGuid());

        wallet.Deposit(100);

        wallet.Balance.Should().Be(100);
    }

    [Fact]
    public void Deposit_MultipleTimes_AccumulatesBalance()
    {
        var wallet = NewWallet(Guid.NewGuid());

        wallet.Deposit(100);
        wallet.Deposit(50);
        wallet.Deposit(25);

        wallet.Balance.Should().Be(175);
    }

    [Fact]
    public void Deposit_WithZero_Throws()
    {
        var wallet = NewWallet(Guid.NewGuid());

        var act = () => wallet.Deposit(0);

        act.Should().Throw<Exception>()
            .WithMessage("*positive*");
    }

    [Fact]
    public void Deposit_WithNegativeAmount_Throws()
    {
        var wallet = NewWallet(Guid.NewGuid());

        var act = () => wallet.Deposit(-50);

        act.Should().Throw<Exception>()
            .WithMessage("*positive*");
    }

    [Fact]
    public void Deposit_ProducesOneUncommittedEvent()
    {
        var wallet = NewWallet(Guid.NewGuid());

        wallet.Deposit(100);

        wallet.UncommittedEvents.Should().HaveCount(1);
        wallet.UncommittedEvents[0].Should().BeOfType<FundsDeposited>();
    }

    [Fact]
    public void Deposit_EventCarriesCorrectData()
    {
        var walletId = Guid.NewGuid();
        var wallet = NewWallet(walletId);

        wallet.Deposit(100);

        var evt = wallet.UncommittedEvents[0] as FundsDeposited;
        evt.Should().NotBeNull();
        evt!.AggregateId.Should().Be(walletId);
        evt.Amount.Should().Be(100);
    }

    // ============================================
    // WITHDRAW
    // ============================================

    [Fact]
    public void Withdraw_WithValidAmount_DecreasesBalance()
    {
        var wallet = NewWallet(Guid.NewGuid());
        wallet.Deposit(100);

        wallet.Withdraw(30);

        wallet.Balance.Should().Be(70);
    }

    [Fact]
    public void Withdraw_WithExactBalance_LeavesZero()
    {
        var wallet = NewWallet(Guid.NewGuid());
        wallet.Deposit(100);

        wallet.Withdraw(100);

        wallet.Balance.Should().Be(0);
    }

    [Fact]
    public void Withdraw_MoreThanBalance_Throws()
    {
        var wallet = NewWallet(Guid.NewGuid());
        wallet.Deposit(50);

        var act = () => wallet.Withdraw(100);

        act.Should().Throw<Exception>()
            .WithMessage("*Insufficient*");
    }

    [Fact]
    public void Withdraw_WithZero_Throws()
    {
        var wallet = NewWallet(Guid.NewGuid());
        wallet.Deposit(100);

        var act = () => wallet.Withdraw(0);

        act.Should().Throw<Exception>()
            .WithMessage("*positive*");
    }

    [Fact]
    public void Withdraw_WithNegativeAmount_Throws()
    {
        var wallet = NewWallet(Guid.NewGuid());
        wallet.Deposit(100);

        var act = () => wallet.Withdraw(-10);

        act.Should().Throw<Exception>()
            .WithMessage("*positive*");
    }

    [Fact]
    public void Withdraw_WhenItFails_ProducesNoEvent()
    {
        var wallet = NewWallet(Guid.NewGuid());
        wallet.Deposit(50);
        wallet.ClearUncommittedEvents();

        var act = () => wallet.Withdraw(100);
        act.Should().Throw<Exception>();

        wallet.UncommittedEvents.Should().BeEmpty();
    }

    // ============================================
    // REHYDRATE
    // ============================================

    [Fact]
    public void Rehydrate_WithNoEvents_ReturnsEmptyWallet()
    {
        var walletId = Guid.NewGuid();

        var wallet = Wallet.Rehydrate(walletId, new List<IEvent>());

        wallet.Id.Should().Be(walletId);
        wallet.Balance.Should().Be(0);
    }

    [Fact]
    public void Rehydrate_FromDepositEvents_RebuildsBalance()
    {
        var walletId = Guid.NewGuid();
        var events = new List<IEvent>
        {
            new FundsDeposited { AggregateId = walletId, Amount = 100, OccurredAt = DateTime.UtcNow },
            new FundsDeposited { AggregateId = walletId, Amount = 50, OccurredAt = DateTime.UtcNow }
        };

        var wallet = Wallet.Rehydrate(walletId, events);

        wallet.Balance.Should().Be(150);
    }

    [Fact]
    public void Rehydrate_FromMixedEvents_ProducesCorrectFinalState()
    {
        var walletId = Guid.NewGuid();
        var events = new List<IEvent>
        {
            new FundsDeposited { AggregateId = walletId, Amount = 100, OccurredAt = DateTime.UtcNow },
            new FundsDeposited { AggregateId = walletId, Amount = 50, OccurredAt = DateTime.UtcNow },
            new FundsWithdrawn { AggregateId = walletId, Amount = 30, OccurredAt = DateTime.UtcNow }
        };

        var wallet = Wallet.Rehydrate(walletId, events);

        wallet.Balance.Should().Be(120);
    }
}