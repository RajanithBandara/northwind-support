namespace SupportDesk.Application.Configuration;

/// <summary>
/// Configuration for issuing and validating JWT bearer tokens.
/// SigningKey is supplied via configuration / environment variables (Jwt__SigningKey).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "SupportDesk.Api";

    public string Audience { get; set; } = "SupportDesk.Web";

    /// <summary>8-hour token lifetime matching a standard support agent shift.</summary>
    public int ExpiryHours { get; set; } = 8;
}
