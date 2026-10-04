namespace FinanceApp.Mobile.Services;

using FinanceApp.Application.Interfaces;

/// <summary>
/// The running app's version, read from the platform so it always reflects the
/// installed package rather than a number copied into the UI.
/// </summary>
public sealed class MauiInstalledVersionProvider
{
    /// <summary>
    /// VersionString (not Version) is the one that carries the Android
    /// versionName, which is what release tags are compared against.
    /// </summary>
    public string Version => AppInfo.Current.VersionString;
}