namespace FinanceApp.Application.Interfaces;

/// <summary>
/// Outcome of an update check.
/// </summary>
public enum AppUpdateStatus
{
    /// <summary>The check could not be completed (offline, rate limited, no release yet).</summary>
    Unavailable,

    /// <summary>The installed build is current, or newer than what is published.</summary>
    UpToDate,

    /// <summary>A newer release is published and can be installed.</summary>
    UpdateAvailable
}

/// <summary>
/// Details of a published release that is newer than the installed build.
/// </summary>
public sealed record AppUpdateInfo(
    string TagName,
    string Version,
    string ReleaseNotesUrl,
    string DownloadUrl);

/// <summary>
/// Checks whether a newer Finora release has been published.
/// </summary>
public interface IAppUpdateService
{
    /// <param name="installedVersion">Version of the running build, e.g. "1.0.0".</param>
    Task<AppUpdateInfo?> GetUpdateAsync(string installedVersion, CancellationToken cancellationToken = default);
}