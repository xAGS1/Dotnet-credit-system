using System.Text;
using CreditTasksApi.Data;
using CreditTasksApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CreditTasksApi", Version = "v1" });

    // JWT auth in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("Default");
    options.UseSqlite(cs);
});

builder.Services.AddScoped<PasswordHasher>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<CreditService>();
builder.Services.AddScoped<TaskExecutionService>();

var jwtSection = builder.Configuration.GetSection("Jwt");
var issuer = jwtSection["Issuer"]!;
var audience = jwtSection["Audience"]!;
var key = jwtSection["Key"]!;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

static string ResolveDbPath(string connectionString)
{
    // Expected: "Data Source=./data/app.db"
    var parts = connectionString.Split('=', 2, StringSplitOptions.TrimEntries);
    var dataSource = parts.Length == 2 ? parts[1].Trim() : "./data/app.db";
    if (Path.IsPathRooted(dataSource)) return dataSource;
    return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, dataSource));
}

static bool HasBadConcurrencyTokenSchema(string cs)
{
    try
    {
        using var conn = new SqliteConnection(cs);
        conn.Open();

        // If Users table doesn't exist yet, schema is fine.
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Users';";
            var exists = Convert.ToInt32(cmd.ExecuteScalar());
            if (exists == 0) return false;
        }

        var found = false;
        var notNull = false;
        string? type = null;

        // Check PRAGMA table_info for ConcurrencyToken column
        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = "PRAGMA table_info('Users');";
        using var reader = cmd2.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(1);
            if (!string.Equals(name, "ConcurrencyToken", StringComparison.OrdinalIgnoreCase)) continue;

            found = true;
            type = reader.IsDBNull(2) ? null : reader.GetString(2);
            notNull = reader.GetInt32(3) == 1;
            break;
        }

        // If column is missing, EnsureCreated() will not fix it => self-heal.
        if (!found) return true;

        // We expect a required column for GUID (SQLite). Provider may map Guid as TEXT or BLOB.
        if (!notNull) return true;
        if (string.IsNullOrWhiteSpace(type)) return true;
        if (!string.Equals(type, "TEXT", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(type, "BLOB", StringComparison.OrdinalIgnoreCase))
            return true;

        using var cmd3 = conn.CreateCommand();
        if (string.Equals(type, "TEXT", StringComparison.OrdinalIgnoreCase))
        {
            // If data already exists with null/empty token, app will fail reading into non-nullable Guid.
            cmd3.CommandText =
                "SELECT COUNT(*) FROM Users WHERE ConcurrencyToken IS NULL OR ConcurrencyToken = '' OR ConcurrencyToken = '00000000-0000-0000-0000-000000000000';";
        }
        else
        {
            cmd3.CommandText = "SELECT COUNT(*) FROM Users WHERE ConcurrencyToken IS NULL;";
        }
        var badRows = Convert.ToInt32(cmd3.ExecuteScalar());
        return badRows > 0;
    }
    catch
    {
        // If anything goes wrong, do not delete DB.
        return false;
    }
}

// Ensure database exists and self-heal old broken schema in Development.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var cs = app.Configuration.GetConnectionString("Default")!;
    Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    if (app.Environment.IsDevelopment() && HasBadConcurrencyTokenSchema(cs))
    {
        var dbPath = ResolveDbPath(cs);
        if (File.Exists(dbPath))
        {
            // Self-heal: remove old DB with outdated concurrency schema
            File.Delete(dbPath);
        }
    }

    db.Database.EnsureCreated();
}

// Swagger: force the server URL to match the current request scheme/host.
app.UseSwagger(c =>
{
    c.PreSerializeFilters.Add((swagger, httpReq) =>
    {
        swagger.Servers = new List<OpenApiServer>
        {
            new() { Url = $"{httpReq.Scheme}://{httpReq.Host.Value}" }
        };
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
