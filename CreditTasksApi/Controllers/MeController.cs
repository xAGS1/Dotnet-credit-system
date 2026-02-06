using System.Security.Claims;
using CreditTasksApi.Data;
using CreditTasksApi.Dtos;
using CreditTasksApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditTasksApi.Controllers;

[ApiController]
[Authorize]
[Route("me")]
public class MeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CreditService _credits;

    public MeController(AppDbContext db, CreditService credits)
    {
        _db = db;
        _credits = credits;
    }

    [HttpGet]
    public async Task<ActionResult<MeResponse>> GetMe(CancellationToken ct)
    {
        var userId = GetUserId();

        // Concurrency-safe grant application
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var user = await _db.Users.FirstAsync(u => u.Id == userId, ct);
                await _credits.EnsureAutoGrantsAsync(user, ct);

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return Ok(new MeResponse(
                    user.Id,
                    user.Email,
                    user.Username,
                    user.Credits,
                    user.RegisteredAtUtc,
                    user.LastAutoGrantAtUtc
                ));
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync(ct);
            }
        }

        return StatusCode(409, "Too much contention. Retry.");
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                  ?? User.FindFirstValue("sub");
        return Guid.Parse(sub!);
    }
}
