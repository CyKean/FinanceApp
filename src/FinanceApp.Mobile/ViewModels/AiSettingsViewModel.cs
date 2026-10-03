namespace FinanceApp.Mobile.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Application.Interfaces;
using FinanceApp.Infrastructure.Configuration;
using FinanceApp.Mobile.Services;
using Microsoft.Extensions.Logging;

public partial class AiSettingsViewModel : BaseViewModel
{
    private readonly IAiClient _aiClient;
    private readonly IAiSettingsStore _settingsStore;
    private readonly ISessionStore _sessionStore;
    private readonly IDialogService _dialogService;
    private readonly ILogger<AiSettingsViewModel> _logger;

    // Groq is first, and shares AiOptions' defaults, so the provider pre-selected on
    // a fresh install is always the same one the client falls back to. It is the
    // only provider still publishing a per-model free-tier table and needs no
    // card; its 70B model allows 1,000 requests/day and 100K tokens/day, which is
    // plenty for a personal assistant. llama-3.1-8b-instant trades quality for
    // 500K tokens/day.
    //
    // Google's free tier has been cut to roughly 20 requests/day, so it is listed
    // but not recommended, and OpenRouter is reached through the openrouter/free
    // router rather than a hardcoded ":free" id, because those catalogue entries
    // appear and disappear constantly.
    private static readonly (string Name, string BaseUrl, string Model)[] Providers =
    {
        ("Groq - Llama 3.3 70B (free, default)", AiOptions.DefaultBaseUrl, AiOptions.DefaultModel),
        ("Groq - Llama 3.1 8B (free, high volume)", AiOptions.DefaultBaseUrl, "llama-3.1-8b-instant"),
        ("OpenRouter - any free model", "https://openrouter.ai/api/v1", "openrouter/free"),
        ("Cerebras - Llama 3.3 70B (free)", "https://api.cerebras.ai/v1", "llama-3.3-70b-versatile"),
        ("Google Gemini 2.5 Flash (free, low limit)", "https://generativelanguage.googleapis.com/v1beta/openai/", "gemini-2.5-flash"),
        ("OpenAI (paid)", "https://api.openai.com/v1", "gpt-4o-mini")
    };

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private bool _isConfigured;

    [ObservableProperty]
    private string _model = string.Empty;

    [ObservableProperty]
    private string _baseUrl = string.Empty;

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private bool _isTesting;

    public AiSettingsViewModel(
        IAiClient aiClient,
        IAiSettingsStore settingsStore,
        ISessionStore sessionStore,
        IDialogService dialogService,
        ILogger<AiSettingsViewModel> logger)
    {
        _aiClient = aiClient;
        _settingsStore = settingsStore;
        _sessionStore = sessionStore;
        _dialogService = dialogService;
        _logger = logger;
        Title = "AI Assistant";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        IsChecking = true;
        ClearError();

        try
        {
            var settings = await _settingsStore.GetAsync();
            BaseUrl = string.IsNullOrWhiteSpace(settings?.BaseUrl) ? _aiClient.DefaultBaseUrl : settings.BaseUrl;
            Model = string.IsNullOrWhiteSpace(settings?.Model) ? _aiClient.DefaultModel : settings.Model;
            IsConfigured = await _aiClient.IsConfiguredAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking AI configuration");
            SetError("Couldn't check the AI configuration");
        }
        finally
        {
            IsChecking = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var settings = await TryGetFormSettingsAsync();
        if (settings is null)
            return;

        var key = ApiKey.Trim();
        if (string.IsNullOrWhiteSpace(key) && !IsConfigured)
        {
            await _dialogService.ShowFailureAsync("Paste an API key first");
            return;
        }

        try
        {
            await _settingsStore.SaveAsync(settings);

            if (!string.IsNullOrWhiteSpace(key))
            {
                await _sessionStore.SaveAsync(AiClientDefaults.ApiKeySessionKey, key);
                ApiKey = string.Empty;
                IsConfigured = true;
                await _dialogService.ShowSuccessAsync("API key saved");
            }
            else
            {
                await _dialogService.ShowSuccessAsync("Provider settings saved");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving AI settings");
            await _dialogService.ShowFailureAsync("Couldn't save the settings");
        }
    }

    [RelayCommand]
    private async Task ChooseProviderAsync()
    {
        var names = Providers.Select(p => p.Name).ToArray();
        var choice = await _dialogService.ShowChoiceSheetAsync("Choose a provider", names);
        if (choice is null) return;

        var provider = Providers.First(p => p.Name == choice);
        BaseUrl = provider.BaseUrl;
        Model = provider.Model;
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (IsTesting) return;

        var settings = await TryGetFormSettingsAsync();
        if (settings is null)
            return;

        IsTesting = true;
        try
        {
            var key = ApiKey.Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                await _sessionStore.SaveAsync(AiClientDefaults.ApiKeySessionKey, key);
                ApiKey = string.Empty;
                IsConfigured = true;
            }

            if (!await _aiClient.IsConfiguredAsync())
            {
                await _dialogService.ShowFailureAsync("Paste an API key first");
                return;
            }

            await _settingsStore.SaveAsync(settings);
            var result = await _aiClient.TestAsync();

            if (result.Success)
            {
                // Name the host too: with several free tiers configured it is
                // not obvious which one actually answered.
                var host = Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var parsed)
                    ? parsed.Host
                    : settings.BaseUrl;
                await _dialogService.ShowSuccessAsync($"Connected to {host} — {settings.Model} replied");
            }
            else
            {
                await _dialogService.ShowFailureAsync(result.Error ?? "Connection failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing AI connection");
            await _dialogService.ShowFailureAsync("Couldn't reach the provider");
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Remove API key",
            "The AI chat will fall back to on-device answers only. Remove the key?",
            "Remove",
            "Cancel",
            destructive: true);

        if (!confirmed) return;

        try
        {
            await _sessionStore.RemoveAsync(AiClientDefaults.ApiKeySessionKey);

            // Drop the provider preset too, otherwise the saved Groq/Gemini base
            // URL and model linger and silently apply to the next key added.
            await _settingsStore.ClearAsync();

            IsConfigured = false;
            await _dialogService.ShowSuccessAsync("API key removed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing AI API key");
            await _dialogService.ShowFailureAsync("Couldn't remove the API key");
        }
    }

    private async Task<AiProviderSettings?> TryGetFormSettingsAsync()
    {
        var baseUrl = BaseUrl.Trim().TrimEnd('/');
        var model = Model.Trim();

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            await _dialogService.ShowFailureAsync("Base URL must be a valid http(s) address");
            return null;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            await _dialogService.ShowFailureAsync("Enter a model name");
            return null;
        }

        return new AiProviderSettings(baseUrl, model);
    }
}
