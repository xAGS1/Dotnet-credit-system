using System.Security.Cryptography;

namespace CreditTasksApi.Services;

public class PasswordHasher
{
    private const int SaltBytes = 16;
    private const int KeyBytes = 32;
    private const int Iterations = 100_000;

    public (string hashB64, string saltB64) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeyBytes);
        return (Convert.ToBase64String(key), Convert.ToBase64String(salt));
    }

    public bool Verify(string password, string hashB64, string saltB64)
    {
        var salt = Convert.FromBase64String(saltB64);
        var expected = Convert.FromBase64String(hashB64);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
