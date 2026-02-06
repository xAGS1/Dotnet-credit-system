namespace CreditTasksApi.Dtos;

public record RegisterRequest(string Email, string Username, string Password);
public record LoginRequest(string EmailOrUsername, string Password);

public record AuthResponse(string Token, DateTime ExpiresAtUtc);

public record MeResponse(
    Guid Id,
    string Email,
    string Username,
    int Credits,
    DateTime RegisteredAtUtc,
    DateTime? LastAutoGrantAtUtc
);
