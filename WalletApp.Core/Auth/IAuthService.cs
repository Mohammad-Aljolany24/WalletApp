namespace WalletApp.Core.Auth;

public record AuthTokens(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt);

public interface IAuthService
{
    Task<User> RegisterAsync(string email, string password);
    Task<AuthTokens> LoginAsync(string email, string password);
    Task<AuthTokens> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
}