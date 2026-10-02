namespace WalletApp.Data.Entities;

public class UserRecord
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }

     public string Role { get; set; } = "User";
    public bool IsVerified { get; set; } = false;
    public bool IsFrozen { get; set; } = false;
}