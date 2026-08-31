namespace PosFiap.Application.DTOs;

public record RegisterRequestDto(string Name, string Email, string Password);
public record LoginRequestDto(string Email, string Password);
public record AuthResponseDto(string Token, string Name, string Email, Guid UserId);
