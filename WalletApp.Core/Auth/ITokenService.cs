namespace WalletApp.Core.Auth;

public interface ITokenService
{
    string GenerateToken(User user);
}