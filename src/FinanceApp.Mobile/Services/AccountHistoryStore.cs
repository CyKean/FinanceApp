namespace FinanceApp.Mobile.Services;

using System.Text.Json;
using Microsoft.Extensions.Logging;

public record AccountHistoryEntryDto(Guid Id, string AccountName, string Action, string Details, DateTime OccurredAt)
{
    public string ActionIcon => Action switch
    {
        AccountHistoryStore.CreatedAction => "➕",
        AccountHistoryStore.DeletedAction => "🗑️",
        _ => "✏️"
    };

    public string ActionColor => Action switch
    {
        AccountHistoryStore.DeletedAction => "#DC2626",
        AccountHistoryStore.CreatedAction => "#16A34A",
        _ => "#0E6B4F"
    };

    public string TimeLabel => OccurredAt.ToString("MMM dd, yyyy HH:mm");
}

public class AccountHistoryStore
{
    public const string CreatedAction = "Created";
    public const string EditedAction = "Edited";
    public const string DeletedAction = "Deleted";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<AccountHistoryStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AccountHistoryStore(ILogger<AccountHistoryStore> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<AccountHistoryEntryDto>> GetAllAsync(Guid userId)
    {
        await _gate.WaitAsync();
        try
        {
            return await ReadUnsafeAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading account history for user {UserId}", userId);
            return Array.Empty<AccountHistoryEntryDto>();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddAsync(Guid userId, string accountName, string action, string? details = null)
    {
        await _gate.WaitAsync();
        try
        {
            var entries = (await ReadUnsafeAsync(userId)).ToList();
            entries.Insert(0, new AccountHistoryEntryDto(Guid.NewGuid(), accountName, action, details ?? string.Empty, DateTime.Now));
            await File.WriteAllTextAsync(GetPath(userId), JsonSerializer.Serialize(entries, JsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing account history for user {UserId}", userId);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string GetPath(Guid userId) =>
        Path.Combine(FileSystem.AppDataDirectory, $"account_history_{userId:N}.json");

    private async Task<List<AccountHistoryEntryDto>> ReadUnsafeAsync(Guid userId)
    {
        var path = GetPath(userId);
        if (!File.Exists(path))
            return new List<AccountHistoryEntryDto>();

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<List<AccountHistoryEntryDto>>(json, JsonOptions) ?? new List<AccountHistoryEntryDto>();
    }
}
