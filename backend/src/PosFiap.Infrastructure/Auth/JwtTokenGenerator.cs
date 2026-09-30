using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PosFiap.Application.Interfaces;
using PosFiap.Domain.Entities;

namespace PosFiap.Infrastructure.Auth;

/// <summary>
/// Gera access tokens de curta duração e refresh tokens de longa duração.
/// O refresh token permite renovar a sessão sem pedir a senha novamente.
/// </summary>
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;

    public JwtTokenGenerator(IOptions<JwtSettings> settings) => _settings = settings.Value;

    public string GenerateToken(User user)
    {
        return Generate(user, "access", DateTime.UtcNow.AddMinutes(_settings.ExpirationMinutes));
    }

    public string GenerateRefreshToken(User user)
    {
        return Generate(user, "refresh", DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays));
    }

    private string Generate(User user, string tokenType, DateTime expires)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email.Value),
            new Claim("name", user.Name),
            new Claim("token_type", tokenType),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
