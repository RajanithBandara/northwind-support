using Moq;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    private const string DefaultActor = "Alex Turner";

    private static EscalateTicketRequest Request(string reason = "Issue escalated due to customer business impact.") =>
        new(reason);

    [Fact]
    public async Task HandleAsync_WhenTicketDoesNotExist_ThrowsNotFound()
    {
        var context = new TicketCommandTestContext();
        context.Tickets.Setup(t => t.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.EscalateTicket.HandleAsync(999, Request(), DefaultActor, CancellationToken.None));

        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketIsCritical_ThrowsConflict()
    {
        var context = new TicketCommandTestContext();
        var ticket = TicketBuilder.Default
            .WithId(1)
            .WithPriority(TicketPriority.Critical)
            .Build();

        context.ExistingTicket(ticket);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => context.EscalateTicket.HandleAsync(1, Request(), DefaultActor, CancellationToken.None));

        Assert.Contains("Critical", ex.Message);
        context.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task HandleAsync_WhenTicketIsResolvedOrClosed_ThrowsConflict(TicketStatus status)
    {
        var context = new TicketCommandTestContext();
        var ticket = TicketBuilder.Default
            .WithId(1)
            .WithPriority(TicketPriority.Medium)
            .WithStatus(status)
            .Build();

        context.ExistingTicket(ticket);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => context.EscalateTicket.HandleAsync(1, Request(), DefaultActor, CancellationToken.None));

        Assert.Contains(status.ToString(), ex.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_RaisesPriorityOneLevel_AndRecordsEscalationHistoryWithTokenActor()
    {
        var context = new TicketCommandTestContext();
        var ticket = TicketBuilder.Default
            .WithId(1)
            .WithPriority(TicketPriority.Medium)
            .WithDueAt(context.Clock.UtcNow.AddHours(20))
            .Build();

        context.ExistingTicket(ticket);

        await context.EscalateTicket.HandleAsync(
            1, Request("Escalating to high due to severity."), "Sara Lindqvist", CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        context.Tickets.Verify(t => t.AddEscalationAsync(
            It.Is<TicketEscalation>(e =>
                e.TicketId == 1 &&
                e.FromPriority == TicketPriority.Medium &&
                e.ToPriority == TicketPriority.High &&
                e.EscalatedBy == "Sara Lindqvist" &&
                e.Reason == "Escalating to high due to severity."),
            It.IsAny<CancellationToken>()), Times.Once());

        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_RecomputesDueDateFromMomentOfEscalation()
    {
        var context = new TicketCommandTestContext();
        var oldDue = context.Clock.UtcNow.AddHours(10);
        var ticket = TicketBuilder.Default
            .WithId(1)
            .WithPriority(TicketPriority.Low)
            .WithDueAt(oldDue)
            .Build();

        context.ExistingTicket(ticket);

        // Low escalates to Medium. Standard customer Medium SLA base window = 24h.
        await context.EscalateTicket.HandleAsync(1, Request(), DefaultActor, CancellationToken.None);

        Assert.Equal(TicketPriority.Medium, ticket.Priority);
        Assert.Equal(context.Clock.UtcNow.AddHours(24), ticket.DueAtUtc);
    }
}
