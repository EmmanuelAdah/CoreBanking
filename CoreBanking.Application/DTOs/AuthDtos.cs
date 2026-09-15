namespace CoreBanking.Application.DTOs;

public record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string Role = "Customer" // Customer | Officer | Admin (Admin only creatable by existing Admin in production)
);

public record LoginRequest(
    string Email,
    string Password
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string Email,
    string FullName,
    string Role,
    Guid UserId
);

public record RefreshTokenRequest(
    string RefreshToken
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);