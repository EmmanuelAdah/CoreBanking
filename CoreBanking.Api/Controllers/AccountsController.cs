using CoreBanking.Application.Interfaces;
using CoreBanking.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreBanking.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService) => _accountService = accountService;

    public record CreateAccountRequest(string CustomerName, string Email, string AccountType = "Savings");

    [HttpPost]
    [Authorize(Roles = $"{Roles.Officer},{Roles.Admin}")]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request, CancellationToken ct)
    {
        var account = await _accountService.CreateAccountAsync(request.CustomerName, request.Email, request.AccountType, ct);
        return CreatedAtAction(nameof(Get), new { accountNumber = account.AccountNumber }, account);
    }

    [HttpGet("{accountNumber}")]
    [Authorize(Roles = $"{Roles.Customer},{Roles.Officer},{Roles.Admin}")]
    public async Task<IActionResult> Get(string accountNumber, CancellationToken ct)
    {
        var account = await _accountService.GetAccountAsync(accountNumber, ct);
        return account == null ? NotFound() : Ok(account);
    }
}
