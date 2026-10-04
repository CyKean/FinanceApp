namespace FinanceApp.Mobile.Services;

using FinanceApp.Application.Interfaces;
using FinanceApp.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Decides when to ask GitHub whether a newer Finora exists, and tells the user
/// through the standard confirm modal when it does.
/// </summary>
/// <remarks>
/// Finora ships as a sideloaded APK rather than a Play Store app, so there is no
/// Play in-app update service to lean on. Instead every launch does a single
/// cheap check against the public GitHub releases endpoint. Play Store's in-app
/// updates work the same way - nothing is pushed to the device, the app finds
/// out the next time it is opened.
/// <para>
/// Results are cached in <see cref="Preferences"/> for a number of hours because
/// GitHub only allows 60 unauthenticated requests per hour per IP address. Without
/// the cache, every install sharing a network would burn that shared budget.
/// </para>
/// </remarks>
public sealed class AppUpdatePromptService
{
    private const string LastCheckedKey = "appupdate.lastCheckedUtc";
    private const string PromptedVersionKey = "appupdate.promptedVersion";

    private readonly IAppUpdateService _updateService;
    private readonly MauiInstalledVersionProvider _versionProvider;
    private readonly ConfirmModalService _confirmModal;
    private readonly ToastService _toast;
    private readonly AppUpdateOptions _options;
    private readonly ILogger<AppUpdatePromptService> _logger;

    public AppUpdatePromptService(
        IAppUpdateService updateService,
        MauiInstalledVersionProvider versionProvider,
        ConfirmModalService confirmModal,
        ToastService toast,
        IOptions<AppUpdateOptions> options,
        ILogger<AppUpdatePromptService> logger)
    {
        _updateService = updateService;
        _versionProvider = versionProvider;
        _confirmModal = confirmModal;
        _toast = toast;
        _options = options.Value;
        _logger = logger;
    }

    public string InstalledVersion => _versionProvider.Version;

    /// <summary>
    /// Checks for an update and, when one exists, asks the user whether to fetch
    /// it. Safe to call on every appearance of the dashboard: it is throttled, and
    /// a dismissed prompt does not reappear until a newer version is published.
    /// </summary>
    public async Task PromptIfAvailableAsync()
    {
        try
        {
            if (!_options.Enabled || !ShouldCheck())
                return;

            Preferences.Set(LastCheckedKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());

            var update = await _updateService.GetUpdateAsync(_versionProvider.Version);
            if (update is null)
                return;

            // Remember the version we nagged about so a user who taps "Later" is
            // not asked again on every launch of the same build.
            if (Preferences.Get(PromptedVersionKey, string.Empty) == update.Version)
                return;

            Preferences.Set(PromptedVersionKey, update.Version);

            // Nothing can show a modal if no page is currently hosting one.
            if (!_confirmModal.HasHost)
                return;

            var accepted = await _confirmModal.ShowAsync(
                title: $"Finora {update.Version} is available",
                message:
                    $"You have {_versionProvider.Version}. Open the download to get the latest version?\n\n" +
                    "Finora will open in your browser, then you install the APK as usual.",
                confirmText: "Download",
                cancelText: "Later");

            if (!accepted)
                return;

            await OpenDownloadAsync(update);
        }
        catch (Exception ex)
        {
            // Never let the update check interfere with the app.
            _logger.LogWarning(ex, "Update prompt failed");
        }
    }

    /// <summary>
    /// Bypasses the cache and reports the outcome, for the manual
    /// "Check for updates" action in Settings.
    /// </summary>
    public async Task CheckNowAsync()
    {
        try
        {
            Preferences.Set(LastCheckedKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());

            var update = await _updateService.GetUpdateAsync(_versionProvider.Version);
            if (update is null)
            {
                await _toast.ShowAsync("Finora is up to date", ToastKind.Success);
                return;
            }

            Preferences.Set(PromptedVersionKey, update.Version);

            await OpenDownloadAsync(update);
            await _toast.ShowAsync($"Finora {update.Version} opened in your browser", ToastKind.Success);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Manual update check failed");
            await _toast.ShowAsync("Could not check for updates", ToastKind.Error);
        }
    }

    /// <summary>
    /// Opens the APK in the browser so Android's installer takes over. Finora is
    /// not distributed through a store, so the user has to allow installs from this
    /// source once; sideloaded updates are always a visible, deliberate step.
    /// </summary>
    private static async Task OpenDownloadAsync(AppUpdateInfo update)
    {
        if (string.IsNullOrWhiteSpace(update.DownloadUrl))
            return;

        await Launcher.Default.OpenAsync(new Uri(update.DownloadUrl));
    }

    private bool ShouldCheck()
    {
        var raw = Preferences.Get(LastCheckedKey, string.Empty);
        if (!long.TryParse(raw, out var lastChecked))
            return true;

        var elapsed = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(lastChecked);
        return elapsed >= TimeSpan.FromHours(Math.Max(1, _options.CacheHours));
    }
}