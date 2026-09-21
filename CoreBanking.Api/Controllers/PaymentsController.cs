using CoreBanking.Application.DTOs;
using CoreBanking.Application.Interfaces;
using CoreBanking.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreBanking.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpPost("initialize")]
    [Authorize(Roles = $"{Roles.Customer},{Roles.Officer},{Roles.Admin}")]
    [ProducesResponseType(typeof(InitiatePaymentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Initialize([FromBody] InitiatePaymentRequest request, CancellationToken ct)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        var result = await _paymentService.InitiatePaymentAsync(request, idempotencyKey, ct);
        return Ok(result);
    }

    /// <summary>Paystack webhook – public (secure with signature verification in production). Returns 202.</summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Webhook([FromBody] PaystackWebhookPayload payload, CancellationToken ct)
    {
        if (payload?.Data?.Reference == null)
            return BadRequest("Invalid payload");

        await _paymentService.ProcessWebhookAsync(payload, ct);
        return Accepted(new { message = "Webhook accepted for processing", reference = payload.Data.Reference });
    }

    [HttpGet("{reference}")]
    [Authorize(Roles = $"{Roles.Customer},{Roles.Officer},{Roles.Admin}")]
    public async Task<IActionResult> Get(string reference, CancellationToken ct)
    {
        var tx = await _paymentService.GetTransactionAsync(reference, ct);
        return tx == null ? NotFound() : Ok(tx);
    }

    [HttpPost("refund")]
    [Authorize(Roles = $"{Roles.Officer},{Roles.Admin}")]
    public async Task<IActionResult> Refund([FromBody] RefundRequest request, CancellationToken ct)
    {
        await _paymentService.RefundAsync(request, ct);
        return Ok(new { message = "Refund initiated" });
    }

    [HttpPost("disputes")]
    [Authorize(Roles = $"{Roles.Customer},{Roles.Officer},{Roles.Admin}")]
    public async Task<IActionResult> CreateDispute([FromBody] DisputeRequest request, CancellationToken ct)
    {
        var result = await _paymentService.CreateDisputeAsync(request, ct);
        return CreatedAtAction(nameof(CreateDispute), new { id = result.Id }, result);
    }
}
