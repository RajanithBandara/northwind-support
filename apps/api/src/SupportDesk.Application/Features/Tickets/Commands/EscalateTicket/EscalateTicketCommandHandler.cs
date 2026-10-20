using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    IClock clock,
    TicketTriageService triageService,
    GetTicketQueryHandler getTicket,
    ILogger<EscalateTicketCommandHandler> logger)
{
    public async Task<TicketDetailDto> HandleAsync(
        int id,
        EscalateTicketRequest request,
        string escalatedBy,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(escalatedBy))
        {
            throw new UnauthorizedException("Authenticated agent identity is required to escalate a ticket.");
        }

        var ticket = await tickets.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Ticket", id);

        if (ticket.Priority == TicketPriority.Critical)
        {
            throw new ConflictException("Ticket is already at Critical priority and cannot be escalated.");
        }

        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            throw new ConflictException($"Ticket is {ticket.Status} and cannot be escalated.");
        }

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var now = clock.UtcNow;

        var triage = await triageService.TriageOnEscalateAsync(ticket, customer, category, now, ct);

        var fromPriority = ticket.Priority;
        var fromAgentId = ticket.AssignedAgentId;
        var fromDueAtUtc = ticket.DueAtUtc;

        // Apply escalation on ticket aggregate (enforces state and priority business rules)
        ticket.Escalate(triage.NewPriority, triage.NewDueAtUtc, triage.NewAgentId, now);

        var escalation = new TicketEscalation(
            ticket.Id,
            fromPriority,
            triage.NewPriority,
            fromAgentId,
            triage.NewAgentId,
            fromDueAtUtc,
            triage.NewDueAtUtc,
            request.Reason,
            escalatedBy,
            now);

        await tickets.AddEscalationAsync(escalation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated from {FromPriority} to {ToPriority} by {EscalatedBy}.",
            ticket.Reference, fromPriority, triage.NewPriority, escalatedBy);

        return await getTicket.HandleAsync(id, ct);
    }
}
