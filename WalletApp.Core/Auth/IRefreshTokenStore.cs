namespace WalletApp.Core.Auth;

public interface IRefreshTokenStore
{
    Task SaveAsync(RefreshTokenData token);
    Task<RefreshTokenData?> GetByHashAsync(string tokenHash);
    Task RevokeAsync(Guid id, Guid? replacedBy = null);
    Task RevokeFamilyAsync(Guid familyId);
}