using PosFiap.Application.DTOs;
using PosFiap.Application.Interfaces;
using PosFiap.Domain.Entities;
using PosFiap.Domain.Exceptions;
using PosFiap.Domain.Interfaces;
using PosFiap.Domain.ValueObjects;

namespace PosFiap.Application.UseCases.Auth;

/// <summary>
/// Caso de uso: cadastro de um novo usuário no sistema.
/// </summary>
public class RegisterUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterUseCase(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthResponseDto> ExecuteAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            throw new DomainException("A senha deve ter ao menos 6 caracteres.");

        var email = Email.Create(request.Email);

        if (await _userRepository.ExistsByEmailAsync(email, cancellationToken))
            throw new DomainException("Já existe um usuário cadastrado com este e-mail.");

        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.Create(request.Name, email, passwordHash);

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenGenerator.GenerateToken(user);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken(user);
        return new AuthResponseDto(token, refreshToken, user.Name, user.Email.Value, user.Id);
    }
}
