using WalletApp.Core.Events;
using WalletApp.Core.Pagination;

namespace WalletApp.Core.EventStore;

public interface IEventStore
{
    Task AppendAsync(
        Guid aggregateId,
        IReadOnlyCollection<IEvent> events,
        int expectedVersion);

    Task<List<IEvent>> GetEventsAsync(Guid aggregateId);

      Task<PagedResult<StoredEvent>> GetEventsPagedAsync(
        Guid aggregateId,
        long? afterId,
        int limit);
}