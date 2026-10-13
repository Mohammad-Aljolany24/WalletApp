using FluentAssertions;
using Moq;
using WalletApp.Core.Auth;
using WalletApp.Core.Exceptions;

namespace WalletApp.Tests.Unit;

public class AuthServiceTests
{
    // Mocks
    private readonly Mock<IUserStore> _userStore = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IRefreshTokenStore> _refreshTokens = new();

    private const int RefreshTokenDays = 7;

    private AuthService CreateService()
    {
        // Sensible defaults so tests that don't care about token generation still work.
        _tokenService
            .Setup(t => t.GenerateAccessToken(It.IsAny<User>()))
            .Returns(new AccessToken("access_token", DateTime.UtcNow.AddMinutes(15)));

        _tokenService
            .Setup(t => t.GenerateRefreshToken())
            .Returns(("refresh_token", "refresh_hash"));

        _tokenService
            .Setup(t => t.HashRefreshToken(It.IsAny<string>()))
            .Returns<string>(s => $"hash:{s}");

        return new AuthService(
            _userStore.Object,
            _passwordHasher.Object,
            _tokenService.Object,
            _refreshTokens.Object,
            RefreshTokenDays);
    }

    // ============================================
    // REGISTER — happy path
    // ============================================

    [Fact]
    public async Task Register_WithValidData_CreatesUser()
    {
        _userStore.Setup(s => s.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);
        _passwordHasher.Setup(h => h.Hash(It.IsAny<string>()))
            .Returns("hashed_password");

        var service = CreateService();

        var user = await service.RegisterAsync("test@test.com", "password123");

        user.Should().NotBeNull();
        user.Email.Should().Be("test@test.com");
        user.PasswordHash.Should().Be("hashed_password");
        user.Id.Should().NotBe(Guid.Empty);

        _userStore.Verify(s => s.SaveAsync(It.IsAny<User>()), Times.Once);
    }

    [Fact]
    public async Task Register_NormalizesEmailToLowercase()
    {
        _userStore.Setup(s => s.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);
        _passwordHasher.Setup(h => h.Hash(It.IsAny<string>()))
            .Returns("hashed_password");

        var service = CreateService();

        var user = await service.RegisterAsync("TEST@TEST.COM", "password123");

        user.Email.Should().Be("test@test.com");
    }

    [Fact]
    public async Task Register_HashesPassword()
    {
        _userStore.Setup(s => s.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);
        _passwordHasher.Setup(h => h.Hash("password123"))
            .Returns("hashed_password");

        var service = CreateService();

        var user = await service.RegisterAsync("test@test.com", "password123");

        user.PasswordHash.Should().Be("hashed_password");
        _passwordHasher.Verify(h => h.Hash("password123"), Times.Once);
    }

    // ============================================
    // REGISTER — validation
    // ============================================

    [Fact]
    public async Task Register_WithEmptyEmail_Throws()
    {
        var service = CreateService();

        var act = async () => await service.RegisterAsync("", "password123");

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Email*");
    }

    [Fact]
    public async Task Register_WithShortPassword_Throws()
    {
        var service = CreateService();

        var act = async () => await service.RegisterAsync("test@test.com", "12345");

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*6 characters*");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Throws()
    {
        _userStore.Setup(s => s.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "test@test.com" });

        var service = CreateService();

        var act = async () => await service.RegisterAsync("test@test.com", "password123");

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*already registered*");
    }

