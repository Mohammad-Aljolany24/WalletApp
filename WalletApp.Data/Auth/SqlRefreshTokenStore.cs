using Microsoft.EntityFrameworkCore;
using WalletApp.Core.Auth;
using WalletApp.Data.Entities;

namespace WalletApp.Data.Auth;

public class SqlRefreshTokenStore : IRefreshTokenStore
{
    private readonly AppDbContext _db;

    public SqlRefreshTokenStore(AppDbContext db) => _db = db;

    public async Task SaveAsync(RefreshTokenData token)
    {
        _db.RefreshTokens.Add(new RefreshTokenRecord
        {
            Id = token.Id,
            UserId = token.UserId,
            TokenHash = token.TokenHash,
            FamilyId = token.FamilyId,
            ExpiresAt = token.ExpiresAt,
            CreatedAt = token.CreatedAt,
            RevokedAt = token.RevokedAt,
            ReplacedByTokenId = token.ReplacedByTokenId
        });
        await _db.SaveChangesAsync();
    }

    public async Task<RefreshTokenData?> GetByHashAsync(string tokenHash)
    {
        var r = await _db.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        return r is null ? null : ToDomain(r);
    }

    public async Task RevokeAsync(Guid id, Guid? replacedBy = null)
    {
        var r = await _db.RefreshTokens.FindAsync(id);
        if (r is null || r.RevokedAt is not null) return;

        r.RevokedAt = DateTime.UtcNow;
        r.ReplacedByTokenId = replacedBy;
        await _db.SaveChangesAsync();
    }

    public async Task RevokeFamilyAsync(Guid familyId)
    {
        var tokens = await _db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync();

        if (tokens.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var t in tokens) t.RevokedAt = now;
        await _db.SaveChangesAsync();
    }

    private static RefreshTokenData ToDomain(RefreshTokenRecord r) => new(
        r.Id, r.UserId, r.TokenHash, r.FamilyId,
        r.ExpiresAt, r.CreatedAt, r.RevokedAt, r.ReplacedByTokenId);
}