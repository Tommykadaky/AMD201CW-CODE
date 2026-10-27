using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Identity.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Api.Services;

public class JwtSettings
{
    public string Issuer { get; set; } = "mamxanh-identity";
    public string Audience { get; set; } = "mamxanh";
    public string Key { get; set; } = "";               // It nhat 32 ky tu
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 7;
}

public class JwtTokenService(JwtSettings settings)
{
    // Tao access token: chua id, email, ten va role cua user.
    // Cac service khac (qua Gateway) doc token nay de biet ai dang goi.
    public (string Token, DateTime ExpiresAt) CreateAccessToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.DisplayName),
            new(ClaimTypes.Role, user.Role),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));
        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    // Refresh token chi la mot chuoi ngau nhien, luu trong database
    public RefreshToken CreateRefreshToken(Guid userId) => new()
    {
        Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
        UserId = userId,
        ExpiresAt = DateTime.UtcNow.AddDays(settings.RefreshTokenDays),
    };
}
