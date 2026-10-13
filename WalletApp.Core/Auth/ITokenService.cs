namespace WalletApp.Core.Auth;

public record AccessToken(string Value, DateTime ExpiresAt);

public interface ITokenService
{
    AccessToken GenerateAccessToken(User user);
    (string Token, string Hash) GenerateRefreshToken();
    string HashRefreshToken(string token);
}