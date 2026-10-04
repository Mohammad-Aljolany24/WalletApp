using WalletApp.Core.Events;
using WalletApp.Core.Exceptions;

namespace WalletApp.Core.Aggregates;

public class Wallet
{
    public Guid Id { get; private set; }
    public decimal Balance { get; private set; }
    public int Version { get; private set; }

    private readonly List<IEvent> _uncommittedEvents = new();
    public IReadOnlyList<IEvent> UncommittedEvents => _uncommittedEvents;

    public void Deposit(decimal amount)
    {
        if (amount <= 0)
            throw new ValidationException("Deposit amount must be positive.");

        var evt = new FundsDeposited
        {
            AggregateId = Id,
            Amount = amount,
            OccurredAt = DateTime.UtcNow
        };

        Apply(evt);
        _uncommittedEvents.Add(evt);
    }

    public void Withdraw(decimal amount)
    {
        if (amount <= 0)
            throw new ValidationException("Withdrawal amount must be positive.");

        if (amount > Balance)
            throw new InsufficientFundsException(amount, Balance);

        var evt = new FundsWithdrawn
        {
            AggregateId = Id,
            Amount = amount,
            OccurredAt = DateTime.UtcNow
        };

        Apply(evt);
        _uncommittedEvents.Add(evt);
    }

    private void Apply(IEvent evt)
    {
        switch (evt)
        {
            case FundsDeposited d:
                Balance += d.Amount;
                break;
            case FundsWithdrawn w:
                Balance -= w.Amount;
                break;
        }

        Version++;
    }

    public static Wallet Rehydrate(Guid id, IEnumerable<IEvent> events)
    {
        var wallet = new Wallet { Id = id };

        foreach (var evt in events)
            wallet.Apply(evt);

        return wallet;
    }

    public void ClearUncommittedEvents()
    {
        _uncommittedEvents.Clear();
    }
}