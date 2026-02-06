using System.Security.Claims;
using CreditTasksApi.Data;
using CreditTasksApi.Domain;
using CreditTasksApi.Dtos;
using CreditTasksApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditTasksApi.Controllers;

[ApiController]
[Authorize]
[Route("tasks")]
public class TasksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TaskExecutionService _exec;

    public TasksController(AppDbContext db, TaskExecutionService exec)
    {
        _db = db;
        _exec = exec;
    }

    [HttpPost]
    public async Task<ActionResult<TaskResponse>> Create([FromBody] CreateTaskRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        var name = string.IsNullOrWhiteSpace(req.Name) ? "Task" : req.Name.Trim();

        var task = new TaskItem
        {
            UserId = userId,
            Name = name,
            Status = CreditTaskStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync(ct);

        return Ok(ToResponse(task));
    }

    [HttpGet]
    public async Task<ActionResult<List<TaskResponse>>> List(CancellationToken ct)
    {
        var userId = GetUserId();
        var tasks = await _db.Tasks
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(100)
            .ToListAsync(ct);

        return Ok(tasks.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskResponse>> Get(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);
        if (task is null) return NotFound();
        return Ok(ToResponse(task));
    }

    [HttpPost("{id:guid}/execute")]
    public async Task<ActionResult<ExecuteTaskResponse>> Execute(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();

        (TaskItem task, string message) result;
        try
        {
            result = await _exec.ExecuteAsync(userId, id, ct);
        }
        catch (TaskNotFoundException)
        {
            return NotFound();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var (task, message) = result;
        return Ok(new ExecuteTaskResponse(task.Id, task.Status, task.ChargedCost, task.ExecutionSeconds, message));
    }

    private static TaskResponse ToResponse(TaskItem t) =>
        new(t.Id, t.Name, t.Status, t.ChargedCost, t.ExecutionSeconds, t.CreatedAtUtc, t.StartedAtUtc, t.CompletedAtUtc);

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.Parse(sub!);
    }
}
