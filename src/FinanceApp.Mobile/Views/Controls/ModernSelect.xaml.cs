namespace FinanceApp.Mobile.Views.Controls;

using System.Collections;
using FinanceApp.Mobile.Helpers;
using Microsoft.Maui.Controls.Shapes;

/// <summary>
/// Modern replacement for Picker: a field that expands an inline option list
/// (badge + label + checkmark) instead of opening the native dialog.
/// </summary>
public partial class ModernSelect : ContentView
{
    private static Color Ink => FinoraOverlay.Resolve("FinoraInk", "#161B16");
    private static Color Muted => FinoraOverlay.Resolve("FinoraMuted", "#6F7668");
    private static Color Selection => FinoraOverlay.Resolve("FinoraCreamDeep", "#E4EACB");

    /// <summary>Badge fill, matching FinoraIconView's default ink circle.</summary>
    private const string BadgeBackground = "#161B16";

    /// <summary>Glyph colour on the ink badge.</summary>
    private static readonly Color OnBadge = Colors.White;

    private const double BadgeSize = 34;

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IList), typeof(ModernSelect), null,
            propertyChanged: (b, _, _) => ((ModernSelect)b)?.Rebuild());

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(ModernSelect), null,
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: (b, _, _) => ((ModernSelect)b)?.Rebuild());

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(ModernSelect), "Select",
            propertyChanged: (b, _, _) => ((ModernSelect)b)?.Rebuild());

    public static readonly BindableProperty DisplayMemberPathProperty =
        BindableProperty.Create(nameof(DisplayMemberPath), typeof(string), typeof(ModernSelect), null,
            propertyChanged: (b, _, _) => ((ModernSelect)b)?.Rebuild());

    public static readonly BindableProperty IconMemberPathProperty =
        BindableProperty.Create(nameof(IconMemberPath), typeof(string), typeof(ModernSelect), null,
            propertyChanged: (b, _, _) => ((ModernSelect)b)?.Rebuild());

    public static readonly BindableProperty ColorMemberPathProperty =
        BindableProperty.Create(nameof(ColorMemberPath), typeof(string), typeof(ModernSelect), null,
            propertyChanged: (b, _, _) => ((ModernSelect)b)?.Rebuild());

    public IList? ItemsSource
    {
        get => (IList?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string? DisplayMemberPath
    {
        get => (string?)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    public string? IconMemberPath
    {
        get => (string?)GetValue(IconMemberPathProperty);
        set => SetValue(IconMemberPathProperty, value);
    }

    public string? ColorMemberPath
    {
        get => (string?)GetValue(ColorMemberPathProperty);
        set => SetValue(ColorMemberPathProperty, value);
    }

    private bool _isOpen;

    public ModernSelect()
    {
        InitializeComponent();
        Rebuild();
    }

    private void OnFieldTapped(object? sender, EventArgs e)
    {
        SetOpen(!_isOpen);
        if (_isOpen)
            Rebuild();
    }

    private void SetOpen(bool open)
    {
        _isOpen = open;
        DropBorder.IsVisible = open;
        ChevronPath.Rotation = open ? 180 : 0;
    }

    private string? ReadMember(object? item, string? path)
    {
        if (item is null || string.IsNullOrEmpty(path))
            return null;

        return item.GetType().GetProperty(path)?.GetValue(item)?.ToString();
    }

    private string GetDisplayText(object? item)
    {
        if (item is null)
            return Placeholder;

        return ReadMember(item, DisplayMemberPath) ?? item.ToString() ?? Placeholder;
    }

    /// <summary>
    /// Maps an item to a Lucide key. The stored value is usually an emoji, so it
    /// goes through <see cref="FinoraIcons.Resolve"/>, which maps known emoji and
    /// otherwise matches the item's name.
    /// </summary>
    private string ResolveIconKey(object? item, string? name) =>
        FinoraIcons.Resolve(item is null ? null : ReadMember(item, IconMemberPath), name);

    /// <summary>
    /// Builds a Lucide stroke glyph. <paramref name="directKey"/> bypasses
    /// <see cref="ResolveIconKey"/> for fixed glyphs such as the selection check.
    /// </summary>
    private Path CreateIconGlyph(object? item, string? name, double size, string? directKey = null, Color? stroke = null) => new()
    {
        Aspect = Microsoft.Maui.Controls.Stretch.Uniform,
        WidthRequest = size,
        HeightRequest = size,
        Data = FinoraIcons.GetGeometry(directKey ?? ResolveIconKey(item, name)),
        Stroke = stroke ?? OnBadge,
        StrokeThickness = 1.9,
        StrokeLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        Fill = Brush.Transparent,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center
    };

    private void Rebuild()
    {
        if (DisplayLabel is null || OptionsContainer is null || DropBorder is null)
            return;

        var selected = SelectedItem;

        DisplayLabel.Text = GetDisplayText(selected);
        DisplayLabel.TextColor = selected is null ? Muted : Ink;

        // Mirror the selected option's badge in the closed field so the choice
        // is identifiable without expanding the list.
        var hasIcon = !string.IsNullOrEmpty(IconMemberPath);
        var showBadge = hasIcon && selected is not null;

        FieldBadge.IsVisible = showBadge;
        if (showBadge)
            FieldBadgeGlyph.Data = FinoraIcons.GetGeometry(ResolveIconKey(selected, GetDisplayText(selected)));

        OptionsContainer.Children.Clear();

        var items = ItemsSource;
        if (items is null || items.Count == 0)
        {
            OptionsContainer.Children.Add(new Label
            {
                Text = "No options yet",
                FontSize = 13,
                TextColor = Muted,
                Margin = new Thickness(12, 10)
            });
            return;
        }

        foreach (var item in items)
            OptionsContainer.Children.Add(CreateOptionRow(item, selected));
    }

    private View CreateOptionRow(object item, object? selected)
    {
        var captured = item;
        var isSelected = Equals(item, selected);
        var hasIcon = !string.IsNullOrEmpty(IconMemberPath);

        // The columns must exist: assigning a column index on a Grid with no
        // ColumnDefinitions silently falls back to column 0, which stacks the
        // badge, label and checkmark on top of each other.
        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        var badgeColumn = 0;

        if (hasIcon)
        {
            content.Children.Add(new Border
            {
                WidthRequest = BadgeSize,
                HeightRequest = BadgeSize,
                StrokeThickness = 0,
                BackgroundColor = Color.FromArgb(BadgeBackground),
                StrokeShape = new Ellipse(),
                VerticalOptions = LayoutOptions.Center,
                Content = CreateIconGlyph(item, GetDisplayText(item), 18)
            });
            badgeColumn = 1;
        }

        var label = new Label
        {
            Text = GetDisplayText(item),
            FontSize = 15,
            FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None,
            TextColor = Ink,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        Grid.SetColumn(label, badgeColumn);
        content.Children.Add(label);

        // Reserve the trailing column even when nothing is selected, so labels
        // do not shift as the selection changes.
        var check = CreateIconGlyph(null, null, 16, directKey: "check");
        Grid.SetColumn(check, badgeColumn + 1);
        check.IsVisible = isSelected;
        content.Children.Add(check);

        var row = new Border
        {
            Padding = new Thickness(10, 8),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Background = isSelected ? Selection : Brush.Transparent,
            Content = content
        };

        row.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() =>
            {
                SelectedItem = captured;
                SetOpen(false);
            })
        });

        return row;
    }
}
