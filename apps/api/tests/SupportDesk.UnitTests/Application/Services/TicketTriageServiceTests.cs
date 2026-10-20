using Microsoft.Extensions.Options;
using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Configuration;
using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Services;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Common;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Services;

public class TicketTriageServiceTests
{
    private readonly Mock<IAgentQueries> _agentQueries = new();
    private readonly SlaOptions _slaOptions = new();
    private readonly DateTime _now = FixedClock.DefaultNow;

    private static Category CreateCategory(string name, bool requiresSpecialist, bool forcesCriticalPriority, int id = 0)
    {
        var category = new Category(name, requiresSpecialist, forcesCriticalPriority);
        if (id != 0)
        {
            typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(category, id);
        }
        return category;
    }

    private TicketTriageService CreateSut() =>
        new(_agentQueries.Object, Options.Create(_slaOptions));

    [Theory]
    [InlineData(TicketPriority.Critical, 4)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.Low, 72)]
    public async Task TriageOnCreate_CalculatesSlaWindow_ForStandardCustomer(TicketPriority priority, int expectedHours)
    {
        var sut = CreateSut();
        var customer = new Customer("Standard Corp", "std@example.com", null, CustomerTier.Standard, _now);
        var category = CreateCategory("General", requiresSpecialist: false, forcesCriticalPriority: false);
        _agentQueries.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await sut.TriageOnCreateAsync(customer, category, priority, _now, CancellationToken.None);

        Assert.Equal(priority, result.Priority);
        Assert.Equal(_now.AddHours(expectedHours), result.DueAtUtc);
        Assert.Equal(TimeSpan.FromHours(expectedHours), result.Window);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, 2)]
    [InlineData(TicketPriority.High, 4)]
    [InlineData(TicketPriority.Medium, 12)]
    [InlineData(TicketPriority.Low, 36)]
    public async Task TriageOnCreate_CalculatesHalfSlaWindow_ForPremiumCustomer(TicketPriority priority, int expectedHours)
    {
        var sut = CreateSut();
        var customer = new Customer("Premium Corp", "prem@example.com", null, CustomerTier.Premium, _now);
        var category = CreateCategory("General", requiresSpecialist: false, forcesCriticalPriority: false);
        _agentQueries.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await sut.TriageOnCreateAsync(customer, category, priority, _now, CancellationToken.None);

        Assert.Equal(_now.AddHours(expectedHours), result.DueAtUtc);
        Assert.Equal(TimeSpan.FromHours(expectedHours), result.Window);
    }

    [Fact]
    public async Task TriageOnCreate_RespectsOneHourFloor_ForPremiumCustomer()
    {
        _slaOptions.BaseWindowHours["Critical"] = 1; // 0.5 * 1h = 30 mins, should floor to 1h
        var sut = CreateSut();
        var customer = new Customer("Premium Corp", "prem@example.com", null, CustomerTier.Premium, _now);
        var category = CreateCategory("General", requiresSpecialist: false, forcesCriticalPriority: false);
        _agentQueries.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await sut.TriageOnCreateAsync(customer, category, TicketPriority.Critical, _now, CancellationToken.None);

        Assert.Equal(TimeSpan.FromHours(1), result.Window);
        Assert.Equal(_now.AddHours(1), result.DueAtUtc);
    }

    [Fact]
    public async Task TriageOnCreate_ForcesCriticalPriority_WhenCategoryConfigured()
    {
        var sut = CreateSut();
        var customer = new Customer("Standard Corp", "std@example.com", null, CustomerTier.Standard, _now);
        var outageCategory = CreateCategory("Outage", requiresSpecialist: false, forcesCriticalPriority: true);
        _agentQueries.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        // Requested as Low, but category forces Critical
        var result = await sut.TriageOnCreateAsync(customer, outageCategory, TicketPriority.Low, _now, CancellationToken.None);

        Assert.Equal(TicketPriority.Critical, result.Priority);
        Assert.Contains("forces critical priority", result.TriageDto.PriorityReason);
    }

    [Fact]
    public async Task TriageOnCreate_DefaultsToMedium_WhenNoPriorityRequested()
    {
        var sut = CreateSut();
        var customer = new Customer("Standard Corp", "std@example.com", null, CustomerTier.Standard, _now);
        var category = CreateCategory("General", requiresSpecialist: false, forcesCriticalPriority: false);
        _agentQueries.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await sut.TriageOnCreateAsync(customer, category, null, _now, CancellationToken.None);

        Assert.Equal(TicketPriority.Medium, result.Priority);
    }

    [Fact]
    public async Task TriageOnCreate_AssignsAgentWithFewestOpenTickets()
    {
        var sut = CreateSut();
        var customer = new Customer("Standard Corp", "std@example.com", null, CustomerTier.Standard, _now);
        var category = CreateCategory("General", requiresSpecialist: false, forcesCriticalPriority: false);

        var agent1 = new AgentDto(1, "Agent Busy", "busy@example.com", IsActive: true, MaxOpenTickets: 5, OpenTicketCount: 4, []);
        var agent2 = new AgentDto(2, "Agent Free", "free@example.com", IsActive: true, MaxOpenTickets: 5, OpenTicketCount: 1, []);
        _agentQueries.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([agent1, agent2]);

        var result = await sut.TriageOnCreateAsync(customer, category, TicketPriority.Medium, _now, CancellationToken.None);

        Assert.Equal(2, result.AssignedAgentId);
        Assert.Contains("Agent Free", result.TriageDto.AssignmentReason);
    }

    [Fact]
    public async Task TriageOnCreate_DeterministicTieBreak_ChoosesLowestId()
    {
        var sut = CreateSut();
        var customer = new Customer("Standard Corp", "std@example.com", null, CustomerTier.Standard, _now);
        var category = CreateCategory("General", requiresSpecialist: false, forcesCriticalPriority: false);

        var agentA = new AgentDto(10, "Agent Ten", "ten@example.com", IsActive: true, MaxOpenTickets: 5, OpenTicketCount: 2, []);
        var agentB = new AgentDto(5, "Agent Five", "five@example.com", IsActive: true, MaxOpenTickets: 5, OpenTicketCount: 2, []);
        _agentQueries.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([agentA, agentB]);

        var result = await sut.TriageOnCreateAsync(customer, category, TicketPriority.Medium, _now, CancellationToken.None);

        Assert.Equal(5, result.AssignedAgentId);
    }

    [Fact]
    public async Task TriageOnCreate_RequiresSpecialist_OnlySelectsSpecializedAgent()
    {
        var sut = CreateSut();
        var customer = new Customer("Standard Corp", "std@example.com", null, CustomerTier.Standard, _now);
        var billingCategory = CreateCategory("Billing", requiresSpecialist: true, forcesCriticalPriority: false, id: 42);

        var generalAgent = new AgentDto(1, "General Agent", "gen@example.com", IsActive: true, MaxOpenTickets: 10, OpenTicketCount: 0, []);
        var billingAgent = new AgentDto(2, "Billing Specialist", "bill@example.com", IsActive: true, MaxOpenTickets: 10, OpenTicketCount: 2,
            [new CategorySummaryDto(42, "Billing")]);

        _agentQueries.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([generalAgent, billingAgent]);

        var result = await sut.TriageOnCreateAsync(customer, billingCategory, TicketPriority.Medium, _now, CancellationToken.None);

        Assert.Equal(2, result.AssignedAgentId);
    }

    [Fact]
    public async Task TriageOnCreate_IgnoresInactiveAndAtCapacityAgents()
    {
        var sut = CreateSut();
        var customer = new Customer("Standard Corp", "std@example.com", null, CustomerTier.Standard, _now);
        var category = CreateCategory("General", requiresSpecialist: false, forcesCriticalPriority: false);

        var inactiveAgent = new AgentDto(1, "Inactive Agent", "in@example.com", IsActive: false, MaxOpenTickets: 5, OpenTicketCount: 0, []);
        var fullAgent = new AgentDto(2, "Full Agent", "full@example.com", IsActive: true, MaxOpenTickets: 5, OpenTicketCount: 5, []);

        _agentQueries.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([inactiveAgent, fullAgent]);

        var result = await sut.TriageOnCreateAsync(customer, category, TicketPriority.Medium, _now, CancellationToken.None);

        Assert.Null(result.AssignedAgentId);
        Assert.Contains("Unassigned", result.TriageDto.AssignmentReason);
    }
}
