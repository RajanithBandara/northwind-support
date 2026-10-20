using System.Security.Claims;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Configuration;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Common;
using SupportDesk.Infrastructure.Security;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Security;

public class JwtTokenServiceTests
{
    private readonly FixedClock _clock = new();
    private readonly JwtOptions _options = new()
    {
        SigningKey = "test-jwt-signing-key-with-at-least-32-bytes!",
        Issuer = "SupportDesk.Api",
        Audience = "SupportDesk.Web",
        ExpiryHours = 8
    };

    private JwtTokenService CreateSut() => new(Options.Create(_options), _clock);

    private Agent CreateAgent()
    {
        var agent = new Agent("Alex Turner", "alex.turner@northwind-support.example", 10, _clock.UtcNow);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(agent, 1);
        return agent;
    }

    [Fact]
    public void IssueAndValidateToken_ValidToken_ReturnsClaimsPrincipal()
    {
        var sut = CreateSut();
        var agent = CreateAgent();

        var issued = sut.IssueToken(agent);
        var principal = sut.ValidateToken(issued.Token);

        Assert.NotNull(principal);
        Assert.Equal("1", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("alex.turner@northwind-support.example", principal.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal("Alex Turner", principal.FindFirst(ClaimTypes.Name)?.Value);
        Assert.Equal(_clock.UtcNow.AddHours(8), issued.ExpiresAtUtc);
    }

    [Fact]
    public void ValidateToken_WhenExpired_ReturnsNull()
    {
        var sut = CreateSut();
        var agent = CreateAgent();

        var issued = sut.IssueToken(agent);

        // Advance clock past the 8-hour token lifetime
        _clock.Advance(TimeSpan.FromHours(8).Add(TimeSpan.FromSeconds(1)));

        var principal = sut.ValidateToken(issued.Token);

        Assert.Null(principal);
    }

    [Fact]
    public void ValidateToken_WhenTampered_ReturnsNull()
    {
        var sut = CreateSut();
        var agent = CreateAgent();

        var issued = sut.IssueToken(agent);
        var tampered = issued.Token[..^4] + "XXXX";

        Assert.Null(sut.ValidateToken(tampered));
    }
}
