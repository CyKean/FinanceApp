namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

public partial class ChoiceSheetHost : ContentView
{
    private ChoiceSheetService? _choiceSheetService;
    private ChoiceSheetRequest? _active;

    public ChoiceSheetHost()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _choiceSheetService = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services.GetService<ChoiceSheetService>();
        if (_choiceSheetService != null)
            _choiceSheetService.ChoiceRequested += OnChoiceRequested;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_choiceSheetService != null)
            _choiceSheetService.ChoiceRequested -= OnChoiceRequested;
        _choiceSheetService = null;
    }

    private void OnChoiceRequested(ChoiceSheetRequest request)
    {
        MainThread.BeginInvokeOnMainThread(() => Show(request));
    }

    private void Show(ChoiceSheetRequest request)
    {
        if (_active != null)
        {
            var previous = _active;
            _active = null;
            previous.Completion.TrySetResult(null);
        }

        _active = request;
        TitleLabel.Text = request.Title;
        OptionsContainer.Children.Clear();
        foreach (var option in request.Options)
            OptionsContainer.Children.Add(CreateOptionRow(option));

        IsVisible = true;
        Scrim.Opacity = 0;
        Card.Opacity = 0;
        Card.Scale = 0.92;
        _ = Scrim.FadeToAsync(1, 160);
        _ = Card.FadeToAsync(1, 160);
        _ = Card.ScaleToAsync(1, 160);
    }

    private View CreateOptionRow(string option)
    {
        var (accent, glyph) = option.ToLowerInvariant() switch
        {
            "expense" => ("#DC2626", "\u2212"),
            "income" => ("#15803D", "+"),
            _ => ("#0E6B4F", "\u2022")
        };

        var badge = new Border
        {
            WidthRequest = 36,
            HeightRequest = 36,
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb(accent),
            StrokeShape = new Ellipse(),
            VerticalOptions = LayoutOptions.Center
        };
        badge.Content = new Label
        {
            Text = glyph,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var label = new Label
        {
            Text = option,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = ThemeColor("OnSurface", "OnSurfaceDark"),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Fill
        };

        var chevron = new Label
        {
            Text = "\u203A",
            FontSize = 20,
            TextColor = ThemeColor("OnSurfaceVariant", "OnSurfaceVariantDark"),
            VerticalOptions = LayoutOptions.Center
        };

        var grid = new Grid { ColumnSpacing = 12 };
        grid.Add(badge);
        Grid.SetColumn(label, 1);
        grid.Add(label);
        Grid.SetColumn(chevron, 2);
        grid.Add(chevron);

        var row = new Border
        {
            HeightRequest = 56,
            Padding = new Thickness(14, 0),
            Background = ThemeBrush("SurfaceContainerLow", "SurfaceContainerLowDark"),
            Stroke = ThemeBrush("OutlineVariant", "OutlineVariantDark"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Content = grid
        };

        var choice = option;
        row.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => Complete(choice))
        });

        return row;
    }

    private void OnCancelTapped(object? sender, EventArgs e) => Complete(null);

    private void Complete(string? choice)
    {
        var active = _active;
        if (active == null) return;

        _active = null;
        _ = HideAsync();
        active.Completion.TrySetResult(choice);
    }

    private async Task HideAsync()
    {
        await Task.WhenAll(
            Scrim.FadeToAsync(0, 140),
            Card.FadeToAsync(0, 140),
            Card.ScaleToAsync(0.94, 140));
        IsVisible = false;
    }

    private static Color ThemeColor(string lightKey, string darkKey) =>
        ThemeResources.GetColor(lightKey, darkKey);

    private static Brush ThemeBrush(string lightKey, string darkKey) =>
        ThemeResources.GetBrush(lightKey, darkKey);
}
