namespace FinanceApp.Domain.ValueObjects;

/// <summary>
/// A comparable application version, used by the update check to decide whether
/// the published release is newer than what is installed.
/// </summary>
/// <remarks>
/// Comparison follows semantic versioning: numeric parts are compared as numbers
/// so 1.10.0 is correctly newer than 1.9.0, which a plain string or ordinal
/// comparison would get wrong. Pre-release and build suffixes are parsed but
/// deliberately not ordered - a release tagged v1.2.0-rc1 is not something the
/// update check should ever push at a user, so suffixes are ignored when deciding
/// that an update exists.
/// </remarks>
public sealed class AppVersion : IComparable<AppVersion>, IEquatable<AppVersion>
{
    private AppVersion(int major, int minor, int patch, string original)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Original = original;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    /// <summary>The text this version was parsed from, e.g. "1.2.0-rc1".</summary>
    public string Original { get; }

    /// <summary>
    /// Parses a version, tolerating a leading "v" and a missing minor or patch
    /// segment, so "v1", "1.2" and "1.2.3-rc1" all succeed.
    /// </summary>
    public static bool TryParse(string? value, out AppVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var text = value.Trim();
        if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            text = text.Substring(1);

        // Drop the build metadata and pre-release suffix before splitting on '.',
        // so "1.2.3-rc1+build5" reads as 1.2.3.
        var cut = text.IndexOfAny(new[] { '-', '+' });
        if (cut >= 0)
            text = text.Substring(0, cut);

        if (text.Length == 0)
            return false;

        var parts = text.Split('.');
        if (parts.Length > 3)
            return false;

        var numbers = new int[3];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out numbers[i]) || numbers[i] < 0)
                return false;
        }

        version = new AppVersion(numbers[0], numbers[1], numbers[2], value.Trim());
        return true;
    }

    public static AppVersion Parse(string value) =>
        TryParse(value, out var version) && version is not null
            ? version
            : throw new FormatException($"'{value}' is not a valid app version.");

    public int CompareTo(AppVersion? other)
    {
        if (other is null)
            return 1;

        var result = Major.CompareTo(other.Major);
        if (result != 0)
            return result;

        result = Minor.CompareTo(other.Minor);
        if (result != 0)
            return result;

        return Patch.CompareTo(other.Patch);
    }

    /// <summary>True when this version is strictly newer than <paramref name="other"/>.</summary>
    public bool IsNewerThan(AppVersion? other) => CompareTo(other) > 0;

    public bool Equals(AppVersion? other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => Equals(obj as AppVersion);

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch);

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    public static bool operator >(AppVersion? left, AppVersion? right) =>
        left is not null && left.CompareTo(right) > 0;

    public static bool operator <(AppVersion? left, AppVersion? right) =>
        left is null || left.CompareTo(right) < 0;

    public static bool operator >=(AppVersion? left, AppVersion? right) =>
        left is null || left.CompareTo(right) >= 0;

    public static bool operator <=(AppVersion? left, AppVersion? right) =>
        left is not null && left.CompareTo(right) <= 0;
}