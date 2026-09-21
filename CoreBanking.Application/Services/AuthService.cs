using System.Security.Cryptography;
using System.Text;
using CoreBanking.Application.DTOs;
using CoreBanking.Application.Interfaces;
using CoreBanking.Domain.Entities;
using CoreBanking.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoreBanking.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly IJwtTokenService _jwt;
    private readonly ILogger<AuthService> _logger;
    private const int WorkFactor = 12;

    public AuthService(
        IUserRepository users,
        IUnitOfWork uow,
        IJwtTokenService jwt,
        ILogger<AuthService> logger)
    {
        _users = users;
        _uow = uow;
        _jwt = jwt;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var existing = await _users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);
        if (existing != null)
            throw new InvalidOperationException("Email is already registered.");

        var role = NormalizeRole(request.Role);

        // In production: only Admins can create Officer/Admin accounts.
        // For bootstrap we allow the first Admin via seed or env flag.

        var user = new User
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            FullName = request.FullName.Trim(),
            PasswordHash = HashPassword(request.Password),
            Role = role,
            IsActive = true
        };

        await _users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("User registered: {Email} ({Role})", user.Email, user.Role);
        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is disabled.");

        if (!VerifyPassword(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        user.LastLoginAt = DateTime.UtcNow;
        await _users.UpdateAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("User logged in: {Email}", user.Email);
        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByRefreshTokenAsync(request.RefreshToken, ct)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        if (user.RefreshTokenExpiry < DateTime.UtcNow || !user.IsActive)
            throw new UnauthorizedAccessException("Refresh token expired or account disabled.");

        return await IssueTokensAsync(user, ct);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("User not found.");

        if (!VerifyPassword(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        user.PasswordHash = HashPassword(request.NewPassword);
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await _users.UpdateAsync(user, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task RevokeRefreshTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user == null) return;

        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await _users.UpdateAsync(user, ct);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken ct)
    {
        var accessToken = _jwt.GenerateAccessToken(user.Id, user.Email, user.Role, user.FullName);
        var refreshToken = _jwt.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _users.UpdateAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        var expiresMinutes = 30; // keep in sync with JWT config default
        return new AuthResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(expiresMinutes),
            user.Email,
            user.FullName,
            user.Role,
            user.Id);
    }

    private static string NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return Roles.Customer;
        role = role.Trim();
        return Roles.All.Contains(role, StringComparer.OrdinalIgnoreCase)
            ? Roles.All.First(r => r.Equals(role, StringComparison.OrdinalIgnoreCase))
            : Roles.Customer;
    }

    // PBKDF2 password hashing (no external package required)
    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    private static bool VerifyPassword(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}