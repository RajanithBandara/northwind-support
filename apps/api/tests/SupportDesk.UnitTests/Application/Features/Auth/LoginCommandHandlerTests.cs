using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SupportDesk.Application.Configuration;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Common;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Security;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class LoginCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _agents = new();
    private readonly Pbkdf2PasswordHasher _passwordHasher = new();
    private readonly FixedClock _clock = new();
    private readonly LoginCommandHandler _sut;

    public LoginCommandHandlerTests()
    {
        var jwtOptions = Options.Create(new JwtOptions
        {
            SigningKey = "test-jwt-signing-key-with-at-least-32-bytes!",
            Issuer = "SupportDesk.Api",
            Audience = "SupportDesk.Web",
            ExpiryHours = 8
        });
        var jwtService = new JwtTokenService(jwtOptions, _clock);

        _sut = new LoginCommandHandler(
            _agents.Object,
            _passwordHasher,
            jwtService,
            NullLogger<LoginCommandHandler>.Instance);
    }

    private Agent CreateAgent(int id, string email, string password, bool isActive = true)
    {
        var agent = new Agent("Alex Turner", email, 10, _clock.UtcNow);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(agent, id);
        agent.SetPasswordHash(_passwordHasher.HashPassword(password));
        if (!isActive)
        {
            agent.Deactivate();
        }
        return agent;
    }

    [Fact]
    public async Task HandleAsync_WithValidCredentials_ReturnsTokenAndAgentDetails()
    {
        var agent = CreateAgent(1, "alex.turner@northwind-support.example", "Northwind_Dev_Pass1!");
        _agents.Setup(a => a.GetByEmailAsync("alex.turner@northwind-support.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var result = await _sut.HandleAsync(
            new LoginRequest("alex.turner@northwind-support.example", "Northwind_Dev_Pass1!"),
            CancellationToken.None);

        Assert.NotEmpty(result.Token);
        Assert.Equal(_clock.UtcNow.AddHours(8), result.ExpiresAtUtc);
        Assert.Equal(1, result.Agent.Id);
        Assert.Equal("Alex Turner", result.Agent.FullName);
        Assert.Equal("alex.turner@northwind-support.example", result.Agent.Email);
    }

    [Fact]
    public async Task HandleAsync_WrongEmailAndWrongPassword_GiveIdenticalUnauthorizedError()
    {
        var agent = CreateAgent(1, "alex.turner@northwind-support.example", "Northwind_Dev_Pass1!");
        _agents.Setup(a => a.GetByEmailAsync("alex.turner@northwind-support.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        _agents.Setup(a => a.GetByEmailAsync("unknown@northwind-support.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        var wrongEmailEx = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.HandleAsync(new LoginRequest("unknown@northwind-support.example", "Northwind_Dev_Pass1!"), CancellationToken.None));

        var wrongPasswordEx = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.HandleAsync(new LoginRequest("alex.turner@northwind-support.example", "Wrong_Password!"), CancellationToken.None));

        Assert.Equal("Invalid email or password.", wrongEmailEx.Message);
        Assert.Equal(wrongEmailEx.Message, wrongPasswordEx.Message);
    }

    [Fact]
    public async Task HandleAsync_WhenAgentIsInactive_ThrowsUnauthorized()
    {
        var inactiveAgent = CreateAgent(6, "yuki.tanaka@northwind-support.example", "Northwind_Dev_Pass1!", isActive: false);
        _agents.Setup(a => a.GetByEmailAsync("yuki.tanaka@northwind-support.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveAgent);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.HandleAsync(new LoginRequest("yuki.tanaka@northwind-support.example", "Northwind_Dev_Pass1!"), CancellationToken.None));

        Assert.Equal("Invalid email or password.", ex.Message);
    }
}
