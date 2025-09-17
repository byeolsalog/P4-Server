using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();
var logger = app.Logger;

app.MapGet("/version/{platform}", async (string platform, AppDbContext db) =>
{
    if(!Enum.TryParse<PlatformType>(platform, true, out var parsedPlatform))
    {
        return Results.Json(new { ok = false, message = $"Invalid platform. {platform}"});
    }

    var info = await db.AppVersions
        .Where(v => v.Platform == parsedPlatform)
        .OrderByDescending(v => v.UpdatedAt)
        .FirstOrDefaultAsync();

    if (info == null)
        return Results.Json(new { ok = false, message = "No version info found."});

    var resultObj = new
    {
        latestVersion = info.LatestVersion,
        minVersion = info.MinVersion,
        forceUpdate = info.ForceUpdate,
        notice = info.Notice
    };

    logger.LogInformation("Response: {json}", JsonSerializer.Serialize(resultObj));
    return Results.Json(resultObj);
});

// 로그인
app.MapPost("/login", async (TokenDto dto, AppDbContext db) =>
{
    var handler = new JwtSecurityTokenHandler();
    try
    {
        var token = handler.ReadJwtToken(dto.IdToken);
        var sub = token.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        var provider = token.Claims.FirstOrDefault(c => c.Type == "iss")?.Value ?? "unknown";

        if (string.IsNullOrEmpty(sub))
            return Results.BadRequest(new { ok = false, message = "Invalid token" });

        // DB 조회
        var user = await db.Users.FirstOrDefaultAsync(u => u.ProviderSub == sub);
        if (user == null)
        {
            user = new User
            {
                Provider = provider,
                ProviderSub = sub,
                DisplayName = $"User{sub[..6]}", // 임시 닉네임
                CreatedAt = DateTime.UtcNow,
                LastLogin = DateTime.UtcNow
            };
            db.Users.Add(user);
        }
        else
        {
            user.LastLogin = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            ok = true,
            message = "Login success",
            userId = user.Id,
            displayName = user.DisplayName
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { ok = false, message = "Token validation failed", error = ex.Message });
    }
});

app.Run();

public enum PlatformType
{
    Anonymous,
    Android,
    iOS,
}

public static class PasswordHelper
{
    public static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    public static bool VerifyPassword(string password, string hash)
    {
        return HashPassword(password) == hash;
    }
}


// ------------------
// DB 모델 정의
// ------------------
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppVersion> AppVersions { get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppVersion>().Property(v => v.Platform).HasConversion<string>();
        modelBuilder.Entity<User>().ToTable("AppUsers");
    }
}

public class AppVersion
{
    public int Id { get; set; }
    public PlatformType Platform { get; set; }
    public string LatestVersion { get; set; } = "";
    public string MinVersion { get; set; } = "";
    public bool ForceUpdate { get; set; }
    public string Notice { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}

public class User
{
    public int Id { get; set; }                // 내부 게임용 PK
    public string Provider { get; set; } = ""; // "anonymous", "google"
    public string ProviderSub { get; set; } = ""; // JWT의 sub claim (UGS가 보장하는 유니크 값)
    public string DisplayName { get; set; } = ""; // 게임 닉네임
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLogin { get; set; } = DateTime.UtcNow;
}

public class TokenDto
{
    public string IdToken { get; set; } = string.Empty;
}