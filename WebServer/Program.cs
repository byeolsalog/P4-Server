using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using MySql.Data.MySqlClient;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseKestrel().ConfigureKestrel((context, options) => { options.Configure(context.Configuration.GetSection("Kestrel")); });
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(10, 11, 13)) // MariaDB 버전
    )
);

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
app.MapPost("/login", async (TokenDto dto, AppDbContext db, IConfiguration config) =>
{
    var handler = new JwtSecurityTokenHandler();
    try
    {
        // Firebase ID 토큰 검증 (간단: 서명 확인 생략, 실제 운영시 Firebase 공개키 검증 필요)
        var token = handler.ReadJwtToken(dto.IdToken);
        var sub = token.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        var iss = token.Claims.FirstOrDefault(c => c.Type == "iss")?.Value ?? "unknown";
        var provider = iss.Contains("securetoken.google.com") ? "google" : "anonymous";

        if (string.IsNullOrEmpty(sub))
            return Results.BadRequest(new { ok = false, message = "Invalid token" });

        var now = DateTime.UtcNow.AddHours(9); // 한국 시간

        // DB 조회
        var user = await db.Users.FirstOrDefaultAsync(u => u.ProviderSub == sub);
        if (user == null)
        {
            user = new User
            {
                Provider = provider,
                ProviderSub = sub,
                DisplayName = NameGenerator.RandomName(6),
                CreatedAt = now,
                LastLogin = now
            };
            db.Users.Add(user);
        }
        else
        {
            user.LastLogin = now;
        }

        await db.SaveChangesAsync();

        // -------------------
        // JWT 발급
        // -------------------
        var jwtKey = config["Jwt:Key"];
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new System.Security.Claims.Claim("userId", user.Id.ToString()),
            new System.Security.Claims.Claim("displayName", user.DisplayName),
            new System.Security.Claims.Claim("provider", user.Provider)
        };

        var jwt = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"] ?? "game-login",
            audience: config["Jwt:Audience"] ?? "game-server",
            claims: claims,
            expires: now.AddHours(1), // 1시간짜리 토큰
            signingCredentials: creds
        );

        var accessToken = handler.WriteToken(jwt);

        return Results.Ok(new
        {
            ok = true,
            message = "Login success",
            userId = user.Id,
            displayName = user.DisplayName,
            accessToken
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

public static class NameGenerator
{
    private static readonly Random _rand = new();
    private const string _chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public static string RandomName(int length = 6)
    {
        return new string(Enumerable.Repeat(_chars, length)
            .Select(s => s[_rand.Next(s.Length)]).ToArray());
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
        modelBuilder.Entity<User>().Property(u => u.Id).ValueGeneratedOnAdd();
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