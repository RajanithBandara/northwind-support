using System.Net.Mime;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Presentation.Authentication;

/// <summary>
/// Validates Bearer JWT tokens on incoming HTTP requests using <see cref="IJwtTokenService"/>.
/// Writes an RFC 7807 ProblemDetails document on 401 challenges.
/// </summary>
public sealed class JwtAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IJwtTokenService jwtTokenService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Bearer";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var header = headerValues.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid authorization scheme."));
        }

        var token = header["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing bearer token."));
        }

        var principal = jwtTokenService.ValidateToken(token);
        if (principal is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid or expired bearer token."));
        }

        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = MediaTypeNames.Application.Json;
        Response.Headers.Append("WWW-Authenticate", SchemeName);

        var problem = new ProblemDetails
        {
            Title = "Unauthorized",
            Detail = "A valid bearer token is required to access this resource.",
            Status = StatusCodes.Status401Unauthorized
        };
        problem.Extensions["traceId"] = Context.TraceIdentifier;

        await Response.WriteAsJsonAsync(problem);
    }
}
