using WalletApp.Core.Auth;

namespace WalletApp.Data.Auth;

public class InMemoryUserStore : IUserStore
{
    private readonly Dictionary<Guid, User> _users = new();

    public Task<User?> GetByEmailAsync(string email)
    {
        var user = _users.Values.FirstOrDefault(u => u.Email == email.ToLowerInvariant());
        return Task.FromResult(user);
    }

    public Task<User?> GetByIdAsync(Guid id)
    {
        _users.TryGetValue(id, out var user);
        return Task.FromResult(user);
    }

    public Task SaveAsync(User user)
    {
        _users[user.Id] = user;
        return Task.CompletedTask;
    }
}