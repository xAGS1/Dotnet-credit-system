using CreditTasksApi.Data;
using CreditTasksApi.Domain;
using CreditTasksApi.Dtos;
using CreditTasksApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditTasksApi.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher _hasher;
    private readonly JwtTokenService _jwt;

    public AuthController(AppDbContext db, PasswordHasher hasher, JwtTokenService jwt)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var username = req.Username.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("Email, username, and password are required.");

        if (req.Password.Length < 8)
            return BadRequest("Password must be at least 8 characters.");

        var exists = await _db.Users.AnyAsync(u => u.Email == email || u.Username == username, ct);
        if (exists) return Conflict("Email or username already exists.");

        var (hash, salt) = _hasher.Hash(req.Password);

        var user = new AppUser
        {
            Email = email,
            Username = username,
            PasswordHash = hash,
            PasswordSalt = salt,
            Credits = CreditService.StartingCredits,
            RegisteredAtUtc = DateTime.UtcNow,
            LastAutoGrantAtUtc = null
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        var (token, exp) = _jwt.CreateToken(user);
        return Ok(new AuthResponse(token, exp));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var key = req.EmailOrUsername.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email == key || u.Username.ToLower() == key, ct);

        if (user is null) return Unauthorized("Invalid credentials.");

        if (!_hasher.Verify(req.Password, user.PasswordHash, user.PasswordSalt))
            return Unauthorized("Invalid credentials.");

        var (token, exp) = _jwt.CreateToken(user);
        return Ok(new AuthResponse(token, exp));
    }
}
