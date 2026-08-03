using System.Security.Cryptography;
using System.Text;

namespace KorrnellHelper.Api.Security;

/// <summary>
/// Shared by ApiKeyAuthFilter and LineSignatureVerifier — both compare a
/// caller-supplied secret/signature against an expected value and need to
/// avoid leaking timing information about how much of it matched.
/// </summary>
public static class ConstantTimeCompare
{
    public static bool Equals(string provided, string expected)
    {
        if (provided.Length == 0)
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        // Lengths differing is itself not secret, but comparing byte-for-byte
        // only when lengths already match keeps this a constant-time check
        // for same-length inputs, which is the case that actually matters.
        return providedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