    // ============================================
    // LOGIN
    // ============================================

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        var existing = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            PasswordHash = "hashed_password"
        };

        _userStore.Setup(s => s.GetByEmailAsync("test@test.com"))
            .ReturnsAsync(existing);
        _passwordHasher.Setup(h => h.Verify("password123", "hashed_password"))
            .Returns(true);

        var service = CreateService();

        var tokens = await service.LoginAsync("test@test.com", "password123");

        tokens.AccessToken.Should().Be("access_token");
        tokens.RefreshToken.Should().Be("refresh_token");
        tokens.AccessTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);

        // Login must persist exactly one refresh-token record.
        _refreshTokens.Verify(r => r.SaveAsync(It.IsAny<RefreshTokenData>()), Times.Once);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Throws()
    {
        _userStore.Setup(s => s.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        var service = CreateService();

        var act = async () => await service.LoginAsync("unknown@test.com", "password123");

        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*Invalid email or password*");
    }

    [Fact]
    public async Task Login_WithWrongPassword_Throws()
    {
        var existing = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            PasswordHash = "hashed_password"
        };

        _userStore.Setup(s => s.GetByEmailAsync("test@test.com"))
            .ReturnsAsync(existing);
        _passwordHasher.Setup(h => h.Verify("wrong_password", "hashed_password"))
            .Returns(false);

        var service = CreateService();

        var act = async () => await service.LoginAsync("test@test.com", "wrong_password");

        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*Invalid email or password*");
    }

    // ============================================
    // REFRESH
    // ============================================

    [Fact]
    public async Task Refresh_WithValidToken_RotatesAndReturnsNewPair()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        var existing = new RefreshTokenData(
            Id: Guid.NewGuid(),
            UserId: userId,
            TokenHash: "hash:old_refresh",
            FamilyId: familyId,
            ExpiresAt: DateTime.UtcNow.AddDays(3),
            CreatedAt: DateTime.UtcNow.AddDays(-1),
            RevokedAt: null,
            ReplacedByTokenId: null);

        _refreshTokens
            .Setup(r => r.GetByHashAsync("hash:old_refresh"))
            .ReturnsAsync(existing);

        _userStore.Setup(s => s.GetByIdAsync(userId))
            .ReturnsAsync(new User { Id = userId, Email = "test@test.com", PasswordHash = "x" });

        var service = CreateService();

        var tokens = await service.RefreshAsync("old_refresh");

        tokens.AccessToken.Should().Be("access_token");
        tokens.RefreshToken.Should().Be("refresh_token");

        // Old token revoked.
        _refreshTokens.Verify(
            r => r.RevokeAsync(existing.Id, It.IsAny<Guid?>()),
            Times.Once);
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_Throws()
    {
        _refreshTokens
            .Setup(r => r.GetByHashAsync(It.IsAny<string>()))
            .ReturnsAsync((RefreshTokenData?)null);

        var service = CreateService();

        var act = async () => await service.RefreshAsync("bogus");

        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*Invalid refresh token*");
    }

    [Fact]
    public async Task Refresh_WithRevokedToken_RevokesEntireFamily()
    {
        var familyId = Guid.NewGuid();

        var revoked = new RefreshTokenData(
            Id: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            TokenHash: "hash:stolen",
            FamilyId: familyId,
            ExpiresAt: DateTime.UtcNow.AddDays(3),
            CreatedAt: DateTime.UtcNow.AddDays(-1),
            RevokedAt: DateTime.UtcNow.AddHours(-1),
            ReplacedByTokenId: Guid.NewGuid());

        _refreshTokens
            .Setup(r => r.GetByHashAsync("hash:stolen"))
            .ReturnsAsync(revoked);

        var service = CreateService();

        var act = async () => await service.RefreshAsync("stolen");

        await act.Should().ThrowAsync<UnauthorizedException>();

        // Theft signal → entire family revoked.
        _refreshTokens.Verify(r => r.RevokeFamilyAsync(familyId), Times.Once);
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_Throws()
    {
        var expired = new RefreshTokenData(
            Id: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            TokenHash: "hash:expired",
            FamilyId: Guid.NewGuid(),
            ExpiresAt: DateTime.UtcNow.AddMinutes(-1),
            CreatedAt: DateTime.UtcNow.AddDays(-8),
            RevokedAt: null,
            ReplacedByTokenId: null);

        _refreshTokens
            .Setup(r => r.GetByHashAsync("hash:expired"))
            .ReturnsAsync(expired);

        var service = CreateService();

        var act = async () => await service.RefreshAsync("expired");

        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*expired*");
    }

    // ============================================
    // LOGOUT
    // ============================================

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var tokenId = Guid.NewGuid();
        var existing = new RefreshTokenData(
            Id: tokenId,
            UserId: Guid.NewGuid(),
            TokenHash: "hash:session",
            FamilyId: Guid.NewGuid(),
            ExpiresAt: DateTime.UtcNow.AddDays(3),
            CreatedAt: DateTime.UtcNow,
            RevokedAt: null,
            ReplacedByTokenId: null);

        _refreshTokens
            .Setup(r => r.GetByHashAsync("hash:session"))
            .ReturnsAsync(existing);

        var service = CreateService();

        await service.LogoutAsync("session");

        _refreshTokens.Verify(r => r.RevokeAsync(tokenId, null), Times.Once);
    }

    [Fact]
    public async Task Logout_WithUnknownToken_DoesNothing()
    {
        _refreshTokens
            .Setup(r => r.GetByHashAsync(It.IsAny<string>()))
            .ReturnsAsync((RefreshTokenData?)null);

        var service = CreateService();

        await service.LogoutAsync("bogus");

        _refreshTokens.Verify(r => r.RevokeAsync(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
    }
}