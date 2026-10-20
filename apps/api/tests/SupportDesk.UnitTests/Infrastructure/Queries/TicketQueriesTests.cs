using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SupportDbContext _db;
    private readonly FixedClock _clock = new();
    private readonly TicketQueries _sut;

    private readonly Customer _customer1;
    private readonly Customer _customer2;
    private readonly Category _category1;
    private readonly Category _category2;
    private readonly Agent _agent1;

    public TicketQueriesTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new SupportDbContext(options);
        _db.Database.EnsureCreated();

        _customer1 = new Customer("Contoso Ltd", "contact@contoso.example", null, CustomerTier.Premium, _clock.UtcNow);
        _customer2 = new Customer("Fabrikam Inc", "info@fabrikam.example", null, CustomerTier.Standard, _clock.UtcNow);
        _category1 = new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
        _category2 = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        _agent1 = new Agent("Alex Turner", "alex@example.com", maxOpenTickets: 5, _clock.UtcNow);

        _db.AddRange(_customer1, _customer2, _category1, _category2, _agent1);
        _db.SaveChanges();

        _sut = new TicketQueries(_db, _clock);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByExactPriority()
    {
        // Arrange
        SeedTickets(
            CreateTicket("TCK-0001", "High priority ticket", _customer1.Id, _category1.Id, TicketPriority.High),
            CreateTicket("TCK-0002", "Low priority ticket", _customer1.Id, _category1.Id, TicketPriority.Low));

        var query = new TicketQuery { Priority = TicketPriority.High };

        // Act
        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("TCK-0001", result.Items[0].Reference);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_SearchesCustomerName()
    {
        // Arrange
        SeedTickets(
            CreateTicket("TCK-0001", "Order issue", _customer1.Id, _category1.Id, TicketPriority.Medium),
            CreateTicket("TCK-0002", "Login problem", _customer2.Id, _category1.Id, TicketPriority.Medium));

        var query = new TicketQuery { Search = "Fabrikam" };

        // Act
        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("TCK-0002", result.Items[0].Reference);
        Assert.Equal("Fabrikam Inc", result.Items[0].Customer.Name);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_SearchesTicketTitleAndReference()
    {
        // Arrange
        SeedTickets(
            CreateTicket("TCK-0042", "Printer jammed", _customer1.Id, _category1.Id, TicketPriority.Low),
            CreateTicket("TCK-0043", "Network outage", _customer1.Id, _category1.Id, TicketPriority.Critical));

        var byRef = await _sut.GetPagedAsync(new TicketQuery { Search = "0042" }, CancellationToken.None);
        var byTitle = await _sut.GetPagedAsync(new TicketQuery { Search = "Printer" }, CancellationToken.None);

        Assert.Single(byRef.Items);
        Assert.Equal("TCK-0042", byRef.Items[0].Reference);

        Assert.Single(byTitle.Items);
        Assert.Equal("TCK-0042", byTitle.Items[0].Reference);
    }

    [Fact]
    public async Task GetPagedAsync_CombinesMultipleFiltersWithAnd()
    {
        // Arrange
        var t1 = CreateTicket("TCK-0001", "Billing issue 1", _customer1.Id, _category1.Id, TicketPriority.High);
        var t2 = CreateTicket("TCK-0002", "Billing issue 2", _customer2.Id, _category1.Id, TicketPriority.High);
        var t3 = CreateTicket("TCK-0003", "General issue", _customer1.Id, _category2.Id, TicketPriority.High);
        SeedTickets(t1, t2, t3);

        var query = new TicketQuery
        {
            Priority = TicketPriority.High,
            CustomerId = _customer1.Id,
            CategoryId = _category1.Id
        };

        // Act
        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("TCK-0001", result.Items[0].Reference);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersUnassignedOnly()
    {
        // Arrange
        var assigned = CreateTicket("TCK-0001", "Assigned ticket", _customer1.Id, _category1.Id, TicketPriority.Medium);
        assigned.AssignTo(_agent1.Id, _clock.UtcNow);

        var unassigned = CreateTicket("TCK-0002", "Unassigned ticket", _customer1.Id, _category1.Id, TicketPriority.Medium);

        SeedTickets(assigned, unassigned);

        var query = new TicketQuery { UnassignedOnly = true };

        // Act
        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("TCK-0002", result.Items[0].Reference);
        Assert.Null(result.Items[0].AssignedAgent);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByExactStatusAndCalculatesPagingCorrectly()
    {
        // Arrange
        var openTicket = CreateTicket("TCK-0001", "Open ticket", _customer1.Id, _category1.Id, TicketPriority.Medium);
        openTicket.ChangeStatus(TicketStatus.Open, _clock.UtcNow);

        var resolvedTicket = CreateTicket("TCK-0002", "Resolved ticket", _customer1.Id, _category1.Id, TicketPriority.Medium);
        resolvedTicket.ChangeStatus(TicketStatus.Resolved, _clock.UtcNow);

        SeedTickets(openTicket, resolvedTicket);

        var query = new TicketQuery { Status = TicketStatus.Open, Page = 1, PageSize = 10 };

        // Act
        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(TicketStatus.Open, result.Items[0].Status);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersBySlaStatus_NotApplicable()
    {
        // Arrange: one with null due date, one with a due date
        var noDue = CreateTicketWithDue("TCK-0010", "No due date", _customer2.Id, _category1.Id, TicketPriority.Medium, null);
        var withDue = CreateTicketWithDue("TCK-0011", "Has due date", _customer2.Id, _category1.Id, TicketPriority.Medium, _clock.UtcNow.AddHours(10));
        SeedTickets(noDue, withDue);

        var query = new TicketQuery { SlaStatus = SlaStatus.NotApplicable };

        // Act
        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("TCK-0010", result.Items[0].Reference);
        Assert.Equal(SlaStatus.NotApplicable, result.Items[0].SlaStatus);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersBySlaStatus_Met()
    {
        // Arrange: one resolved on time, one resolved late, one unresolved
        var met = CreateTicketWithDue("TCK-0020", "Met ticket", _customer2.Id, _category1.Id, TicketPriority.Medium, _clock.UtcNow.AddHours(2));
        met.ChangeStatus(TicketStatus.Resolved, _clock.UtcNow); // resolved at Now, due in 2 hours

        var breachedLate = CreateTicketWithDue("TCK-0021", "Breached late", _customer2.Id, _category1.Id, TicketPriority.Medium, _clock.UtcNow.AddHours(-2));
        breachedLate.ChangeStatus(TicketStatus.Resolved, _clock.UtcNow); // resolved at Now, due was 2 hours ago

        SeedTickets(met, breachedLate);

        var query = new TicketQuery { SlaStatus = SlaStatus.Met };

        // Act
        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("TCK-0020", result.Items[0].Reference);
        Assert.Equal(SlaStatus.Met, result.Items[0].SlaStatus);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersBySlaStatus_Breached()
    {
        // Arrange: one unresolved past due, one resolved on time
        var breached = CreateTicketWithDue("TCK-0030", "Breached ticket", _customer2.Id, _category1.Id, TicketPriority.Medium, _clock.UtcNow.AddHours(-1));
        var onTime = CreateTicketWithDue("TCK-0031", "On time ticket", _customer2.Id, _category1.Id, TicketPriority.Medium, _clock.UtcNow.AddHours(5));

        SeedTickets(breached, onTime);

        var query = new TicketQuery { SlaStatus = SlaStatus.Breached };

        // Act
        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("TCK-0030", result.Items[0].Reference);
        Assert.Equal(SlaStatus.Breached, result.Items[0].SlaStatus);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersBySlaStatus_AtRiskAndWithinSla()
    {
        // Standard customer: Critical window = 4h. 25% threshold = 1h.
        // AtRisk: due in 30 mins (<= 1h)
        var atRisk = CreateTicketWithDue("TCK-0040", "At risk ticket", _customer2.Id, _category1.Id, TicketPriority.Critical, _clock.UtcNow.AddMinutes(30));
        // WithinSla: due in 3h (> 1h)
        var withinSla = CreateTicketWithDue("TCK-0041", "Within SLA ticket", _customer2.Id, _category1.Id, TicketPriority.Critical, _clock.UtcNow.AddHours(3));

        SeedTickets(atRisk, withinSla);

        var atRiskResult = await _sut.GetPagedAsync(new TicketQuery { SlaStatus = SlaStatus.AtRisk }, CancellationToken.None);
        var withinResult = await _sut.GetPagedAsync(new TicketQuery { SlaStatus = SlaStatus.WithinSla }, CancellationToken.None);

        Assert.Single(atRiskResult.Items);
        Assert.Equal("TCK-0040", atRiskResult.Items[0].Reference);
        Assert.Equal(SlaStatus.AtRisk, atRiskResult.Items[0].SlaStatus);

        Assert.Single(withinResult.Items);
        Assert.Equal("TCK-0041", withinResult.Items[0].Reference);
        Assert.Equal(SlaStatus.WithinSla, withinResult.Items[0].SlaStatus);
    }

    private Ticket CreateTicket(string reference, string title, int customerId, int categoryId, TicketPriority priority) =>
        Ticket.Raise(reference, title, "Standard test description.", customerId, categoryId, priority, _clock.UtcNow);

    private Ticket CreateTicketWithDue(string reference, string title, int customerId, int categoryId, TicketPriority priority, DateTime? dueAtUtc) =>
        Ticket.Raise(reference, title, "Standard test description.", customerId, categoryId, priority, _clock.UtcNow, dueAtUtc);

    private void SeedTickets(params Ticket[] tickets)
    {
        _db.AddRange(tickets);
        _db.SaveChanges();
    }
}
