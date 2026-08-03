using System.Security.Cryptography;
using System.Text;

namespace KorrnellHelper.Api.Security;

/// <summary>
/// Verifies the "X-Line-Signature" header LINE sends on every webhook
/// request — HMAC-SHA256 of the raw request body, keyed by the channel
/// secret, base64-encoded. Without this, anyone who finds the webhook URL
/// could POST fake events and trigger AI calls or spoof messages as any
/// user, whitelist or no whitelist.
/// </summary>
public static class LineSignatureVerifier
{
    public static bool IsValid(string requestBody, string signatureHeader, string channelSecret)
    {
        if (signatureHeader.Length == 0)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(channelSecret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(requestBody));
        var computedSignature = Convert.ToBase64String(computedHash);

        return ConstantTimeCompare.Equals(signatureHeader, computedSignature);
    }
}
