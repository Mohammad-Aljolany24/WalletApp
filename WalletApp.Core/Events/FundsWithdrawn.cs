namespace WalletApp.Core.Events;

public class FundsWithdrawn : IEvent
{
    public Guid AggregateId { get; set; }
    public decimal Amount { get; set; }
    public DateTime OccurredAt { get; set; }
}