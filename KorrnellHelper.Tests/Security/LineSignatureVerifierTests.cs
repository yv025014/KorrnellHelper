using KorrnellHelper.Api.Security;
using Xunit;

namespace KorrnellHelper.Tests.Security;

public class LineSignatureVerifierTests
{
    // Precomputed independently (Python's hmac/hashlib, not this codebase) for this exact
    // secret+body pair, so this test verifies against a known-correct vector rather than
    // just round-tripping the same algorithm against itself.
    private const string Secret = "test-channel-secret";
    private const string Body = """{"events":[{"type":"message"}]}""";
    private const string KnownCorrectSignature = "t3LUo8vUQA+CBUc7+EBD1Gez+u/ExrSz324HjxbNDmM=";

    [Fact]
    public void IsValid_KnownCorrectSignature_ReturnsTrue()
    {
        Assert.True(LineSignatureVerifier.IsValid(Body, KnownCorrectSignature, Secret));
    }

    [Fact]
    public void IsValid_WrongSignature_ReturnsFalse()
    {
        Assert.False(LineSignatureVerifier.IsValid(Body, "wrong-signature==", Secret));
    }

    [Fact]
    public void IsValid_TamperedBody_ReturnsFalse()
    {
        Assert.False(LineSignatureVerifier.IsValid(Body + " ", KnownCorrectSignature, Secret));
    }

    [Fact]
    public void IsValid_WrongSecret_ReturnsFalse()
    {
        Assert.False(LineSignatureVerifier.IsValid(Body, KnownCorrectSignature, "different-secret"));
    }

    [Fact]
    public void IsValid_MissingSignatureHeader_ReturnsFalseWithoutThrowing()
    {
        Assert.False(LineSignatureVerifier.IsValid(Body, "", Secret));
    }
}
