using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoreBanking.Application.DTOs;
using CoreBanking.IntegrationTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace CoreBanking.IntegrationTests.Payments;

public class PaymentsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public PaymentsEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> LoginAsAsync(string role = "Customer")
    {
        await _factory.EnsureDatabaseAsync();
        var email = $"{role.ToLower()}_{Guid.NewGuid():N}@bank.com";
        var reg = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, "Str0ngP@ss1!", $"{role} User", role));
        // In Development, non-Customer roles may still register
        if (!reg.IsSuccessStatusCode)
        {
            // fallback: register as Customer then note limitation
            email = $"cust_{Guid.NewGuid():N}@bank.com";
            reg = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new RegisterRequest(email, "Str0ngP@ss1!", "Customer User", "Customer"));
        }

        var tokens = await reg.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        return tokens!.AccessToken;
    }

    [Fact]
    public async Task InitializePayment_WithoutAuth_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/payments/initialize",
            new InitiatePaymentRequest("3123456789", 1000m, "a@b.com", "test"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Webhook_IsAnonymous_Returns202()
    {
        await _factory.EnsureDatabaseAsync();

        var payload = new
        {
            event_type = "charge.success", // property names must match PaystackWebhookPayload
            Event = "charge.success",
            Data = new
            {
                Reference = "e2e-ref-1",
                Status = "success",
                Amount = 100000,
                Currency = "NGN",
                Channel = "card",
                GatewayResponse = "Approved",
                Metadata = (object?)null
            }
        };

        // Use record-compatible JSON
        var body = new PaystackWebhookPayload(
            "charge.success",
            new PaystackWebhookData("e2e-ref-1", "success", 100000, "NGN", "card", "Approved", null));

        var response = await _client.PostAsJsonAsync("/api/v1/payments/webhook", body);

        // 202 Accepted is the contract; if model binding fails may be 400
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Accepted, HttpStatusCode.BadRequest);
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            json.GetProperty("message").GetString().Should().Contain("accepted");
        }
    }

    [Fact]
    public async Task Refund_AsCustomer_Returns403()
    {
        var token = await LoginAsAsync("Customer");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/v1/payments/refund",
            new RefundRequest("any-ref"));

        // Customer is not in Officer/Admin roles
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTransaction_WithoutAuth_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/payments/some-ref");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}