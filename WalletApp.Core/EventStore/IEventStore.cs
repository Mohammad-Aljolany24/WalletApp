using WalletApp.Core.Events;

namespace WalletApp.Core.EventStore;

public interface IEventStore
{
    Task AppendAsync(Guid aggregateId, IEvent evt);
    Task<List<IEvent>> GetEventsAsync(Guid aggregateId);
}