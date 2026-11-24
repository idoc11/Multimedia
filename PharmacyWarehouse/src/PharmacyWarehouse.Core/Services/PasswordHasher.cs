using System.Security.Cryptography;
using System.Text;

namespace PharmacyWarehouse.Core.Services;

/// <summary>SHA-256 с индивидуальной солью (требование ТЗ). Сравнение — за постоянное время.</summary>
public static class PasswordHasher
{
    public static (string Hash, string Salt) CreateHash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        return (ComputeHash(password, salt), Convert.ToBase64String(salt));
    }

    public static bool Verify(string password, string storedHash, string storedSalt)
    {
        byte[] salt = Convert.FromBase64String(storedSalt);
        byte[] computed = Convert.FromBase64String(ComputeHash(password, salt));
        byte[] stored = Convert.FromBase64String(storedHash);
        return CryptographicOperations.FixedTimeEquals(computed, stored);
    }

    private static string ComputeHash(string password, byte[] salt)
    {
        byte[] pwd = Encoding.UTF8.GetBytes(password);
        byte[] combined = new byte[pwd.Length + salt.Length];
        Buffer.BlockCopy(pwd, 0, combined, 0, pwd.Length);
        Buffer.BlockCopy(salt, 0, combined, pwd.Length, salt.Length);
        return Convert.ToBase64String(SHA256.HashData(combined));
    }
}
