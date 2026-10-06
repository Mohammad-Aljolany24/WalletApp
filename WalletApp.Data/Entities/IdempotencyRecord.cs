namespace WalletApp.Data.Entities;

public class IdempotencyRecord
{
    public Guid UserId { get; set; }
    public string Key { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public int StatusCode { get; set; }
    public string ResponseBody { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}