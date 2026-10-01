namespace WalletApp.Core.EventStore;

public class ConcurrencyException : Exception
{
    public Guid AggregateId { get; }
    public int ExpectedVersion { get; }
    public int ActualVersion { get; }

    public ConcurrencyException(Guid aggregateId, int expectedVersion, int actualVersion)
        : base($"Concurrency conflict for aggregate {aggregateId}. Expected version {expectedVersion}, actual version {actualVersion}.")
    {
        AggregateId = aggregateId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }
}