using FluentAssertions;
using Moq;
using WalletApp.Core.Auth;

namespace WalletApp.Tests.Unit;

public class AuthServiceTests
{
    // Mocks
    private readonly Mock<IUserStore> _userStore = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ITokenService> _tokenService = new();

    private AuthService CreateService() =>
        new AuthService(_userStore.Object, _passwordHasher.Object, _tokenService.Object);

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

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*Email*");
    }

    [Fact]
    public async Task Register_WithShortPassword_Throws()
    {
        var service = CreateService();

        var act = async () => await service.RegisterAsync("test@test.com", "12345");

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*6 characters*");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Throws()
    {
        _userStore.Setup(s => s.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "test@test.com" });

        var service = CreateService();

        var act = async () => await service.RegisterAsync("test@test.com", "password123");

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*already registered*");
    }

    // ============================================
    // LOGIN
    // ============================================

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
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
        _tokenService.Setup(t => t.GenerateToken(existing))
            .Returns("jwt_token_here");

        var service = CreateService();

        var token = await service.LoginAsync("test@test.com", "password123");

        token.Should().Be("jwt_token_here");
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Throws()
    {
        _userStore.Setup(s => s.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        var service = CreateService();

        var act = async () => await service.LoginAsync("unknown@test.com", "password123");

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*Invalid credentials*");
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

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*Invalid credentials*");
    }
}