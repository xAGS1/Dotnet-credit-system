using CreditTasksApi.Data;
using CreditTasksApi.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditTasksApi.Services;

public class TaskExecutionService
{
    private readonly AppDbContext _db;
    private readonly CreditService _credits;

    public TaskExecutionService(AppDbContext db, CreditService credits)
    {
        _db = db;
        _credits = credits;
    }

    public async Task<(TaskItem task, string message)> ExecuteAsync(Guid userId, Guid taskId, CancellationToken ct)
    {
        // We use optimistic concurrency and retry to handle concurrent executions.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId, ct);
                if (task is null) throw new TaskNotFoundException();

                // Idempotency: if cost already calculated or not pending, do not charge again.
                if (task.Status != CreditTaskStatus.Pending)
                {
                    await tx.CommitAsync(ct);
                    return (task, $"Task is already {task.Status}.");
                }

                var user = await _db.Users.FirstAsync(u => u.Id == userId, ct);

                // Apply any auto-grants before charging/rejecting.
                await _credits.EnsureAutoGrantsAsync(user, ct);

                var cost = Random.Shared.Next(1, 16); // inclusive 1..15
                task.ChargedCost = cost;

                // If insufficient credits: reject, no deduction.
                if (user.Credits - cost < 0)
                {
                    task.Status = CreditTaskStatus.RejectedInsufficientCredits;
                    task.StartedAtUtc = DateTime.UtcNow;
                    task.CompletedAtUtc = DateTime.UtcNow;
                    task.ExecutionSeconds = 0;

                    await _db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);

                    return (task, "Rejected: insufficient credits.");
                }

                // Deduct and mark running (charge happens exactly once here).
                user.Credits -= cost;

                task.Status = CreditTaskStatus.Running;
                task.StartedAtUtc = DateTime.UtcNow;

                // Random duration 10..40 seconds
                task.ExecutionSeconds = Random.Shared.Next(10, 41);

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                // Simulate work outside the transaction (so other requests can proceed).
                await Task.Delay(TimeSpan.FromSeconds(task.ExecutionSeconds.Value), ct);

                // Random success/failure outcome (credits are NOT refunded on failure).
                var success = Random.Shared.NextDouble() < 0.75;

                // Update final status (retry if concurrency conflicts).
                await FinalizeAsync(userId, taskId, success, ct);

                // Reload task to return latest
                var refreshed = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId, ct);
                if (refreshed is null) throw new TaskNotFoundException();
                return (refreshed, success ? "Succeeded." : "Failed after consuming credits.");
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync(ct);
                // retry
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        throw new InvalidOperationException("Too much contention. Please retry.");
    }

    private async Task FinalizeAsync(Guid userId, Guid taskId, bool success, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId, ct);
                if (task is null) throw new TaskNotFoundException();

                // If already finalized by another request, keep it (idempotent).
                if (task.Status is CreditTaskStatus.Succeeded or CreditTaskStatus.Failed or CreditTaskStatus.RejectedInsufficientCredits)
                    return;

                task.Status = success ? CreditTaskStatus.Succeeded : CreditTaskStatus.Failed;
                task.CompletedAtUtc = DateTime.UtcNow;

                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException)
            {
                // retry
            }
        }
    }
}
