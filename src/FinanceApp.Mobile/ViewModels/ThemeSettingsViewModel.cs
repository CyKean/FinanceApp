namespace FinanceApp.Mobile.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Mobile.Services.Theming;

public partial class ThemeSettingsViewModel : BaseViewModel
{
    private readonly ThemeService _themeService;

    [ObservableProperty]
    private string _customAccentHex = "#CDF463";

    [ObservableProperty]
    private bool _customDarkBase;

    [ObservableProperty]
    private string _customError = string.Empty;

    [ObservableProperty]
    private string _activeThemeName = string.Empty;

    [ObservableProperty]
    private bool _isSystemSelected;

    [ObservableProperty]
    private bool _isCustomSelected;

    public bool HasCustomError => !string.IsNullOrWhiteSpace(CustomError);

    partial void OnCustomErrorChanged(string value) => OnPropertyChanged(nameof(HasCustomError));

    public ObservableCollection<ThemePresetItem> Presets { get; } = new();

    public ThemeSettingsViewModel(ThemeService themeService)
    {
        _themeService = themeService;
        Title = "Theme";

        foreach (var p in ThemePresets.All)
            Presets.Add(new ThemePresetItem(p));

        if (!string.IsNullOrWhiteSpace(_themeService.CustomAccent))
            _customAccentHex = _themeService.CustomAccent;
        _customDarkBase = _themeService.CustomDarkBase;
        RefreshSelection();
    }

    [RelayCommand]
    private void SelectPreset(ThemePresetItem item)
    {
        _themeService.ApplyPreset(item.Palette);
        RefreshSelection();
    }

    [RelayCommand]
    private void UseSystem()
    {
        _themeService.ApplySystem();
        RefreshSelection();
    }

    [RelayCommand]
    private void ApplyCustom()
    {
        CustomError = string.Empty;
        var error = _themeService.ApplyCustom(CustomAccentHex, CustomDarkBase);
        if (error is not null)
        {
            CustomError = error;
            return;
        }
        RefreshSelection();
    }

    [RelayCommand]
    private void Reset()
    {
        _themeService.ResetToDefault();
        CustomAccentHex = ThemePresets.CreamLime.Accent;
        CustomDarkBase = false;
        CustomError = string.Empty;
        RefreshSelection();
    }

    public void RefreshSelection()
    {
        ActiveThemeName = _themeService.DisplayName;
        IsSystemSelected = _themeService.Mode == ThemeMode.System;
        IsCustomSelected = _themeService.Mode == ThemeMode.Custom;
        foreach (var p in Presets)
            p.IsSelected = _themeService.Mode == ThemeMode.Preset && p.Palette == _themeService.Current;
    }
}

public partial class ThemePresetItem : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public ThemePresetItem(ThemePalette palette) => Palette = palette;

    public ThemePalette Palette { get; }

    public string Name => Palette.Name;

    public Color PreviewBackground => Color.FromArgb(Palette.Background);

    public Color PreviewAccent => Color.FromArgb(Palette.Accent);

    public Color PreviewCard => Color.FromArgb(Palette.Card);
}
