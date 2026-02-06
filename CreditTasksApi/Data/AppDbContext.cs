using CreditTasksApi.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditTasksApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(b =>
        {
            b.HasIndex(x => x.Email).IsUnique();
            b.HasIndex(x => x.Username).IsUnique();
            b.Property(x => x.Credits).IsRequired();
            b.Property(x => x.RegisteredAtUtc).IsRequired();

            b.Property(x => x.ConcurrencyToken).IsRequired().IsConcurrencyToken();
        });

        modelBuilder.Entity<TaskItem>(b =>
        {
            b.HasIndex(x => new { x.UserId, x.Id });
            b.Property(x => x.Status).HasConversion<string>();
            b.Property(x => x.ConcurrencyToken).IsRequired().IsConcurrencyToken();

            b.HasOne(x => x.User)
                .WithMany(u => u.Tasks)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public override int SaveChanges()
    {
        ApplyConcurrencyTokens();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyConcurrencyTokens();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Ensures our GUID concurrency token participates in optimistic concurrency.
    /// On UPDATE, we generate a fresh token so concurrent writers conflict reliably.
    /// </summary>
    private void ApplyConcurrencyTokens()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                if (entry.Entity is AppUser || entry.Entity is TaskItem)
                {
                    var prop = entry.Property("ConcurrencyToken");
                    if (entry.State == EntityState.Added)
                    {
                        var current = (Guid?)prop.CurrentValue;
                        if (!current.HasValue || current.Value == Guid.Empty)
                            prop.CurrentValue = Guid.NewGuid();
                    }
                    else
                    {
                        prop.CurrentValue = Guid.NewGuid();
                    }
                }
            }
        }
    }
}
