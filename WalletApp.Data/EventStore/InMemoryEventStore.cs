using WalletApp.Core.EventStore;
using WalletApp.Core.Events;

namespace WalletApp.Data.EventStore;

public class InMemoryEventStore : IEventStore
{
    private readonly Dictionary<Guid, List<IEvent>> _store = new();

    public Task AppendAsync(Guid aggregateId, IEvent evt)
    {
        if (!_store.ContainsKey(aggregateId))
        {
            _store[aggregateId] = new List<IEvent>();
        }

        _store[aggregateId].Add(evt);
        return Task.CompletedTask;
    }

    public Task<List<IEvent>> GetEventsAsync(Guid aggregateId)
    {
        if (_store.ContainsKey(aggregateId))
        {
            return Task.FromResult(_store[aggregateId]);
        }

        return Task.FromResult(new List<IEvent>());
    }
}