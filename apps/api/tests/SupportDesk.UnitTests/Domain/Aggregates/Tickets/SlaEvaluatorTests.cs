using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now));

    [Fact]
    public void Evaluate_ResolvedOnTime_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now.AddMinutes(1), Now));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddMinutes(-1), null, Now));

    [Fact]
    public void Evaluate_OpenAndBeforeDue_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddHours(1), null, Now));

    [Fact]
    public void Evaluate_UnresolvedWithExactly25PercentWindowRemaining_IsAtRisk()
    {
        // Window = 4 hours (240 mins). 25% threshold = 1 hour (60 mins).
        // Due date = Now + 1 hour.
        var due = Now.AddHours(1);
        var window = TimeSpan.FromHours(4);

        var status = SlaEvaluator.Evaluate(due, null, Now, window, atRiskThresholdPercent: 0.25);

        Assert.Equal(SlaStatus.AtRisk, status);
    }

    [Fact]
    public void Evaluate_UnresolvedWithLessThan25PercentWindowRemaining_IsAtRisk()
    {
        // Window = 4 hours. Remaining = 59 minutes (< 60 minutes).
        var due = Now.AddMinutes(59);
        var window = TimeSpan.FromHours(4);

        var status = SlaEvaluator.Evaluate(due, null, Now, window, atRiskThresholdPercent: 0.25);

        Assert.Equal(SlaStatus.AtRisk, status);
    }

    [Fact]
    public void Evaluate_UnresolvedWithMoreThan25PercentWindowRemaining_IsWithinSla()
    {
        // Window = 4 hours. Remaining = 61 minutes (> 60 minutes).
        var due = Now.AddMinutes(61);
        var window = TimeSpan.FromHours(4);

        var status = SlaEvaluator.Evaluate(due, null, Now, window, atRiskThresholdPercent: 0.25);

        Assert.Equal(SlaStatus.WithinSla, status);
    }

    [Fact]
    public void Evaluate_CalculatedFromCreatedAt_EvaluatesAtRiskCorrectly()
    {
        var createdAt = Now.AddHours(-3);
        var dueAt = Now.AddHours(1); // 4 hour window, 1 hour remaining = exactly 25%

        var status = SlaEvaluator.Evaluate(dueAt, null, Now, createdAt, atRiskThresholdPercent: 0.25);

        Assert.Equal(SlaStatus.AtRisk, status);
    }
}
