using Microsoft.EntityFrameworkCore;
using WalletApp.Core.ReadModels;
using WalletApp.Data.Entities;

namespace WalletApp.Data.ReadModels;

public class SqlWalletReadStore : IWalletReadStore
{
    private readonly AppDbContext _db;

    public SqlWalletReadStore(AppDbContext db)
    {
        _db = db;
    }

    public async Task<WalletReadModel?> GetAsync(Guid walletId)
    {
        var record = await _db.WalletReadModel
            .FirstOrDefaultAsync(w => w.WalletId == walletId);

        if (record == null) return null;

        return new WalletReadModel
        {
            WalletId = record.WalletId,
            Balance = record.Balance
        };
    }

    public async Task SaveAsync(WalletReadModel wallet)
    {
        var record = await _db.WalletReadModel
            .FirstOrDefaultAsync(w => w.WalletId == wallet.WalletId);

        if (record == null)
        {
            _db.WalletReadModel.Add(new WalletReadRecord
            {
                WalletId = wallet.WalletId,
                Balance = wallet.Balance
            });
        }
        else
        {
            record.Balance = wallet.Balance;
        }

        await _db.SaveChangesAsync();
    }
}