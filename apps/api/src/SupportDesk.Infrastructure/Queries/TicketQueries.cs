using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Configuration;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Ticket reads. Projected in the database and never tracked. Aggregates hold only each
/// other's ids, so the customer, category and agent labels are joined in here, on the read side.
/// </summary>
public sealed class TicketQueries(
    SupportDbContext db,
    IClock clock,
    IOptions<SlaOptions>? slaOptions = null) : ITicketQueries
{
    private readonly SlaOptions _slaOptions = slaOptions?.Value ?? new SlaOptions();

    /// <remarks>
    /// Filters, sorts and pages tickets in the database.
    /// </remarks>
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(TicketQuery query, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var tickets = ApplyFilters(TicketsWithLabels(), query, now, _slaOptions);

        var totalCount = await tickets.CountAsync(ct);

        var items = await ApplySort(tickets, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now,
                    _slaOptions.CalculateWindow(x.Ticket.Priority, x.Customer.Tier),
                    _slaOptions.AtRiskThresholdPercent / 100.0)
            })
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var now = clock.UtcNow;

        return TicketsWithLabels()
            .Where(x => x.Ticket.Id == id)
            .Select(x => new TicketDetailDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Description = x.Ticket.Description,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerContactDto(
                    x.Customer.Id, x.Customer.Name, x.Customer.Email, x.Customer.Phone, x.Customer.Tier),
                Category = new CategoryDto(x.Category.Id, x.Category.Name, x.Category.RequiresSpecialist),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now,
                    _slaOptions.CalculateWindow(x.Ticket.Priority, x.Customer.Tier),
                    _slaOptions.AtRiskThresholdPercent / 100.0)
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(int customerId, CancellationToken ct)
    {
        var now = clock.UtcNow;

        return await TicketsWithLabels()
            .Where(x => x.Ticket.CustomerId == customerId)
            .OrderByDescending(x => x.Ticket.CreatedAtUtc)
            .ThenByDescending(x => x.Ticket.Id)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now,
                    _slaOptions.CalculateWindow(x.Ticket.Priority, x.Customer.Tier),
                    _slaOptions.AtRiskThresholdPercent / 100.0)
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Every ticket with the rows that label it. Filter and sort this, then project.
    /// </summary>
    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        join customer in db.Set<Customer>() on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>() on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>().Where(a => a.Id == ticket.AssignedAgentId).DefaultIfEmpty()
        select new TicketWithLabels { Ticket = ticket, Customer = customer, Category = category, Agent = agent };

    private static IQueryable<TicketWithLabels> ApplyFilters(
        IQueryable<TicketWithLabels> source,
        TicketQuery query,
        DateTime now,
        SlaOptions slaOptions)
    {
        if (query.Status.HasValue)
        {
            source = source.Where(x => x.Ticket.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            source = source.Where(x => x.Ticket.Priority == query.Priority.Value);
        }

        if (query.CategoryId.HasValue)
        {
            source = source.Where(x => x.Ticket.CategoryId == query.CategoryId.Value);
        }

        if (query.CustomerId.HasValue)
        {
            source = source.Where(x => x.Ticket.CustomerId == query.CustomerId.Value);
        }

        if (query.AssignedAgentId.HasValue)
        {
            source = source.Where(x => x.Ticket.AssignedAgentId == query.AssignedAgentId.Value);
        }

        if (query.UnassignedOnly)
        {
            source = source.Where(x => x.Ticket.AssignedAgentId == null);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            source = source.Where(x =>
                x.Ticket.Title.Contains(term) ||
                x.Ticket.Reference.Contains(term) ||
                x.Customer.Name.Contains(term));
        }

        if (query.SlaStatus.HasValue)
        {
            var thresholdPercent = slaOptions.AtRiskThresholdPercent / 100.0;
            var cStd = now.Add(TimeSpan.FromTicks((long)(slaOptions.CalculateWindow(TicketPriority.Critical, CustomerTier.Standard).Ticks * thresholdPercent)));
            var cPrem = now.Add(TimeSpan.FromTicks((long)(slaOptions.CalculateWindow(TicketPriority.Critical, CustomerTier.Premium).Ticks * thresholdPercent)));
            var hStd = now.Add(TimeSpan.FromTicks((long)(slaOptions.CalculateWindow(TicketPriority.High, CustomerTier.Standard).Ticks * thresholdPercent)));
            var hPrem = now.Add(TimeSpan.FromTicks((long)(slaOptions.CalculateWindow(TicketPriority.High, CustomerTier.Premium).Ticks * thresholdPercent)));
            var mStd = now.Add(TimeSpan.FromTicks((long)(slaOptions.CalculateWindow(TicketPriority.Medium, CustomerTier.Standard).Ticks * thresholdPercent)));
            var mPrem = now.Add(TimeSpan.FromTicks((long)(slaOptions.CalculateWindow(TicketPriority.Medium, CustomerTier.Premium).Ticks * thresholdPercent)));
            var lStd = now.Add(TimeSpan.FromTicks((long)(slaOptions.CalculateWindow(TicketPriority.Low, CustomerTier.Standard).Ticks * thresholdPercent)));
            var lPrem = now.Add(TimeSpan.FromTicks((long)(slaOptions.CalculateWindow(TicketPriority.Low, CustomerTier.Premium).Ticks * thresholdPercent)));

            switch (query.SlaStatus.Value)
            {
                case SlaStatus.NotApplicable:
                    source = source.Where(x => x.Ticket.DueAtUtc == null);
                    break;

                case SlaStatus.Met:
                    source = source.Where(x =>
                        x.Ticket.DueAtUtc != null &&
                        x.Ticket.ResolvedAtUtc != null &&
                        x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc);
                    break;

                case SlaStatus.Breached:
                    source = source.Where(x =>
                        x.Ticket.DueAtUtc != null && (
                            (x.Ticket.ResolvedAtUtc != null && x.Ticket.ResolvedAtUtc > x.Ticket.DueAtUtc) ||
                            (x.Ticket.ResolvedAtUtc == null && x.Ticket.DueAtUtc < now)));
                    break;

                case SlaStatus.AtRisk:
                    source = source.Where(x =>
                        x.Ticket.DueAtUtc != null &&
                        x.Ticket.ResolvedAtUtc == null &&
                        x.Ticket.DueAtUtc >= now && (
                            (x.Ticket.Priority == TicketPriority.Critical && x.Customer.Tier == CustomerTier.Standard && x.Ticket.DueAtUtc <= cStd) ||
                            (x.Ticket.Priority == TicketPriority.Critical && x.Customer.Tier == CustomerTier.Premium && x.Ticket.DueAtUtc <= cPrem) ||
                            (x.Ticket.Priority == TicketPriority.High && x.Customer.Tier == CustomerTier.Standard && x.Ticket.DueAtUtc <= hStd) ||
                            (x.Ticket.Priority == TicketPriority.High && x.Customer.Tier == CustomerTier.Premium && x.Ticket.DueAtUtc <= hPrem) ||
                            (x.Ticket.Priority == TicketPriority.Medium && x.Customer.Tier == CustomerTier.Standard && x.Ticket.DueAtUtc <= mStd) ||
                            (x.Ticket.Priority == TicketPriority.Medium && x.Customer.Tier == CustomerTier.Premium && x.Ticket.DueAtUtc <= mPrem) ||
                            (x.Ticket.Priority == TicketPriority.Low && x.Customer.Tier == CustomerTier.Standard && x.Ticket.DueAtUtc <= lStd) ||
                            (x.Ticket.Priority == TicketPriority.Low && x.Customer.Tier == CustomerTier.Premium && x.Ticket.DueAtUtc <= lPrem)
                        ));
                    break;

                case SlaStatus.WithinSla:
                    source = source.Where(x =>
                        x.Ticket.DueAtUtc != null &&
                        x.Ticket.ResolvedAtUtc == null &&
                        x.Ticket.DueAtUtc >= now && (
                            (x.Ticket.Priority == TicketPriority.Critical && x.Customer.Tier == CustomerTier.Standard && x.Ticket.DueAtUtc > cStd) ||
                            (x.Ticket.Priority == TicketPriority.Critical && x.Customer.Tier == CustomerTier.Premium && x.Ticket.DueAtUtc > cPrem) ||
                            (x.Ticket.Priority == TicketPriority.High && x.Customer.Tier == CustomerTier.Standard && x.Ticket.DueAtUtc > hStd) ||
                            (x.Ticket.Priority == TicketPriority.High && x.Customer.Tier == CustomerTier.Premium && x.Ticket.DueAtUtc > hPrem) ||
                            (x.Ticket.Priority == TicketPriority.Medium && x.Customer.Tier == CustomerTier.Standard && x.Ticket.DueAtUtc > mStd) ||
                            (x.Ticket.Priority == TicketPriority.Medium && x.Customer.Tier == CustomerTier.Premium && x.Ticket.DueAtUtc > mPrem) ||
                            (x.Ticket.Priority == TicketPriority.Low && x.Customer.Tier == CustomerTier.Standard && x.Ticket.DueAtUtc > lStd) ||
                            (x.Ticket.Priority == TicketPriority.Low && x.Customer.Tier == CustomerTier.Premium && x.Ticket.DueAtUtc > lPrem)
                        ));
                    break;
            }
        }

        return source;
    }

    private static IQueryable<TicketWithLabels> ApplySort(IQueryable<TicketWithLabels> source, TicketQuery query)
    {
        var descending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.UpdatedAtUtc)
                : source.OrderBy(x => x.Ticket.UpdatedAtUtc),
            "dueatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.DueAtUtc)
                : source.OrderBy(x => x.Ticket.DueAtUtc),
            "priority" => descending
                ? source.OrderByDescending(x => x.Ticket.Priority)
                : source.OrderBy(x => x.Ticket.Priority),
            "status" => descending
                ? source.OrderByDescending(x => x.Ticket.Status)
                : source.OrderBy(x => x.Ticket.Status),
            _ => descending
                ? source.OrderByDescending(x => x.Ticket.CreatedAtUtc)
                : source.OrderBy(x => x.Ticket.CreatedAtUtc)
        };

        // Keeps paging stable when the sort column has duplicates.
        return sorted.ThenByDescending(x => x.Ticket.Id);
    }

    private sealed class TicketWithLabels
    {
        public required Ticket Ticket { get; init; }

        public required Customer Customer { get; init; }

        public required Category Category { get; init; }

        public Agent? Agent { get; init; }
    }
}
