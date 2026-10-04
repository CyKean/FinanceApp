namespace FinanceApp.Infrastructure.Services;

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceApp.Application.Interfaces;
using FinanceApp.Domain.ValueObjects;
using FinanceApp.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Reads the newest published Finora release from GitHub and reports it when it is
/// newer than the installed build.
/// </summary>
/// <remarks>
/// Finora is distributed as a sideloaded APK rather than through Google Play, so the
/// Play in-app update API is not available. This asks GitHub instead, against the
/// public releases endpoint, which needs no token and therefore ships no secret in
/// the APK.
/// <para>
/// Every failure mode - offline, rate limited, no release published yet, malformed
/// payload - resolves to "no update" without throwing. A release check is a
/// convenience and must never be able to break the app or block startup.
/// </para>
/// </remarks>
public sealed class GitHubAppUpdateService : IAppUpdateService
{
    public const string HttpClientName = "appupdate";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppUpdateOptions _options;
    private readonly ILogger<GitHubAppUpdateService> _logger;

    public GitHubAppUpdateService(
        IHttpClientFactory httpClientFactory,
        IOptions<AppUpdateOptions> options,
        ILogger<GitHubAppUpdateService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AppUpdateInfo?> GetUpdateAsync(
        string installedVersion,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return null;

        if (string.IsNullOrWhiteSpace(_options.Owner) || string.IsNullOrWhiteSpace(_options.Repository))
        {
            _logger.LogWarning("App update check skipped: Owner/Repository not configured");
            return null;
        }

        if (!AppVersion.TryParse(installedVersion, out var installed) || installed is null)
        {
            _logger.LogWarning("App update check skipped: '{Version}' is not a parseable version", installedVersion);
            return null;
        }

        var requestUri = $"https://api.github.com/repos/{_options.Owner}/{_options.Repository}/releases/latest";

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            // GitHub rejects API calls without a User-Agent.
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Finora", "1.0"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // 404 simply means nothing has been published yet, which is the
                // normal state until the first release goes out.
                _logger.LogInformation(
                    "App update check returned {Status} for {Uri}", (int)response.StatusCode, requestUri);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json, JsonOptions);
            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
                return null;

            // Drafts and prereleases are already excluded by the "latest" endpoint,
            // but a non-semver tag must never be pushed at a user either.
            if (!AppVersion.TryParse(release.TagName, out var latest) || latest is null)
            {
                _logger.LogInformation("App update check skipped: tag '{Tag}' is not a semver tag", release.TagName);
                return null;
            }

            if (!latest.IsNewerThan(installed))
            {
                _logger.LogInformation(
                    "Finora is up to date (installed {Installed}, latest {Latest})", installed, latest);
                return null;
            }

            return new AppUpdateInfo(
                TagName: release.TagName,
                Version: latest.ToString(),
                ReleaseNotesUrl: release.HtmlUrl ?? $"https://github.com/{_options.Owner}/{_options.Repository}/releases/tag/{release.TagName}",
                DownloadUrl: ResolveDownloadUrl(release));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "App update check failed");
            return null;
        }
    }

    /// <summary>
    /// Prefers the APK asset so the browser starts the download straight away,
    /// falling back to the release page when no APK is attached.
    /// </summary>
    private static string ResolveDownloadUrl(GitHubRelease release)
    {
        var apk = release.Assets?.FirstOrDefault(a =>
            a.Name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(apk?.BrowserDownloadUrl))
            return apk!.BrowserDownloadUrl!;

        return release.HtmlUrl ?? string.Empty;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}