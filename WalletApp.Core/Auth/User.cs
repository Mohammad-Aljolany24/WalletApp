namespace WalletApp.Core.Auth;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

      public string Role { get; set; } = "User";
    public bool IsVerified { get; set; } = false;
    public bool IsFrozen { get; set; } = false;


}