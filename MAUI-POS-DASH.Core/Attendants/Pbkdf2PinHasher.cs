using System.Security.Cryptography;

namespace MAUI_POS_DASH.Core.Attendants;

/// <summary>
/// We use PBKDF2 (via the BCL's Rfc2898DeriveBytes) rather than a third-party package — Core
/// stays dependency-free, and it's the same primitive ASP.NET Core Identity's own
/// PasswordHasher uses internally. The salt and iteration count are packed into the returned
/// string so no separate salt column is needed.
/// </summary>
public class Pbkdf2PinHasher : IPinHasher
{
    #region Fields
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;
    #endregion

    #region Public Methods
    public string Hash(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, Algorithm, HashSize);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string pin, string hash)
    {
        var parts = hash.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[1]);
        var expectedHash = Convert.FromBase64String(parts[2]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, Algorithm, expectedHash.Length);

        // We use a fixed-time comparison rather than == or SequenceEqual — a short-circuiting
        // comparison would leak how many leading bytes matched via response timing.
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
    #endregion
}
