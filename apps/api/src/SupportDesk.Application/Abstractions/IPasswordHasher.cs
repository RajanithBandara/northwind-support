namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Hashes and verifies user passwords using a salted, one-way cryptographic derivation function.
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);

    bool VerifyPassword(string password, string passwordHash);
}
