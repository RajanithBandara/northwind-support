namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Explains the automatic triage decisions made when a ticket was created.
/// </summary>
public sealed record TicketTriageDto(
    string PriorityReason,
    string SlaReason,
    string AssignmentReason);
