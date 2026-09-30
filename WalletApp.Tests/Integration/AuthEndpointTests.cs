using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace WalletApp.Tests.Integration;

public class AuthEndpointTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthEndpointTests(CustomWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    // ============================================
    // REGISTER
    // ============================================

    [Fact]
    public async Task Register_WithValidData_ReturnsUserIdAndEmail()
    {
        var response = await _client.PostAsJsonAsync("/auth/register", new
        {
            email = "newuser@test.com",
            password = "password123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        body.Should().NotBeNull();
        body!.Email.Should().Be("newuser@test.com");
        body.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsError()
    {
        await _client.PostAsJsonAsync("/auth/register", new
        {
            email = "dup@test.com",
            password = "password123"
        });

        var response = await _client.PostAsJsonAsync("/auth/register", new
        {
            email = "dup@test.com",
            password = "password123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ============================================
    // LOGIN
    // ============================================

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        await _client.PostAsJsonAsync("/auth/register", new
        {
            email = "login@test.com",
            password = "password123"
        });

        var response = await _client.PostAsJsonAsync("/auth/login", new
        {
            email = "login@test.com",
            password = "password123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsError()
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new
        {
            email = "ghost@test.com",
            password = "password123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsError()
    {
        await _client.PostAsJsonAsync("/auth/register", new
        {
            email = "wrongpass@test.com",
            password = "password123"
        });

        var response = await _client.PostAsJsonAsync("/auth/login", new
        {
            email = "wrongpass@test.com",
            password = "wrong_password"
        });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // Response shapes
    private record RegisterResponse(Guid Id, string Email);
    private record LoginResponse(string Token);
}