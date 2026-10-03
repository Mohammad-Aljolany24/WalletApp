namespace WalletApp.Core.Auth;

public interface IUserStore
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(Guid id);
    Task SaveAsync(User user);
    Task<List<User>> GetAllAsync(); 
}