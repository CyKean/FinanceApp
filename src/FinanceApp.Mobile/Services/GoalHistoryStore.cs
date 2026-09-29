namespace FinanceApp.Mobile.Services;

using System.Text.Json;
using Microsoft.Extensions.Logging;

public record GoalHistoryEntryDto(Guid Id, string GoalName, string Action, string Details, DateTime OccurredAt)
{
    public string ActionIcon => Action == GoalHistoryStore.DeletedAction ? "🗑️" : "✏️";

    public string ActionColor => Action == GoalHistoryStore.DeletedAction ? "#DC2626" : "#16A34A";

    public string TimeLabel => OccurredAt.ToString("MMM dd, yyyy HH:mm");
}

public class GoalHistoryStore
{
    public const string EditedAction = "Edited";
    public const string DeletedAction = "Deleted";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<GoalHistoryStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public GoalHistoryStore(ILogger<GoalHistoryStore> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<GoalHistoryEntryDto>> GetAllAsync(Guid userId)
    {
        await _gate.WaitAsync();
        try
        {
            return await ReadUnsafeAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading goal history for user {UserId}", userId);
            return Array.Empty<GoalHistoryEntryDto>();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddAsync(Guid userId, string goalName, string action, string? details = null)
    {
        await _gate.WaitAsync();
        try
        {
            var entries = (await ReadUnsafeAsync(userId)).ToList();
            entries.Insert(0, new GoalHistoryEntryDto(Guid.NewGuid(), goalName, action, details ?? string.Empty, DateTime.Now));
            await File.WriteAllTextAsync(GetPath(userId), JsonSerializer.Serialize(entries, JsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing goal history for user {UserId}", userId);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string GetPath(Guid userId) =>
        Path.Combine(FileSystem.AppDataDirectory, $"goal_history_{userId:N}.json");

    private async Task<List<GoalHistoryEntryDto>> ReadUnsafeAsync(Guid userId)
    {
        var path = GetPath(userId);
        if (!File.Exists(path))
            return new List<GoalHistoryEntryDto>();

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<List<GoalHistoryEntryDto>>(json, JsonOptions) ?? new List<GoalHistoryEntryDto>();
    }
}
