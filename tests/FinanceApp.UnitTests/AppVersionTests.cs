namespace FinanceApp.UnitTests;

using FinanceApp.Domain.ValueObjects;

public class AppVersionTests
{
    [Theory]
    [InlineData("1.0.0", 1, 0, 0)]
    [InlineData("v1.0.0", 1, 0, 0)]
    [InlineData("1.2", 1, 2, 0)]
    [InlineData("1", 1, 0, 0)]
    [InlineData("  v2.3.4  ", 2, 3, 4)]
    [InlineData("1.0.1", 1, 0, 1)]
    [InlineData("10.20.30", 10, 20, 30)]
    public void TryParse_AcceptsCommonShapes(string input, int major, int minor, int patch)
    {
        Assert.True(AppVersion.TryParse(input, out var version));
        Assert.NotNull(version);
        Assert.Equal(major, version!.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
    }

    [Theory]
    [InlineData("1.0.0-rc1", "1.0.0")]
    [InlineData("v1.2.3-beta.2", "1.2.3")]
    [InlineData("1.0.0+build.7", "1.0.0")]
    [InlineData("1.0.0-rc1+build.7", "1.0.0")]
    [InlineData("v2.5.0-alpha", "2.5.0")]
    public void TryParse_StripsPrereleaseAndBuildSuffixes(string input, string expected)
    {
        Assert.True(AppVersion.TryParse(input, out var version));
        Assert.Equal(expected, version!.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("v")]
    [InlineData("abc")]
    [InlineData("1.2.3.4")]
    [InlineData("-1.0.0")]
    [InlineData("1.-2.0")]
    public void TryParse_RejectsGarbage(string? input)
    {
        Assert.False(AppVersion.TryParse(input, out var version));
        Assert.Null(version);
    }

    [Fact]
    public void Parse_ThrowsOnGarbage() =>
        Assert.Throws<FormatException>(() => AppVersion.Parse("not-a-version"));

    [Fact]
    public void CompareTo_OrdersNumericallyNotAlphabetically()
    {
        // The whole point of parsing: "1.10.0" must beat "1.9.0", which an ordinal
        // string comparison gets exactly backwards.
        Assert.True(AppVersion.Parse("1.10.0").IsNewerThan(AppVersion.Parse("1.9.0")));
        Assert.True(AppVersion.Parse("1.0.10").IsNewerThan(AppVersion.Parse("1.0.9")));
        Assert.True(AppVersion.Parse("2.0.0").IsNewerThan(AppVersion.Parse("1.99.99")));
    }

    [Fact]
    public void CompareTo_DetectsEqualVersions()
    {
        Assert.Equal(0, AppVersion.Parse("1.0.0").CompareTo(AppVersion.Parse("v1.0.0")));
        Assert.Equal(AppVersion.Parse("1.0.0"), AppVersion.Parse("1.0.0"));
        Assert.False(AppVersion.Parse("1.0.0").IsNewerThan(AppVersion.Parse("1.0.0")));
    }

    [Fact]
    public void IsNewerThan_IgnoresShorthandEquivalence()
    {
        // "v1" and "1.0.0" are the same version, so neither is newer.
        Assert.False(AppVersion.Parse("v1").IsNewerThan(AppVersion.Parse("1.0.0")));
        Assert.False(AppVersion.Parse("1.0.0").IsNewerThan(AppVersion.Parse("v1")));
    }

    [Fact]
    public void IsNewerThan_TreatsShorthandAsOlderThanFullVersion()
    {
        Assert.True(AppVersion.Parse("1.0.1").IsNewerThan(AppVersion.Parse("v1")));
    }

    [Fact]
    public void IsNewerThan_ComparesAgainstNullAsNewer() =>
        Assert.True(AppVersion.Parse("1.0.0").IsNewerThan(null));

    [Fact]
    public void OperatorsCompareVersions()
    {
        var older = AppVersion.Parse("1.0.0");
        var newer = AppVersion.Parse("1.0.1");

        Assert.True(newer > older);
        Assert.True(older < newer);
        Assert.True(newer >= older);
        Assert.True(older <= newer);
        Assert.False(older > newer);
    }
}