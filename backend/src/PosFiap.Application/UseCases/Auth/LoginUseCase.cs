using PosFiap.Application.DTOs;
using PosFiap.Application.Interfaces;
using PosFiap.Domain.Exceptions;
using PosFiap.Domain.Interfaces;
using PosFiap.Domain.ValueObjects;

namespace PosFiap.Application.UseCases.Auth;

/// <summary>
/// Caso de uso: autenticação de um usuário existente (tela de login).
/// </summary>
public class LoginUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public LoginUseCase(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<AuthResponseDto> ExecuteAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var email = Email.Create(request.Email);
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null || !user.IsActive || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new DomainException("E-mail ou senha inválidos.");

        var token = _jwtTokenGenerator.GenerateToken(user);
        return new AuthResponseDto(token, user.Name, user.Email.Value, user.Id);
    }
}
