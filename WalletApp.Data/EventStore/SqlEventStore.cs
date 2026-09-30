using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WalletApp.Core.EventStore;
using WalletApp.Core.Events;
using WalletApp.Data.Entities;

namespace WalletApp.Data.EventStore;

public class SqlEventStore : IEventStore
{
    private readonly AppDbContext _db;

    public SqlEventStore(AppDbContext db)
    {
        _db = db;
    }

    public async Task AppendAsync(Guid aggregateId, IEvent evt)
    {
        var record = new EventRecord
        {
            AggregateId = aggregateId,
            EventType = evt.GetType().Name,
            Data = JsonSerializer.Serialize(evt, evt.GetType()),
            OccurredAt = evt.OccurredAt
        };

        _db.Events.Add(record);
        await _db.SaveChangesAsync();
    }

    public async Task<List<IEvent>> GetEventsAsync(Guid aggregateId)
    {
        var records = await _db.Events
            .Where(e => e.AggregateId == aggregateId)
            .OrderBy(e => e.Id)
            .ToListAsync();

        var events = new List<IEvent>();

        foreach (var record in records)
        {
            var type = Type.GetType($"WalletApp.Core.Events.{record.EventType}, WalletApp.Core");
            if (type == null) continue;

            var evt = (IEvent?)JsonSerializer.Deserialize(record.Data, type);
            if (evt != null) events.Add(evt);
        }

        return events;
    }
}