namespace WalletApp.Core.Events;

public interface IEvent
{
    Guid AggregateId { get; }
    DateTime OccurredAt { get; }
}