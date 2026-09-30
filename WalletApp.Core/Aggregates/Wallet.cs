using WalletApp.Core.Events;

namespace WalletApp.Core.Aggregates;

public class Wallet
{
    public Guid Id { get; private set; }
    public decimal Balance { get; private set; }

    private readonly List<IEvent> _uncommittedEvents = new();
    public IReadOnlyList<IEvent> UncommittedEvents => _uncommittedEvents;

    // Business logic: deposit
    public void Deposit(decimal amount)
    {
        if (amount <= 0)
            throw new Exception("Deposit amount must be positive");

        var evt = new FundsDeposited
        {
            AggregateId = Id,
            Amount = amount,
            OccurredAt = DateTime.UtcNow
        };

        Apply(evt);
        _uncommittedEvents.Add(evt);
    }

    // Business logic: withdraw
    public void Withdraw(decimal amount)
    {
        if (amount <= 0)
            throw new Exception("Withdrawal amount must be positive");

        if (amount > Balance)
            throw new Exception("Insufficient funds");

        var evt = new FundsWithdrawn
        {
            AggregateId = Id,
            Amount = amount,
            OccurredAt = DateTime.UtcNow
        };

        Apply(evt);
        _uncommittedEvents.Add(evt);
    }

    // The ONLY place state changes
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
    }

    // Rebuild state from past events
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