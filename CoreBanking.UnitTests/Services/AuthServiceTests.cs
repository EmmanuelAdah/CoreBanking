using CoreBanking.Application.DTOs;
using CoreBanking.Application.Interfaces;
using CoreBanking.Application.Services;
using CoreBanking.Domain.Entities;
using CoreBanking.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CoreBanking.UnitTests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IJwtTokenService> _jwt = new();
    private readonly Mock<ILogger<AuthService>> _logger = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _jwt.Setup(j => j.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("access-token");
        _jwt.Setup(j => j.GenerateRefreshToken()).Returns("refresh-token");

        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _sut = new AuthService(_users.Object, _uow.Object, _jwt.Object, _logger.Object);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailIsNew_ReturnsTokensAndCreatesUser()
    {
        _users.Setup(r => r.GetByEmailAsync("alice@bank.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) => u);
        _users.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.RegisterAsync(new RegisterRequest(
            "alice@bank.com", "Str0ngP@ss!", "Alice Okoro", Roles.Customer));

        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
        result.Email.Should().Be("alice@bank.com");
        result.Role.Should().Be(Roles.Customer);
        result.FullName.Should().Be("Alice Okoro");

        _users.Verify(r => r.AddAsync(It.Is<User>(u =>
            u.Email == "alice@bank.com" &&
            u.Role == Roles.Customer &&
            !string.IsNullOrEmpty(u.PasswordHash)), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailExists_ThrowsInvalidOperationException()
    {
        _users.Setup(r => r.GetByEmailAsync("taken@bank.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Email = "taken@bank.com" });

        var act = () => _sut.RegisterAsync(new RegisterRequest(
            "taken@bank.com", "Str0ngP@ss!", "Bob", Roles.Customer));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokens()
    {
        // Pre-hash using the same algorithm AuthService uses (PBKDF2 or Argon2 depending on implementation).
        // For unit test we inject a user whose password will be verified by the real VerifyPassword.
        // Easiest path: register first via service then login — but Register uses real hashing.
        // So we call Register then Login against the same in-memory mock behavior.

        User? stored = null;
        _users.Setup(r => r.GetByEmailAsync("carol@bank.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => stored);
        _users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => stored = u)
            .ReturnsAsync((User u, CancellationToken _) => u);
        _users.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => stored = u)
            .Returns(Task.CompletedTask);

        await _sut.RegisterAsync(new RegisterRequest("carol@bank.com", "Str0ngP@ss!", "Carol", Roles.Customer));

        var result = await _sut.LoginAsync(new LoginRequest("carol@bank.com", "Str0ngP@ss!"));

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.Email.Should().Be("carol@bank.com");
        stored!.LastLoginAt.Should().NotBeNull();
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ThrowsUnauthorized()
    {
        User? stored = null;
        _users.Setup(r => r.GetByEmailAsync("dave@bank.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => stored);
        _users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => stored = u)
            .ReturnsAsync((User u, CancellationToken _) => u);
        _users.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.RegisterAsync(new RegisterRequest("dave@bank.com", "Correct#123", "Dave", Roles.Customer));

        var act = () => _sut.LoginAsync(new LoginRequest("dave@bank.com", "WrongPassword"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task LoginAsync_WhenUserDisabled_ThrowsUnauthorized()
    {
        _users.Setup(r => r.GetByEmailAsync("disabled@bank.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Email = "disabled@bank.com",
                IsActive = false,
                PasswordHash = "irrelevant"
            });

        var act = () => _sut.LoginAsync(new LoginRequest("disabled@bank.com", "any"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*disabled*");
    }

    [Fact]
    public async Task RefreshTokenAsync_WithValidToken_ReturnsNewTokens()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "eve@bank.com",
            FullName = "Eve",
            Role = Roles.Officer,
            IsActive = true,
            RefreshToken = "valid-refresh",
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(1)
        };

        _users.Setup(r => r.GetByRefreshTokenAsync("valid-refresh", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _users.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.RefreshTokenAsync(new RefreshTokenRequest("valid-refresh"));

        result.AccessToken.Should().Be("access-token");
        result.Role.Should().Be(Roles.Officer);
    }

    [Fact]
    public async Task RefreshTokenAsync_WithExpiredToken_ThrowsUnauthorized()
    {
        _users.Setup(r => r.GetByRefreshTokenAsync("expired", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                RefreshToken = "expired",
                RefreshTokenExpiry = DateTime.UtcNow.AddMinutes(-5),
                IsActive = true
            });

        var act = () => _sut.RefreshTokenAsync(new RefreshTokenRequest("expired"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_ClearsToken()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            RefreshToken = "to-revoke",
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(1)
        };

        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.RevokeRefreshTokenAsync(user.Id);

        user.RefreshToken.Should().BeNull();
        user.RefreshTokenExpiry.Should().BeNull();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}