namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Mobile.Helpers;
using FinanceApp.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

/// <summary>
/// Bottom sheet of choices, used for the "Add Transaction" expense/income
/// prompt. Rows use the same ink badge and Lucide glyph as the rest of the app.
/// </summary>
public partial class ChoiceSheetHost : ContentView
{
    private const double SlideIn = 60;

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

    private void OnChoiceRequested(ChoiceSheetRequest request) =>
        MainThread.BeginInvokeOnMainThread(() => Show(request));

    private void Show(ChoiceSheetRequest request)
    {
        // A new prompt supersedes the old one: settle it so its awaiter returns.
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
        Card.TranslationY = SlideIn;

        _ = Scrim.FadeToAsync(1, FinoraOverlay.ScrimFadeMs, Easing.CubicOut);
        _ = Card.FadeToAsync(1, FinoraOverlay.CardInMs, Easing.CubicOut);
        _ = Card.TranslateToAsync(0, 0, FinoraOverlay.CardInMs, Easing.CubicOut);
    }

    /// <summary>Accent, Lucide key and destructive flag for a known option.</summary>
    private static (string Accent, string Glyph, bool Destructive) ResolveOption(string option) =>
        option.Trim().ToLowerInvariant() switch
        {
            "expense" => ("#DC2626", "trenddown", false),
            "income" => ("#CDF463", "trendup", false),
            "edit" => ("#CDF463", "pencil", false),
            "delete" => ("#DC2626", "trash", true),
            "activate" => ("#CDF463", "check", false),
            "deactivate" => ("#F59E0B", "alert", false),
            "add" => ("#CDF463", "plus", false),
            "save" => ("#CDF463", "check", false),
            _ => ("#CDF463", "dots", false)
        };

    private View CreateOptionRow(string option)
    {
        var (accent, glyph, destructive) = ResolveOption(option);
        var accentColor = FinoraOverlay.Resolve(accent, accent);

        var badge = new Border
        {
            WidthRequest = 36,
            HeightRequest = 36,
            StrokeThickness = 0,
            BackgroundColor = accentColor,
            StrokeShape = new Ellipse(),
            VerticalOptions = LayoutOptions.Center
        };
        badge.Content = new Path
        {
            Aspect = Microsoft.Maui.Controls.Stretch.Uniform,
            WidthRequest = 18,
            HeightRequest = 18,
            Data = FinoraIcons.GetGeometry(glyph),
            Stroke = IsDarkAccent(accentColor) ? FinoraOverlay.Lime : FinoraOverlay.Ink,
            StrokeThickness = 2,
            StrokeLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = Brush.Transparent,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var label = new Label
        {
            Text = option,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = destructive ? accentColor : FinoraOverlay.Ink,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Fill
        };

        // Columns must be declared: assigning a column index on a Grid without
        // ColumnDefinitions silently falls back to the implicit single column
        // and stacks the children on top of each other.
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 12
        };
        grid.Add(badge);
        Grid.SetColumn(label, 1);
        grid.Add(label);

        var row = new Border
        {
            HeightRequest = 56,
            Padding = new Thickness(14, 0),
            Background = Colors.Transparent,
            Stroke = FinoraOverlay.Ink,
            StrokeThickness = 1.5,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Content = grid
        };

        var choice = option;
        row.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => Complete(choice))
        });

        return row;
    }

    /// <summary>Lime reads on the dark accents; ink on the lime ones.</summary>
    private static bool IsDarkAccent(Color color)
    {
        // Color.Red/Green/Blue are already normalised to 0..1, so this must not
        // be scaled by 255 again or every accent reads as dark.
        var luminance = 0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue;
        return luminance < 0.55;
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
            Scrim.FadeToAsync(0, FinoraOverlay.CardOutMs, Easing.CubicIn),
            Card.FadeToAsync(0, FinoraOverlay.CardOutMs, Easing.CubicIn),
            Card.TranslateToAsync(0, SlideIn, FinoraOverlay.CardOutMs, Easing.CubicIn));
        IsVisible = false;
    }
}
