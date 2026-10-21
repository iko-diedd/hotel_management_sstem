using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HousekeepingApi.Domain;
using Microsoft.IdentityModel.Tokens;

namespace HousekeepingApi.Services;

public class JwtOptions
{
    public string Issuer { get; set; } = "HousekeepingApi";
    public string Audience { get; set; } = "HousekeepingClients";
    public string Key { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 60;
}

public class TokenService
{
    private readonly JwtOptions _opt;
    public TokenService(Microsoft.Extensions.Options.IOptions<JwtOptions> opt) => _opt = opt.Value;

    public (string Token, DateTime ExpiresAt) Create(User user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_opt.ExpiryMinutes);
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _opt.Issuer, _opt.Audience,
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            },
            notBefore: DateTime.UtcNow, expires: expires, signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
