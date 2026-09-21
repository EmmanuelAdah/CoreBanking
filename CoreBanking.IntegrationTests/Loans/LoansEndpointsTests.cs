using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoreBanking.Application.DTOs;
using CoreBanking.IntegrationTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace CoreBanking.IntegrationTests.Loans;

public class LoansEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly System.Text.Json.JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public LoansEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> GetTokenAsync(string role = "Customer")
    {
        await _factory.EnsureDatabaseAsync();
        var email = $"loan_{role}_{Guid.NewGuid():N}@bank.com";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, "Str0ngP@ss1!", "Loan User", role));
        response.EnsureSuccessStatusCode();
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        return tokens!.AccessToken;
    }

    [Fact]
    public async Task ApplyForLoan_WithoutAuth_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/loans/apply",
            new LoanApplicationRequest("3123456789", 100_000m, 12));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApplyForLoan_WithAuth_ButUnknownAccount_Returns400Or500()
    {
        var token = await GetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/v1/loans/apply",
            new LoanApplicationRequest("0000000000", 100_000m, 12));

        // Service throws InvalidOperationException for missing account
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Disburse_AsCustomer_Returns403()
    {
        var token = await GetTokenAsync("Customer");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsync($"/api/v1/loans/{Guid.NewGuid()}/disburse", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}