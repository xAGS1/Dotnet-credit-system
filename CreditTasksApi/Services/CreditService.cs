using CreditTasksApi.Data;
using CreditTasksApi.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditTasksApi.Services;

/// <summary>
/// Applies auto-grants and provides concurrency-safe credit operations.
/// Business rules:
/// - Start credits: 500
/// - Auto grant: 100 credits every 3 days per user (anchored to registration date)
/// - Whole-number credits only
/// - Credits never negative
/// </summary>
public class CreditService
{
    public const int StartingCredits = 500;
    public const int AutoGrantAmount = 100;
    public static readonly TimeSpan AutoGrantEvery = TimeSpan.FromDays(3);

    private readonly AppDbContext _db;

    public CreditService(AppDbContext db)
    {
        _db = db;
    }

    public async Task EnsureAutoGrantsAsync(AppUser user, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var anchor = user.LastAutoGrantAtUtc ?? user.RegisteredAtUtc;

        var elapsed = now - anchor;
        if (elapsed < AutoGrantEvery) return;

        var periods = (int)Math.Floor(elapsed.TotalDays / AutoGrantEvery.TotalDays);
        if (periods <= 0) return;

        user.Credits += periods * AutoGrantAmount;
        user.LastAutoGrantAtUtc = anchor.AddDays(periods * AutoGrantEvery.TotalDays);

        // No SaveChanges here; caller should save within its transaction.
        await Task.CompletedTask;
    }

    /// <summary>
    /// Deducts credits safely with optimistic concurrency. Returns false if insufficient.
    /// Caller should have already ensured auto-grants.
    /// </summary>
    public bool TryDeduct(AppUser user, int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (user.Credits - amount < 0) return false;
        user.Credits -= amount;
        return true;
    }

    public Task<AppUser?> FindUserAsync(Guid userId, CancellationToken ct)
        => _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
}
