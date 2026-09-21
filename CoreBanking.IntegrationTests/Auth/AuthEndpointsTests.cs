using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoreBanking.Application.DTOs;
using CoreBanking.IntegrationTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace CoreBanking.IntegrationTests.Auth;

public class AuthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AuthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_Then_Login_ReturnsTokens()
    {
        await _factory.EnsureDatabaseAsync();

        var email = $"user_{Guid.NewGuid():N}@bank.com";
        var register = new RegisterRequest(email, "Str0ngP@ss1!", "Test User", "Customer");

        var regResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", register);
        regResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var regBody = await regResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        regBody.Should().NotBeNull();
        regBody!.AccessToken.Should().NotBeNullOrWhiteSpace();
        regBody.RefreshToken.Should().NotBeNullOrWhiteSpace();
        regBody.Email.Should().Be(email);
        regBody.Role.Should().Be("Customer");

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(email, "Str0ngP@ss1!"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        loginBody!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        await _factory.EnsureDatabaseAsync();

        var email = $"bad_{Guid.NewGuid():N}@bank.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, "Str0ngP@ss1!", "User", "Customer"));

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(email, "WrongPassword"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsProfile()
    {
        await _factory.EnsureDatabaseAsync();

        var email = $"me_{Guid.NewGuid():N}@bank.com";
        var reg = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, "Str0ngP@ss1!", "Me User", "Customer"));
        var tokens = await reg.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var me = await _client.GetAsync("/api/v1/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await me.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().Be(email);
        body.GetProperty("role").GetString().Should().Be("Customer");
    }

    [Fact]
    public async Task Refresh_ReturnsNewAccessToken()
    {
        await _factory.EnsureDatabaseAsync();

        var email = $"refresh_{Guid.NewGuid():N}@bank.com";
        var reg = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, "Str0ngP@ss1!", "Refresh User", "Customer"));
        var tokens = await reg.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);

        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest(tokens!.RefreshToken));

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        refreshed!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DuplicateRegister_Returns400()
    {
        await _factory.EnsureDatabaseAsync();

        var email = $"dup_{Guid.NewGuid():N}@bank.com";
        var req = new RegisterRequest(email, "Str0ngP@ss1!", "Dup", "Customer");

        (await _client.PostAsJsonAsync("/api/v1/auth/register", req)).StatusCode
            .Should().Be(HttpStatusCode.Created);

        var second = await _client.PostAsJsonAsync("/api/v1/auth/register", req);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}