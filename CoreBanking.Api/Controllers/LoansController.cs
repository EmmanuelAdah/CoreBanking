using CoreBanking.Application.DTOs;
using CoreBanking.Application.Interfaces;
using CoreBanking.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreBanking.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class LoansController : ControllerBase
{
    private readonly ILoanService _loanService;

    public LoansController(ILoanService loanService) => _loanService = loanService;

    [HttpPost("apply")]
    [Authorize(Roles = $"{Roles.Customer},{Roles.Officer},{Roles.Admin}")]
    public async Task<IActionResult> Apply([FromBody] LoanApplicationRequest request, CancellationToken ct)
    {
        var result = await _loanService.ApplyForLoanAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{Roles.Customer},{Roles.Officer},{Roles.Admin}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var loan = await _loanService.GetLoanAsync(id, ct);
        return loan == null ? NotFound() : Ok(loan);
    }

    [HttpPost("{id:guid}/disburse")]
    [Authorize(Roles = $"{Roles.Officer},{Roles.Admin}")]
    public async Task<IActionResult> Disburse(Guid id, CancellationToken ct)
    {
        await _loanService.DisburseLoanAsync(id, ct);
        return Ok(new { message = "Loan disbursed successfully" });
    }
}
