using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Configuration;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Infrastructure.Security;

/// <summary>
/// Issues and validates RFC 7519 HMAC-SHA256 JSON Web Tokens using the configured signing key
/// and injectable clock.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : IJwtTokenService
{
    private static readonly byte[] HeaderBytes = JsonSerializer.SerializeToUtf8Bytes(new
    {
        alg = "HS256",
        typ = "JWT"
    });

    private readonly JwtOptions _options = options.Value;

    public IssuedToken IssueToken(Agent agent)
    {
        var keyBytes = GetSigningKeyBytes();
        var now = clock.UtcNow;
        var expiresAt = now.AddHours(_options.ExpiryHours > 0 ? _options.ExpiryHours : 8);

        var iat = new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var exp = new DateTimeOffset(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc)).ToUnixTimeSeconds();

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(new JwtPayloadData
        {
            sub = agent.Id.ToString(),
            email = agent.Email,
            name = agent.FullName,
            iss = _options.Issuer,
            aud = _options.Audience,
            iat = iat,
            exp = exp
        });

        var encodedHeader = Base64UrlEncode(HeaderBytes);
        var encodedPayload = Base64UrlEncode(payloadBytes);
        var signingInput = $"{encodedHeader}.{encodedPayload}";
        var signatureBytes = ComputeSignature(signingInput, keyBytes);
        var encodedSignature = Base64UrlEncode(signatureBytes);

        return new IssuedToken($"{signingInput}.{encodedSignature}", expiresAt);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        byte[] keyBytes;
        try
        {
            keyBytes = GetSigningKeyBytes();
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        var signingInput = $"{parts[0]}.{parts[1]}";
        var expectedSignature = ComputeSignature(signingInput, keyBytes);

        byte[] actualSignature;
        byte[] payloadBytes;
        try
        {
            actualSignature = Base64UrlDecode(parts[2]);
            payloadBytes = Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(actualSignature, expectedSignature))
        {
            return null;
        }

        JwtPayloadData? payload;
        try
        {
            payload = JsonSerializer.Deserialize<JwtPayloadData>(payloadBytes);
        }
        catch (JsonException)
        {
            return null;
        }

        if (payload is null ||
            string.IsNullOrWhiteSpace(payload.sub) ||
            string.IsNullOrWhiteSpace(payload.email) ||
            string.IsNullOrWhiteSpace(payload.name))
        {
            return null;
        }

        if (!string.Equals(payload.iss, _options.Issuer, StringComparison.Ordinal) ||
            !string.Equals(payload.aud, _options.Audience, StringComparison.Ordinal))
        {
            return null;
        }

        var nowUnix = new DateTimeOffset(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc)).ToUnixTimeSeconds();
        if (payload.exp <= nowUnix)
        {
            return null;
        }

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, payload.sub),
            new(ClaimTypes.Email, payload.email),
            new(ClaimTypes.Name, payload.name),
            new("sub", payload.sub),
            new("email", payload.email),
            new("name", payload.name)
        ];

        var identity = new ClaimsIdentity(claims, authenticationType: "Bearer", ClaimTypes.Name, ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    private byte[] GetSigningKeyBytes()
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be configured with at least 32 characters (via appsettings or Jwt__SigningKey).");
        }

        return Encoding.UTF8.GetBytes(_options.SigningKey);
    }

    private static byte[] ComputeSignature(string signingInput, byte[] keyBytes) =>
        HMACSHA256.HashData(keyBytes, Encoding.ASCII.GetBytes(signingInput));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        return Convert.FromBase64String(padded);
    }

    private sealed class JwtPayloadData
    {
        public string sub { get; set; } = string.Empty;
        public string email { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string iss { get; set; } = string.Empty;
        public string aud { get; set; } = string.Empty;
        public long iat { get; set; }
        public long exp { get; set; }
    }
}
