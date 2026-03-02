using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BookingScheduleSystem.Contracts.Common;
using Microsoft.IdentityModel.Tokens;

namespace BookingScheduleSystem.Api.Infrastructure.Auth;

/// <summary>
/// JWT token generation service
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(IConfiguration configuration, ILogger<JwtTokenService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string GenerateToken(User user)
    {
        return GenerateTokenInternal(user, user.TenantId);
    }

    public string GenerateToken(User user, TenantId activeTenantId)
    {
        return GenerateTokenInternal(user, activeTenantId);
    }

    private string GenerateTokenInternal(User user, TenantId? tenantId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new("IsActive", user.IsActive.ToString())
        };

        if (tenantId.HasValue)
        {
            claims.Add(new Claim("TenantId", tenantId.Value.ToString()));
        }

        if (user.IsGlobalAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "GlobalAdmin"));
        }

        var secretKey = _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: GetTokenExpiration(),
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        _logger.LogInformation("Generated JWT token for user {UserId} with tenant {TenantId}", user.Id, tenantId);

        return tokenString;
    }

    public DateTime GetTokenExpiration()
    {
        return DateTime.UtcNow.AddHours(8);
    }
}
