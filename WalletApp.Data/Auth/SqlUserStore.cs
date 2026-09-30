using Microsoft.EntityFrameworkCore;
using WalletApp.Core.Auth;
using WalletApp.Data.Entities;

namespace WalletApp.Data.Auth;

public class SqlUserStore : IUserStore
{
    private readonly AppDbContext _db;

    public SqlUserStore(AppDbContext db)
    {
        _db = db;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var record = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());

        return record == null ? null : ToDomain(record);
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        var record = await _db.Users.FindAsync(id);
        return record == null ? null : ToDomain(record);
    }

    public async Task SaveAsync(User user)
    {
        var record = await _db.Users.FindAsync(user.Id);

        if (record == null)
        {
            _db.Users.Add(new UserRecord
            {
                Id = user.Id,
                Email = user.Email,
                PasswordHash = user.PasswordHash,
                CreatedAt = user.CreatedAt
            });
        }
        else
        {
            record.Email = user.Email;
            record.PasswordHash = user.PasswordHash;
        }

        await _db.SaveChangesAsync();
    }

    private static User ToDomain(UserRecord r) => new User
    {
        Id = r.Id,
        Email = r.Email,
        PasswordHash = r.PasswordHash,
        CreatedAt = r.CreatedAt
    };
}