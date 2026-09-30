namespace WalletApp.Core.Auth;

public class AuthService : IAuthService
{
    private readonly IUserStore _userStore;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthService(
        IUserStore userStore,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _userStore = userStore;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<User> RegisterAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new Exception("Email is required");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            throw new Exception("Password must be at least 6 characters");

        var existing = await _userStore.GetByEmailAsync(email);
        if (existing != null)
            throw new Exception("Email already registered");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            PasswordHash = _passwordHasher.Hash(password),
            CreatedAt = DateTime.UtcNow
        };

        await _userStore.SaveAsync(user);
        return user;
    }

    public async Task<string> LoginAsync(string email, string password)
    {
        var user = await _userStore.GetByEmailAsync(email);
        if (user == null)
            throw new Exception("Invalid credentials");

        if (!_passwordHasher.Verify(password, user.PasswordHash))
            throw new Exception("Invalid credentials");

        return _tokenService.GenerateToken(user);
    }
}