using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

/// <summary>
/// Authenticates an active support agent by email and password and issues a JWT bearer token.
/// </summary>
public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    ILogger<LoginCommandHandler> logger)
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";

    public async Task<LoginResponseDto> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim();
        var agent = await agents.GetByEmailAsync(normalizedEmail, ct);

        if (agent is null ||
            !agent.IsActive ||
            string.IsNullOrWhiteSpace(agent.PasswordHash) ||
            !passwordHasher.VerifyPassword(request.Password, agent.PasswordHash))
        {
            logger.LogInformation("Rejected login attempt for {Email}.", normalizedEmail);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        var issued = jwtTokenService.IssueToken(agent);

        logger.LogInformation("Agent {AgentId} ({Email}) signed in.", agent.Id, agent.Email);

        return new LoginResponseDto(
            issued.Token,
            issued.ExpiresAtUtc,
            new CurrentAgentDto(agent.Id, agent.FullName, agent.Email));
    }
}
