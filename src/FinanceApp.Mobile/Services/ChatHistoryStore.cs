namespace FinanceApp.Mobile.Services;

using System.Diagnostics;
using System.Text.Json;
using FinanceApp.Application.DTOs;
using Microsoft.Extensions.Logging;

public class ChatHistoryStore
{
    private const int MaxMessages = 200;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<ChatHistoryStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ChatHistoryStore(ILogger<ChatHistoryStore> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetAllAsync(Guid userId)
    {
        await _gate.WaitAsync();
        try
        {
            return await ReadUnsafeAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading chat history for user {UserId}", userId);
            return Array.Empty<ChatMessageDto>();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AppendAsync(Guid userId, ChatMessageDto message)
    {
        await _gate.WaitAsync();
        try
        {
            var messages = (await ReadUnsafeAsync(userId)).ToList();
            messages.Add(message);
            if (messages.Count > MaxMessages)
                messages.RemoveRange(0, messages.Count - MaxMessages);

            await WriteAtomicAsync(GetPath(userId), JsonSerializer.Serialize(messages, JsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing chat history for user {UserId}", userId);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(Guid userId)
    {
        await _gate.WaitAsync();
        try
        {
            var path = GetPath(userId);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing chat history for user {UserId}", userId);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string GetPath(Guid userId) =>
        Path.Combine(FileSystem.AppDataDirectory, $"chat_history_{userId:N}.json");

    /// <summary>
    /// Writes via a temp file and swaps it in. WriteAllTextAsync truncates in
    /// place, so an interrupted write left unparseable JSON behind - and the
    /// reader treated that as "no history", silently wiping the conversation.
    /// </summary>
    private static async Task WriteAtomicAsync(string path, string contents)
    {
        var temp = path + ".tmp";

        try
        {
            await File.WriteAllTextAsync(temp, contents);
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch (Exception cleanup)
            {
                Debug.WriteLine($"[ChatHistory] temp cleanup failed: {cleanup.Message}");
            }

            throw;
        }
    }

    private async Task<List<ChatMessageDto>> ReadUnsafeAsync(Guid userId)
    {
        var path = GetPath(userId);
        if (!File.Exists(path))
            return new List<ChatMessageDto>();

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<List<ChatMessageDto>>(json, JsonOptions) ?? new List<ChatMessageDto>();
    }
}
