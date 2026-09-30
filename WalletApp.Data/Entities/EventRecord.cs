namespace WalletApp.Data.Entities;

public class EventRecord
{
    public long Id { get; set; }
    public Guid AggregateId { get; set; }
    public string EventType { get; set; } = "";
    public string Data { get; set; } = "";
    public DateTime OccurredAt { get; set; }
}
