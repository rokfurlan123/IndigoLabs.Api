using System.Security.Cryptography;

namespace IndigoLabs.Api.Authentication;

public static class PasswordHashVerifier
{
    public static bool Verify(
        string password,
        string saltBase64,
        string expectedHashBase64,
        int iterationCount)
    {
        if (iterationCount <= 0)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(saltBase64);
            var expectedHash = Convert.FromBase64String(expectedHashBase64);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterationCount,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
