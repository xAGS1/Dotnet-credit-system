using CreditTasksApi.Domain;

namespace CreditTasksApi.Dtos;

public record CreateTaskRequest(string Name);

public record TaskResponse(
    Guid Id,
    string Name,
    CreditTaskStatus Status,
    int? ChargedCost,
    int? ExecutionSeconds,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc
);

public record ExecuteTaskResponse(
    Guid TaskId,
    CreditTaskStatus Status,
    int? ChargedCost,
    int? ExecutionSeconds,
    string Message
);
