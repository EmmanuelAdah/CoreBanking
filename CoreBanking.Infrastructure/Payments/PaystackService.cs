using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoreBanking.Application.DTOs;
using CoreBanking.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CoreBanking.Infrastructure.Payments;

public class PaystackService : IPaystackService
{
    private readonly HttpClient _http;
    private readonly ILogger<PaystackService> _logger;
    private readonly string _secretKey;
    private readonly string _baseUrl;

    public PaystackService(HttpClient http, IConfiguration config, ILogger<PaystackService> logger)
    {
        _http = http;
        _logger = logger;
        _secretKey = config["PAYSTACK_SECRET_KEY"] ?? throw new InvalidOperationException("PAYSTACK_SECRET_KEY missing");
        _baseUrl = config["PAYSTACK_BASE_URL"] ?? "https://api.paystack.co";
        _http.BaseAddress = new Uri(_baseUrl);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _secretKey);
    }

    public async Task<InitiatePaymentResponse> InitializeTransactionAsync(InitiatePaymentRequest request, string reference, CancellationToken ct = default)
    {
        var payload = new
        {
            email = request.Email,
            amount = (int)(request.Amount * 100), // kobo
            reference,
            currency = "NGN",
            callback_url = request.CallbackUrl,
            metadata = new { account_number = request.AccountNumber, narration = request.Narration }
        };

        var response = await _http.PostAsJsonAsync("/transaction/initialize", payload, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Paystack initialize failed: {Content}", content);
            throw new InvalidOperationException($"Paystack error: {content}");
        }

        using var doc = JsonDocument.Parse(content);
        var data = doc.RootElement.GetProperty("data");

        return new InitiatePaymentResponse(
            Reference: data.GetProperty("reference").GetString()!,
            AuthorizationUrl: data.GetProperty("authorization_url").GetString()!,
            AccessCode: data.GetProperty("access_code").GetString()!,
            Status: "initialized"
        );
    }

    public async Task<bool> VerifyTransactionAsync(string reference, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/transaction/verify/{reference}", ct);
        if (!response.IsSuccessStatusCode) return false;

        var content = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);
        var status = doc.RootElement.GetProperty("data").GetProperty("status").GetString();
        return status == "success";
    }

    public async Task<bool> RefundTransactionAsync(string reference, decimal? amount, string reason, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object>
        {
            ["transaction"] = reference,
            ["merchant_note"] = reason
        };
        if (amount.HasValue)
            payload["amount"] = (int)(amount.Value * 100);

        var response = await _http.PostAsJsonAsync("/refund", payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Paystack refund failed: {Error}", err);
            return false;
        }
        return true;
    }
}