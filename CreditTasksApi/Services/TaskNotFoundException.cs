namespace CreditTasksApi.Services;

public sealed class TaskNotFoundException : Exception
{
    public TaskNotFoundException(string? message = null, Exception? innerException = null)
        : base(message ?? "Task not found.", innerException)
    {
    }
}

