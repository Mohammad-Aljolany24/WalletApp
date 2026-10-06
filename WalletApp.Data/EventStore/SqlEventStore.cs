using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WalletApp.Core.EventStore;
using WalletApp.Core.Events;
using WalletApp.Core.Pagination;
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

    public async Task<List<IEvent>> GetEventsAsync(Guid aggregateId)
    {
        var records = await _db.Events
            .Where(e => e.AggregateId == aggregateId)
            .OrderBy(e => e.Id)
            .ToListAsync();

        return records
            .Select(Deserialize)
            .Where(e => e is not null)
            .Cast<IEvent>()
            .ToList();
    }

    public async Task<PagedResult<StoredEvent>> GetEventsPagedAsync(
        Guid aggregateId,
        long? afterId,
        int limit)
    {
        var query = _db.Events.Where(e => e.AggregateId == aggregateId);

        if (afterId.HasValue)
            query = query.Where(e => e.Id < afterId.Value);

        var records = await query
            .OrderByDescending(e => e.Id)
            .Take(limit + 1)
            .ToListAsync();

        var hasMore = records.Count > limit;
        var page = records.Take(limit).ToList();

        var items = new List<StoredEvent>(page.Count);
        foreach (var r in page)
        {
            var evt = Deserialize(r);
            if (evt is not null)
                items.Add(new StoredEvent(r.Id, evt));
        }

        var nextCursor = hasMore && page.Count > 0
            ? Cursor.EncodeEventCursor(page[^1].Id)
            : null;

        return new PagedResult<StoredEvent>(items, nextCursor);
    }

    private static IEvent? Deserialize(EventRecord record)
    {
        var type = Type.GetType($"WalletApp.Core.Events.{record.EventType}, WalletApp.Core");
        if (type == null) return null;
        return JsonSerializer.Deserialize(record.Data, type) as IEvent;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException is SqlException sql
           && (sql.Number == 2601 || sql.Number == 2627);
}