using WalletApp.Core.Events;

namespace WalletApp.Core.EventStore;

public interface IEventStore
{
    Task AppendAsync(
        Guid aggregateId,
        IReadOnlyCollection<IEvent> events,
        int expectedVersion);

    Task<List<IEvent>> GetEventsAsync(Guid aggregateId);
}