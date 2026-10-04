namespace FinanceApp.Infrastructure.Configuration;

/// <summary>
/// Where Finora looks for its own published releases.
/// </summary>
public class AppUpdateOptions
{
    public const string SectionName = "AppUpdate";

    /// <summary>
    /// Set to false to switch the in-app update prompt off without touching code.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public string Owner { get; set; } = "CyKean";

    public string Repository { get; set; } = "FinanceApp";

    /// <summary>
    /// How long a check result is trusted before the app asks GitHub again.
    /// GitHub allows only 60 unauthenticated requests per hour per IP address,
    /// and every Finora install on a network shares that budget, so this is
    /// deliberately hours rather than minutes.
    /// </summary>
    public int CacheHours { get; set; } = 12;

    public int TimeoutSeconds { get; set; } = 10;
}