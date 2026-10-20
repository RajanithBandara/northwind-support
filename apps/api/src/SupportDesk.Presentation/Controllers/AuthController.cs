using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Application.Features.Auth.Queries.GetCurrentAgent;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    /// <summary>Signs in an active support agent and returns a signed JWT token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public Task<LoginResponseDto> Login(
        LoginRequest request,
        [FromServices] LoginCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(request, ct);

    /// <summary>Returns the currently authenticated agent from the bearer token.</summary>
    [HttpGet("me")]
    [Authorize]
    public Task<CurrentAgentDto> Me(
        [FromServices] GetCurrentAgentQueryHandler handler,
        CancellationToken ct)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!int.TryParse(idClaim, out var agentId))
        {
            throw new UnauthorizedException("Invalid authentication token.");
        }

        return handler.HandleAsync(agentId, ct);
    }
}
