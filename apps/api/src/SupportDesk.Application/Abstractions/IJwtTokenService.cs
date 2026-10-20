using System.Security.Claims;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Application.Abstractions;

public sealed record IssuedToken(string Token, DateTime ExpiresAtUtc);

/// <summary>
/// Issues and validates signed JWT tokens for support agents.
/// </summary>
public interface IJwtTokenService
{
    IssuedToken IssueToken(Agent agent);

    ClaimsPrincipal? ValidateToken(string token);
}
