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
            CreatedAt = user.CreatedAt,
            Role = user.Role,
            IsVerified = user.IsVerified,
            IsFrozen = user.IsFrozen
        });
    }
    else
    {
        record.Email = user.Email;
        record.PasswordHash = user.PasswordHash;
        record.Role = user.Role;
        record.IsVerified = user.IsVerified;
        record.IsFrozen = user.IsFrozen;
    }

    await _db.SaveChangesAsync();
}

    private static User ToDomain(UserRecord r) => new User
    {
        Id = r.Id,
        Email = r.Email,
        PasswordHash = r.PasswordHash,
        CreatedAt = r.CreatedAt,
          Role = r.Role,
    IsVerified = r.IsVerified,
    IsFrozen = r.IsFrozen
    };

    public async Task<List<User>> GetAllAsync()
{
    var records = await _db.Users
        .OrderBy(u => u.CreatedAt)
        .ToListAsync();

    return records.Select(ToDomain).ToList();
}
}