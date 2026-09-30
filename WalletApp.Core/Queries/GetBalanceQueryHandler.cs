using WalletApp.Core.ReadModels;

namespace WalletApp.Core.Queries;

public class GetBalanceQueryHandler
{
    private readonly IWalletReadStore _readStore;

    public GetBalanceQueryHandler(IWalletReadStore readStore)
    {
        _readStore = readStore;
    }

    public async Task<decimal> HandleAsync(GetBalanceQuery query)
    {
        var wallet = await _readStore.GetAsync(query.WalletId);
        return wallet?.Balance ?? 0;
    }
}