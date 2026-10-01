using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WalletApp.Core.EventStore;
using WalletApp.Core.Events;
using WalletApp.Data.Entities;
using Microsoft.Data.SqlClient;

namespace WalletApp.Data.EventStore;

public class SqlEventStore : IEventStore
{
    private readonly AppDbContext _db;

    public SqlEventStore(AppDbContext db)
    {
        _db = db;
    }

    public async Task AppendAsync(
    Guid aggregateId,
    IReadOnlyCollection<IEvent> events,
    int expectedVersion)
{
    if (events.Count == 0)
        return;

    var currentVersion = await _db.Events
        .Where(e => e.AggregateId == aggregateId)
        .MaxAsync(e => (int?)e.Version) ?? 0;

    if (currentVersion != expectedVersion)
        throw new ConcurrencyException(aggregateId, expectedVersion, currentVersion);

    var nextVersion = expectedVersion;
    var records = new List<EventRecord>(events.Count);

    foreach (var evt in events)
    {
        nextVersion++;
        records.Add(new EventRecord
        {
            AggregateId = aggregateId,
            EventType = evt.GetType().Name,
            Data = JsonSerializer.Serialize(evt, evt.GetType()),
            Version = nextVersion,
            OccurredAt = evt.OccurredAt
        });
    }

    _db.Events.AddRange(records);

    try
    {
        await _db.SaveChangesAsync();
    }
    catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
    {
        var actualVersion = await _db.Events
            .Where(e => e.AggregateId == aggregateId)
            .MaxAsync(e => (int?)e.Version) ?? 0;

        throw new ConcurrencyException(aggregateId, expectedVersion, actualVersion);
    }
}

private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    => ex.InnerException is SqlException sql
       && (sql.Number == 2601 || sql.Number == 2627);
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