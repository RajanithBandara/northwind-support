namespace SupportDesk.Application.Contracts.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record CurrentAgentDto(int Id, string FullName, string Email);

public sealed record LoginResponseDto(
    string Token,
    DateTime ExpiresAtUtc,
    CurrentAgentDto Agent);
