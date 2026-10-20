using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Queries.GetCurrentAgent;

/// <summary>
/// Resolves the currently authenticated support agent from their token subject claim.
/// </summary>
public sealed class GetCurrentAgentQueryHandler(IAgentRepository agents)
{
    public async Task<CurrentAgentDto> HandleAsync(int agentId, CancellationToken ct)
    {
        var agent = await agents.GetByIdAsync(agentId, ct);

        if (agent is null || !agent.IsActive)
        {
            throw new UnauthorizedException("Authentication token is invalid or agent is no longer active.");
        }

        return new CurrentAgentDto(agent.Id, agent.FullName, agent.Email);
    }
}
