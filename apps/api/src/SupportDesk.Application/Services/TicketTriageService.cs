using Microsoft.Extensions.Options;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Configuration;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Services;

public sealed class TicketTriageService(
    IAgentQueries agentQueries,
    IOptions<SlaOptions> slaOptions)
{
    private readonly SlaOptions _options = slaOptions.Value;

    public async Task<TriageResult> TriageOnCreateAsync(
        Customer customer,
        Category category,
        TicketPriority? requestedPriority,
        DateTime nowUtc,
        CancellationToken ct)
    {
        // BR-1: Priority determination
        var (priority, priorityReason) = DeterminePriority(category, requestedPriority);

        // BR-2, BR-3, BR-6: SLA window & due date
        var (dueAtUtc, window, slaReason) = CalculateSla(priority, customer.Tier, nowUtc);

        // BR-4, BR-5: Agent assignment
        var agents = await agentQueries.GetAllAsync(ct);
        var (assignedAgentId, assignmentReason) = SelectAgent(agents, category);

        return new TriageResult(
            priority,
            dueAtUtc,
            window,
            assignedAgentId,
            new TicketTriageDto(priorityReason, slaReason, assignmentReason));
    }

    public async Task<EscalationTriageResult> TriageOnEscalateAsync(
        Ticket ticket,
        Customer customer,
        Category category,
        DateTime nowUtc,
        CancellationToken ct)
    {
        // BR-7: One-step increase
        var newPriority = Ticket.NextPriority(ticket.Priority);

        // Recompute due date from the moment of escalation
        var (newDueAtUtc, _, _) = CalculateSla(newPriority, customer.Tier, nowUtc);

        // Re-evaluate owner: keep current agent if still eligible, else reassign per BR-5
        var agents = await agentQueries.GetAllAsync(ct);
        int? newAgentId;

        if (ticket.AssignedAgentId is { } currentAgentId)
        {
            var currentAgent = agents.FirstOrDefault(a => a.Id == currentAgentId);
            var isStillEligible = currentAgent is not null &&
                                  currentAgent.IsActive &&
                                  (!category.RequiresSpecialist || currentAgent.Specializations.Any(s => s.Id == category.Id)) &&
                                  currentAgent.OpenTicketCount <= currentAgent.MaxOpenTickets;

            if (isStillEligible)
            {
                newAgentId = currentAgentId;
            }
            else
            {
                var (reassignedId, _) = SelectAgent(agents, category);
                newAgentId = reassignedId;
            }
        }
        else
        {
            var (reassignedId, _) = SelectAgent(agents, category);
            newAgentId = reassignedId;
        }

        return new EscalationTriageResult(newPriority, newDueAtUtc, newAgentId);
    }

    private static (TicketPriority Priority, string Reason) DeterminePriority(
        Category category,
        TicketPriority? requestedPriority)
    {
        if (category.ForcesCriticalPriority)
        {
            return (TicketPriority.Critical, $"Category '{category.Name}' forces critical priority.");
        }

        if (requestedPriority.HasValue)
        {
            return (requestedPriority.Value, $"Requested priority '{requestedPriority.Value}' applied.");
        }

        return (TicketPriority.Medium, "Defaulted to Medium priority.");
    }

    private (DateTime DueAtUtc, TimeSpan Window, string Reason) CalculateSla(
        TicketPriority priority,
        CustomerTier tier,
        DateTime nowUtc)
    {
        var baseWindow = _options.GetBaseWindow(priority);
        var window = _options.CalculateWindow(priority, tier);
        var dueAtUtc = nowUtc.Add(window);

        string reason;
        if (tier == CustomerTier.Premium)
        {
            reason = $"{priority} base window is {baseWindow.TotalHours:0.#}h, reduced to {window.TotalHours:0.#}h for Premium customer.";
        }
        else
        {
            reason = $"{priority} base window is {window.TotalHours:0.#}h.";
        }

        return (dueAtUtc, window, reason);
    }

    private static (int? AgentId, string Reason) SelectAgent(
        IEnumerable<Contracts.Agents.AgentDto> allAgents,
        Category category)
    {
        var eligible = allAgents
            .Where(a => a.IsActive)
            .Where(a => !category.RequiresSpecialist || a.Specializations.Any(s => s.Id == category.Id))
            .Where(a => a.OpenTicketCount < a.MaxOpenTickets)
            .OrderBy(a => a.OpenTicketCount)
            .ThenBy(a => a.Id)
            .ToList();

        if (eligible.Count > 0)
        {
            var best = eligible[0];
            return (best.Id, $"Assigned to {best.FullName} with fewest open tickets ({best.OpenTicketCount}/{best.MaxOpenTickets}).");
        }

        if (category.RequiresSpecialist)
        {
            return (null, $"Unassigned because no active specialist for '{category.Name}' is currently under their ticket limit.");
        }

        return (null, "Unassigned because no active agent is currently under their ticket limit.");
    }
}

public sealed record TriageResult(
    TicketPriority Priority,
    DateTime DueAtUtc,
    TimeSpan Window,
    int? AssignedAgentId,
    TicketTriageDto TriageDto);

public sealed record EscalationTriageResult(
    TicketPriority NewPriority,
    DateTime NewDueAtUtc,
    int? NewAgentId);
