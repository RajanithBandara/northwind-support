using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SupportDesk.Application.Configuration;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Common;
using SupportDesk.Infrastructure.Security;
using SupportDesk.Presentation.Authentication;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Presentation.Authentication;

public class JwtAuthenticationHandlerTests
{
    private readonly FixedClock _clock = new();
    private readonly JwtTokenService _tokenService;

    public JwtAuthenticationHandlerTests()
    {
        var jwtOptions = Options.Create(new JwtOptions
        {
            SigningKey = "test-jwt-signing-key-with-at-least-32-bytes!",
            Issuer = "SupportDesk.Api",
            Audience = "SupportDesk.Web",
            ExpiryHours = 8
        });
        _tokenService = new JwtTokenService(jwtOptions, _clock);
    }

    private async Task<JwtAuthenticationHandler> CreateHandlerAsync(HttpContext context)
    {
        var optionsMonitor = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        optionsMonitor
            .Setup(x => x.Get(It.IsAny<string>()))
            .Returns(new AuthenticationSchemeOptions());

        var handler = new JwtAuthenticationHandler(
            optionsMonitor.Object,
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            _tokenService);

        var scheme = new AuthenticationScheme(
            JwtAuthenticationHandler.SchemeName,
            JwtAuthenticationHandler.SchemeName,
            typeof(JwtAuthenticationHandler));

        await handler.InitializeAsync(scheme, context);
        return handler;
    }

    [Fact]
    public async Task AuthenticateAsync_WithoutToken_FailsAndChallengeWrites401()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = await CreateHandlerAsync(context);

        var result = await handler.AuthenticateAsync();
        Assert.False(result.Succeeded);

        await handler.ChallengeAsync(null);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidBearerToken_SucceedsWithAgentClaims()
    {
        var agent = new Agent("Sara Lindqvist", "sara.lindqvist@northwind-support.example", 6, _clock.UtcNow);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(agent, 4);
        var issued = _tokenService.IssueToken(agent);

        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {issued.Token}";
        var handler = await CreateHandlerAsync(context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal("Sara Lindqvist", result.Principal?.Identity?.Name);
    }
}
