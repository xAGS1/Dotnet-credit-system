using System.ComponentModel.DataAnnotations;

namespace CreditTasksApi.Domain;

public enum CreditTaskStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    RejectedInsufficientCredits
}

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = "Task";

    public CreditTaskStatus Status { get; set; } = CreditTaskStatus.Pending;

    // Cost computed exactly once at execution time (stored for idempotency).
    public int? ChargedCost { get; set; }

    // Execution duration (seconds), stored for observability / debugging.
    public int? ExecutionSeconds { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    // Optimistic concurrency
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
}
