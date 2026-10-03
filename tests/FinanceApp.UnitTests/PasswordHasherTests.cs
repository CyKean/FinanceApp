namespace FinanceApp.UnitTests;

using FinanceApp.Infrastructure.Services;
using Xunit;

/// <summary>
/// Passwords are the one thing an offline-first auth cannot delegate to a
/// server, so they are verified against a local digest. These tests pin the
/// properties that digest has to have.
/// </summary>
public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Verify_AcceptsTheOriginalPassword()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.True(_hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_RejectsAnyOtherPassword()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.False(_hasher.Verify("correct horse battery stapl", hash));
        Assert.False(_hasher.Verify("Correct horse battery staple", hash));
        Assert.False(_hasher.Verify(string.Empty, hash));
    }

    [Fact]
    public void Hash_SaltsEachDigestSoIdenticalPasswordsDiffer()
    {
        var first = _hasher.Hash("correct horse");
        var second = _hasher.Hash("correct horse");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify("correct horse", first));
        Assert.True(_hasher.Verify("correct horse", second));
    }

    [Fact]
    public void Hash_RecordsItsOwnAlgorithmAndWorkFactor()
    {
        var hash = _hasher.Hash("correct horse");

        Assert.StartsWith("$pbkdf2-sha256$", hash);
        Assert.Contains("210000", hash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-hash")]
    [InlineData("$pbkdf2-sha256$notanumber$c2FsdA==$aGFzaA==")]
    [InlineData("$pbkdf2-sha256$1000$notbase64!$aGFzaA==")]
    [InlineData("$pbkdf2-sha256$1000$c2FsdA==")]
    [InlineData("$md5$1000$c2FsdA==$aGFzaA==")]
    public void Verify_ReturnsFalseForMalformedDigests(string storedHash)
    {
        Assert.False(_hasher.Verify("correct horse", storedHash));
    }

    [Fact]
    public void Verify_ReturnsFalseWhenThePasswordIsMissing()
    {
        var hash = _hasher.Hash("correct horse");

        Assert.False(_hasher.Verify(string.Empty, hash));
        Assert.False(_hasher.Verify(null!, hash));
        Assert.False(_hasher.Verify("correct horse", string.Empty));
    }
}