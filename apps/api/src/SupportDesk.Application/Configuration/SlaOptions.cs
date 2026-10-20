using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Configuration;

/// <summary>
/// SLA configuration loaded from appsettings.
/// </summary>
public sealed class SlaOptions
{
    public const string SectionName = "Sla";

    public Dictionary<string, int> BaseWindowHours { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Low"] = 72,
        ["Medium"] = 24,
        ["High"] = 8,
        ["Critical"] = 4
    };

    public double PremiumCustomerMultiplier { get; set; } = 0.5;

    public int MinimumWindowHours { get; set; } = 1;

    public int AtRiskThresholdPercent { get; set; } = 25;

    public TimeSpan GetBaseWindow(TicketPriority priority)
    {
        var key = priority.ToString();
        if (BaseWindowHours.TryGetValue(key, out var hours))
        {
            return TimeSpan.FromHours(hours);
        }

        return priority switch
        {
            TicketPriority.Critical => TimeSpan.FromHours(4),
            TicketPriority.High => TimeSpan.FromHours(8),
            TicketPriority.Medium => TimeSpan.FromHours(24),
            _ => TimeSpan.FromHours(72)
        };
    }

    public TimeSpan CalculateWindow(TicketPriority priority, CustomerTier customerTier)
    {
        var baseWindow = GetBaseWindow(priority);
        if (customerTier == CustomerTier.Premium)
        {
            var reducedHours = baseWindow.TotalHours * PremiumCustomerMultiplier;
            var finalHours = Math.Max(reducedHours, MinimumWindowHours);
            return TimeSpan.FromHours(finalHours);
        }

        return baseWindow;
    }
}
