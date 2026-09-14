namespace FinanceApp.Infrastructure.Configuration;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    public string SqliteConnectionString { get; set; } = "Data Source=financeapp.db";
    public string SupabaseUrl { get; set; } = "";
    public string SupabaseAnonKey { get; set; } = "";
    public bool EnableSensitiveDataLogging { get; set; } = false;
}