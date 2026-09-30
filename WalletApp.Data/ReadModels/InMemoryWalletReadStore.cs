using WalletApp.Core.ReadModels;

namespace WalletApp.Data.ReadModels;

public class InMemoryWalletReadStore : IWalletReadStore
{
    private readonly Dictionary<Guid, WalletReadModel> _store = new();

    public Task<WalletReadModel?> GetAsync(Guid walletId)
    {
        _store.TryGetValue(walletId, out var wallet);
        return Task.FromResult(wallet);
    }

    public Task SaveAsync(WalletReadModel wallet)
    {
        _store[wallet.WalletId] = wallet;
        return Task.CompletedTask;
    }
}