namespace WalletApp.Core.ReadModels;

public interface IWalletReadStore
{
    Task<WalletReadModel?> GetAsync(Guid walletId);
    Task SaveAsync(WalletReadModel wallet);
}