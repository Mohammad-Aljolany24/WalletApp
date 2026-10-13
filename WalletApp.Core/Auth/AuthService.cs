using WalletApp.Core.Exceptions;

namespace WalletApp.Core.Auth;

public class AuthService : IAuthService
{
    private readonly IUserStore _userStore;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly int _refreshTokenDays;

    public AuthService(
        IUserStore userStore,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IRefreshTokenStore refreshTokens,
        int refreshTokenDays)
    {
        _userStore = userStore;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _refreshTokens = refreshTokens;
        _refreshTokenDays = refreshTokenDays;
    }

    public async Task<User> RegisterAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ValidationException("Email is required.");

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            throw new ValidationException("Password must be at least 6 characters.");

        var existing = await _userStore.GetByEmailAsync(email);
        if (existing != null)
            throw new ValidationException("Email already registered.");

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

    public async Task<AuthTokens> LoginAsync(string email, string password)
    {
        var user = await _userStore.GetByEmailAsync(email);
        if (user == null)
            throw new UnauthorizedException("Invalid email or password.");

        if (!_passwordHasher.Verify(password, user.PasswordHash))
            throw new UnauthorizedException("Invalid email or password.");

        var (tokens, _) = await IssueTokensAsync(user, familyId: Guid.NewGuid());
        return tokens;
    }

    public async Task<AuthTokens> RefreshAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new UnauthorizedException("Missing refresh token.");

        var hash = _tokenService.HashRefreshToken(refreshToken);
        var existing = await _refreshTokens.GetByHashAsync(hash);

        if (existing is null)
            throw new UnauthorizedException("Invalid refresh token.");

        // Reuse of a revoked token is a theft signal: revoke the whole family.
        if (existing.RevokedAt is not null)
        {
            await _refreshTokens.RevokeFamilyAsync(existing.FamilyId);
            throw new UnauthorizedException("Refresh token has been revoked.");
        }

        if (existing.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedException("Refresh token has expired.");

        var user = await _userStore.GetByIdAsync(existing.UserId);
        if (user is null)
            throw new UnauthorizedException("User no longer exists.");

        // Issue the successor and revoke the predecessor in one step.
        // IssueTokensAsync returns the new record's Id, so no DB round-trip
        // is needed to link old → new.
        var (next, nextId) = await IssueTokensAsync(user, existing.FamilyId);
        await _refreshTokens.RevokeAsync(existing.Id, nextId);

        return next;
    }

    public async Task LogoutAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;

        var hash = _tokenService.HashRefreshToken(refreshToken);
        var existing = await _refreshTokens.GetByHashAsync(hash);
        if (existing is not null && existing.RevokedAt is null)
            await _refreshTokens.RevokeAsync(existing.Id);
    }

    /// <summary>
    /// Issues a new access + refresh pair and persists the refresh record.
    /// Returns the tokens plus the persisted record's Id so callers doing
    /// rotation can link predecessor → successor without a second lookup.
    /// </summary>
    private async Task<(AuthTokens Tokens, Guid RefreshTokenId)> IssueTokensAsync(
        User user,
        Guid familyId)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);

        var (refreshToken, refreshHash) = _tokenService.GenerateRefreshToken();
        var refreshExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenDays);
        var refreshId = Guid.NewGuid();

        var record = new RefreshTokenData(
            Id: refreshId,
            UserId: user.Id,
            TokenHash: refreshHash,
            FamilyId: familyId,
            ExpiresAt: refreshExpiresAt,
            CreatedAt: DateTime.UtcNow,
            RevokedAt: null,
            ReplacedByTokenId: null);

        await _refreshTokens.SaveAsync(record);

        return (new AuthTokens(accessToken.Value, refreshToken, accessToken.ExpiresAt), refreshId);
    }
}