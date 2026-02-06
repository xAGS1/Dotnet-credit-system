using System.ComponentModel.DataAnnotations;

namespace CreditTasksApi.Domain;

public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    // PBKDF2 hash + salt (both base64)
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;

    public int Credits { get; set; }

    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;

    // Tracks the last time we applied auto-grants (anchored to registration date).
    public DateTime? LastAutoGrantAtUtc { get; set; }

    public List<TaskItem> Tasks { get; set; } = new();

    // Optimistic concurrency
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
}
