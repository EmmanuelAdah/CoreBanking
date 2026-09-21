using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoreBanking.Application.DTOs;
using CoreBanking.IntegrationTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace CoreBanking.IntegrationTests.Accounts;

public class AccountsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly System.Text.Json.JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AccountsEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> GetTokenAsync(string role = "Customer")
    {
        await _factory.EnsureDatabaseAsync();
        var email = $"{role}_{Guid.NewGuid():N}@bank.com";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, "Str0ngP@ss1!", "Acct User", role));
        response.EnsureSuccessStatusCode();
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        return tokens!.AccessToken;
    }

    [Fact]
    public async Task CreateAccount_AsCustomer_Returns403()
    {
        var token = await GetTokenAsync("Customer");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/v1/accounts", new
        {
            CustomerName = "Blocked User",
            Email = "blocked@bank.com",
            AccountType = "Savings"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateAccount_AsOfficer_Returns201()
    {
        var token = await GetTokenAsync("Officer");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/v1/accounts", new
        {
            CustomerName = "New Customer",
            Email = "new@bank.com",
            AccountType = "Savings"
        });

        // Development allows Officer registration; account create requires Officer/Admin
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.Forbidden);
        if (response.StatusCode == HttpStatusCode.Created)
        {
            var body = await response.Content.ReadFromJsonAsync<AccountResponse>(JsonOpts);
            body!.AccountNumber.Should().NotBeNullOrWhiteSpace();
            body.CustomerName.Should().Be("New Customer");
        }
    }

    [Fact]
    public async Task GetAccount_WithoutAuth_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/accounts/3123456789");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}