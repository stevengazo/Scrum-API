using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Scrum.Api.Data;
using Scrum.Api.Models;

namespace Scrum.Api.Auth;

public class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "scrum-api";
    public string Audience { get; set; } = "scrum-web";
    public int ExpiresMinutes { get; set; } = 240;
}

public record UserDto(string Id, string Email, string DisplayName, string Color, string[] Roles);

public class TokenService(Microsoft.Extensions.Options.IOptions<JwtOptions> options)
{
    private readonly JwtOptions _o = options.Value;

    public string Create(AppUser user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new("sub", user.Id),
            new("name", user.DisplayName),
            new("email", user.Email ?? ""),
        };
        claims.AddRange(roles.Select(r => new Claim("role", r)));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _o.Issuer,
            Audience = _o.Audience,
            Expires = DateTime.UtcNow.AddMinutes(_o.ExpiresMinutes),
            SigningCredentials = new SigningCredentials(SigningKey(_o), SecurityAlgorithms.HmacSha256),
        });
    }

    public static SymmetricSecurityKey SigningKey(JwtOptions o) => new(Encoding.UTF8.GetBytes(o.Key));
}

public static class AuthExtensions
{
    public static string UserId(this ClaimsPrincipal user) =>
        user.FindFirstValue("sub") ?? throw new InvalidOperationException("Authenticated user has no 'sub' claim.");

    public static IServiceCollection AddScrumAuth(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwt.Key) && env.IsDevelopment())
            jwt.Key = "dev-only-key-do-not-use-in-production-0123456789";
        if (jwt.Key.Length < 32)
            throw new InvalidOperationException("Jwt:Key must be set (>= 32 characters). Provide it via the Jwt__Key environment variable.");
        services.Configure<JwtOptions>(o => { o.Key = jwt.Key; o.Issuer = jwt.Issuer; o.Audience = jwt.Audience; o.ExpiresMinutes = jwt.ExpiresMinutes; });
        services.AddScoped<TokenService>();

        services.AddIdentityCore<AppUser>(o =>
        {
            o.User.RequireUniqueEmail = true;
            o.Password.RequiredLength = 8;
            o.Password.RequireNonAlphanumeric = false;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<ScrumDbContext>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                // Sin esto "role"/"sub" se reasignan a URIs largas y RoleClaimType/UserId() dejan de coincidir.
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = TokenService.SigningKey(jwt),
                    NameClaimType = "name",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.ManageProjects, p => p.RequireRole(Roles.Admin, Roles.ScrumMaster, Roles.ProductOwner))
            .AddPolicy(Policies.ManageSprints, p => p.RequireRole(Roles.Admin, Roles.ScrumMaster))
            .AddPolicy(Policies.Contribute, p => p.RequireRole(Roles.Admin, Roles.ScrumMaster, Roles.ProductOwner, Roles.Developer))
            .AddPolicy(Policies.ManageUsers, p => p.RequireRole(Roles.Admin));
        return services;
    }

    public static async Task SeedRolesAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var r in Roles.All)
            if (!await roles.RoleExistsAsync(r)) await roles.CreateAsync(new IdentityRole(r));
    }

    private static readonly string[] Palette = ["#6366f1", "#0ea5e9", "#10b981", "#f59e0b", "#ef4444", "#8b5cf6", "#ec4899", "#14b8a6"];

    public static string ColorFor(string seed) => Palette[(int)((uint)seed.GetHashCode() % Palette.Length)];
}
