using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ArtistPlatform.Domain.Enums;
using ArtistPlatform.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ArtistPlatform.Infrastructure.Auth;

public class JwtSettings
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "ArtistPlatform";
    public string Audience { get; set; } = "ArtistPlatformApp";
    public string SecretKey { get; set; } = default!;
    public int AccessTokenMinutes { get; set; } = 120;
    public int RefreshTokenDays { get; set; } = 7;
}

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) GenerateAccessToken(AppUser user);
    (string Token, DateTime ExpiresAtUtc) GenerateRefreshToken();
}

public class JwtTokenService(IOptions<JwtSettings> options) : IJwtTokenService
{
    private readonly JwtSettings _settings = options.Value;

    public (string Token, DateTime ExpiresAtUtc) GenerateAccessToken(AppUser user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        if (user.ArtistId.HasValue)
            claims.Add(new Claim("artistId", user.ArtistId.Value.ToString()));
        if (user.ContractorId.HasValue)
            claims.Add(new Claim("contractorId", user.ContractorId.Value.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return (Convert.ToBase64String(bytes), DateTime.UtcNow.AddDays(_settings.RefreshTokenDays));
    }
}
