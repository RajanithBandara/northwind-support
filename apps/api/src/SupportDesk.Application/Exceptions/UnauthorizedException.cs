namespace SupportDesk.Application.Exceptions;

/// <summary>
/// Thrown when authentication credentials are missing, invalid, or belong to an inactive user.
/// Mapped to HTTP 401 Unauthorized.
/// </summary>
public sealed class UnauthorizedException(string message = "Invalid email or password.") : Exception(message);
