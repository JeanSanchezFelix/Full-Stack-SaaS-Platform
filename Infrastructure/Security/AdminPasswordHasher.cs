using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using System.Text;

namespace SvelteHybridMVC.Infrastructure.Security;

public sealed class AdminPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MemorySize = 65536;
    private const int Iterations = 3;
    private const int DegreeOfParallelism = 2;

    public (byte[] Salt, byte[] PasswordHash) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        return (salt, DeriveHash(password, salt));
    }

    public bool VerifyPassword(string password, byte[] salt, byte[] expectedHash)
    {
        if (salt.Length == 0 || expectedHash.Length == 0)
        {
            return false;
        }

        var actualHash = DeriveHash(password, salt);
        return actualHash.Length == expectedHash.Length
            && CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] DeriveHash(string password, byte[] salt)
    {
        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            Iterations = Iterations,
            MemorySize = MemorySize
        };

        return argon2.GetBytes(HashSize);
    }
}
