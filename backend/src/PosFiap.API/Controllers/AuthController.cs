using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PosFiap.Application.DTOs;
using PosFiap.Application.Interfaces;
using PosFiap.Application.UseCases.Auth;
using PosFiap.Domain.Exceptions;
using PosFiap.Domain.Interfaces;
using PosFiap.Infrastructure.Auth;
using PosFiap.Domain.ValueObjects;

namespace PosFiap.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly RegisterUseCase _registerUseCase;
    private readonly LoginUseCase _loginUseCase;
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly JwtSettings _jwtSettings;

    public AuthController(
        RegisterUseCase registerUseCase,
        LoginUseCase loginUseCase,
        IUserRepository userRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        IOptions<JwtSettings> jwtSettings)
    {
        _registerUseCase = registerUseCase;
        _loginUseCase = loginUseCase;
        _userRepository = userRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _jwtSettings = jwtSettings.Value;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<AuthResponseDto>> Register(
        [FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _registerUseCase.ExecuteAsync(request, cancellationToken);
        return Created(string.Empty, result);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _loginUseCase.ExecuteAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Renova o access token usando o refresh token. A senha não é solicitada novamente.
    /// O refresh token também é rotacionado a cada renovação.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponseDto>> Refresh(
        [FromBody] RefreshTokenRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Unauthorized();

        ClaimsPrincipal principal;
        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidAudience = _jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey)),
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var handler = new JwtSecurityTokenHandler();
            principal = handler.ValidateToken(request.RefreshToken, validationParameters, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken ||
                !string.Equals(jwtToken.Header.Alg, SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
                return Unauthorized();

            var tokenType = principal.FindFirstValue("token_type");
            if (!string.Equals(tokenType, "refresh", StringComparison.Ordinal))
                return Unauthorized();
        }
        catch (SecurityTokenException)
        {
            return Unauthorized();
        }
        catch (ArgumentException)
        {
            return Unauthorized();
        }

        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(subject, out var userId))
            return Unauthorized();

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
            return Unauthorized();

        var accessToken = _jwtTokenGenerator.GenerateToken(user);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken(user);

        return Ok(new AuthResponseDto(
            accessToken,
            refreshToken,
            user.Name,
            user.Email.Value,
            user.Id));
    }
}
