using WalletApp.Core.Events;
using WalletApp.Core.ReadModels;

namespace WalletApp.Core.Projections;

public class WalletProjection
{
    private readonly IWalletReadStore _readStore;

    public WalletProjection(IWalletReadStore readStore)
    {
        _readStore = readStore;
    }

    public async Task HandleAsync(IEvent evt)
    {
        switch (evt)
        {
            case FundsDeposited d:
                await ApplyDeposit(d);
                break;
            case FundsWithdrawn w:
                await ApplyWithdraw(w);
                break;
        }
    }

    private async Task ApplyDeposit(FundsDeposited evt)
    {
        var wallet = await _readStore.GetAsync(evt.AggregateId);

        if (wallet == null)
        {
            wallet = new WalletReadModel
            {
                WalletId = evt.AggregateId,
                Balance = evt.Amount
            };
        }
        else
        {
            wallet.Balance += evt.Amount;
        }

        await _readStore.SaveAsync(wallet);
    }

    private async Task ApplyWithdraw(FundsWithdrawn evt)
    {
        var wallet = await _readStore.GetAsync(evt.AggregateId);

        if (wallet != null)
        {
            wallet.Balance -= evt.Amount;
            await _readStore.SaveAsync(wallet);
        }
    }
}