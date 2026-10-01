using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WalletApp.Core.Aggregates;
using WalletApp.Core.EventStore;
using WalletApp.Data;
using WalletApp.Data.EventStore;
using Xunit;

namespace WalletApp.Tests.Integration;

public class ConcurrencyTests
{
    [Fact]
    public async Task ConcurrentAppends_OneSucceeds_OneThrowsConcurrencyException()
    {
        var dbName = $"WalletAppTestDb_{Guid.NewGuid():N}";
        var connectionString =
            $@"Server=.\SQLEXPRESS;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.MigrateAsync();
        }

        try
        {
            var walletId = Guid.NewGuid();

            await using var db1 = new AppDbContext(options);
            await using var db2 = new AppDbContext(options);

            var store1 = new SqlEventStore(db1);
            var store2 = new SqlEventStore(db2);

            // Both load the same empty wallet → both see Version 0.
            var wallet1 = Wallet.Rehydrate(walletId, await store1.GetEventsAsync(walletId));
            var expectedVersion1 = wallet1.Version;

            var wallet2 = Wallet.Rehydrate(walletId, await store2.GetEventsAsync(walletId));
            var expectedVersion2 = wallet2.Version;

            wallet1.Deposit(100m);
            wallet2.Deposit(50m);

            // Fire both appends concurrently.
            var task1 = store1.AppendAsync(walletId, wallet1.UncommittedEvents, expectedVersion1);
            var task2 = store2.AppendAsync(walletId, wallet2.UncommittedEvents, expectedVersion2);

            var exceptions = new List<Exception>();
            try { await task1; } catch (Exception ex) { exceptions.Add(ex); }
            try { await task2; } catch (Exception ex) { exceptions.Add(ex); }

            // Exactly one append lost the race.
            exceptions.Should().ContainSingle()
                .Which.Should().BeOfType<ConcurrencyException>();

            // DB has exactly one event, at Version 1.
            await using var verifyDb = new AppDbContext(options);
            var persisted = await verifyDb.Events
                .Where(e => e.AggregateId == walletId)
                .ToListAsync();

            persisted.Should().HaveCount(1);
            persisted.Single().Version.Should().Be(1);
        }
        finally
        {
            await using var cleanup = new AppDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}