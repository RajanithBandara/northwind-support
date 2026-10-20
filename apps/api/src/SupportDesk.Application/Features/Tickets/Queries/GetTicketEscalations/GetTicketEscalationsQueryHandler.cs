using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;

public sealed class GetTicketEscalationsQueryHandler(
    ITicketRepository tickets,
    IAgentQueries agentQueries)
{
    public async Task<IReadOnlyList<TicketEscalationDto>> HandleAsync(int ticketId, CancellationToken ct)
    {
        var escalations = await tickets.GetEscalationsAsync(ticketId, ct);
        if (escalations.Count == 0)
        {
            return [];
        }

        var allAgents = await agentQueries.GetAllAsync(ct);
        var agentsMap = allAgents.ToDictionary(a => a.Id, a => new AgentSummaryDto(a.Id, a.FullName));

        return escalations.Select(e => new TicketEscalationDto(
            e.Id,
            e.TicketId,
            e.FromPriority,
            e.ToPriority,
            e.FromAgentId.HasValue && agentsMap.TryGetValue(e.FromAgentId.Value, out var fromAgent) ? fromAgent : null,
            e.ToAgentId.HasValue && agentsMap.TryGetValue(e.ToAgentId.Value, out var toAgent) ? toAgent : null,
            e.FromDueAtUtc,
            e.ToDueAtUtc,
            e.Reason,
            e.EscalatedBy,
            e.EscalatedAtUtc))
            .ToList();
    }
}
