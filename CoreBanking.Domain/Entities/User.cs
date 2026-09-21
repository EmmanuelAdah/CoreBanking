namespace CoreBanking.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Customer; // Customer, Officer, Admin
    public string? PhoneNumber { get; set; }
    public string? BVN { get; set; }
    public string? CustomerId { get; set; } // links to Account.CustomerId when applicable
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
}

public static class Roles
{
    public const string Customer = "Customer";
    public const string Officer = "Officer";
    public const string Admin = "Admin";

    public static readonly string[] All = { Customer, Officer, Admin };
}