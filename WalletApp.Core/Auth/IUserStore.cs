using WalletApp.Core.Pagination;

namespace WalletApp.Core.Auth;

public interface IUserStore
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(Guid id);
    Task SaveAsync(User user);
    Task<List<User>> GetAllAsync(); 
       Task<PagedResult<User>> GetPagedAsync(
        DateTime? afterCreatedAt,
        Guid? afterId,
        int limit);
}