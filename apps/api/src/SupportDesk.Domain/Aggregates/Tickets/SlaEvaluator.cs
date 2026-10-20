namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
public static class SlaEvaluator
{
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        TimeSpan? window = null,
        double atRiskThresholdPercent = 0.25)
    {
        if (dueAtUtc is null)
        {
            return SlaStatus.NotApplicable;
        }

        if (resolvedAtUtc is not null)
        {
            return resolvedAtUtc <= dueAtUtc ? SlaStatus.Met : SlaStatus.Breached;
        }

        if (nowUtc > dueAtUtc)
        {
            return SlaStatus.Breached;
        }

        if (window.HasValue && window.Value > TimeSpan.Zero)
        {
            var remaining = dueAtUtc.Value - nowUtc;
            var thresholdTicks = (long)(window.Value.Ticks * atRiskThresholdPercent);
            var threshold = TimeSpan.FromTicks(thresholdTicks);
            if (remaining <= threshold)
            {
                return SlaStatus.AtRisk;
            }
        }

        return SlaStatus.WithinSla;
    }

    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        DateTime createdAtUtc,
        double atRiskThresholdPercent = 0.25)
    {
        var window = dueAtUtc.HasValue && dueAtUtc.Value > createdAtUtc
            ? dueAtUtc.Value - createdAtUtc
            : (TimeSpan?)null;

        return Evaluate(dueAtUtc, resolvedAtUtc, nowUtc, window, atRiskThresholdPercent);
    }
}
