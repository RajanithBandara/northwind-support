using SupportDesk.Infrastructure.Security;

namespace SupportDesk.UnitTests.Infrastructure.Security;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _sut = new();

    [Fact]
    public void VerifyPassword_WithMatchingPassword_ReturnsTrue()
    {
        var hash = _sut.HashPassword("Northwind_Dev_Pass1!");

        Assert.True(_sut.VerifyPassword("Northwind_Dev_Pass1!", hash));
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        var hash = _sut.HashPassword("Northwind_Dev_Pass1!");

        Assert.False(_sut.VerifyPassword("Wrong_Password123!", hash));
    }

    [Fact]
    public void HashPassword_GeneratesUniqueSaltedHashesForSameInput()
    {
        var hash1 = _sut.HashPassword("Northwind_Dev_Pass1!");
        var hash2 = _sut.HashPassword("Northwind_Dev_Pass1!");

        Assert.NotEqual(hash1, hash2);
        Assert.True(_sut.VerifyPassword("Northwind_Dev_Pass1!", hash1));
        Assert.True(_sut.VerifyPassword("Northwind_Dev_Pass1!", hash2));
    }
}
