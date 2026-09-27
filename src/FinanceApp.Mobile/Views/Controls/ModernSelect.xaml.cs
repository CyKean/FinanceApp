namespace FinanceApp.Mobile.Views.Controls;

using System.Collections;
using Microsoft.Maui.Controls.Shapes;

/// <summary>
/// Modern replacement for Picker: a field that expands an inline option list
/// (avatar + label + checkmark) instead of opening the native dialog.
/// </summary>
public partial class ModernSelect : ContentView
{
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
        _isOpen = !_isOpen;
        DropBorder.IsVisible = _isOpen;
        ChevronLabel.Text = _isOpen ? "⌃" : "⌄";
        if (_isOpen)
            Rebuild();
    }

    private string? ReadMember(object? item, string? path)
    {
        if (item == null || string.IsNullOrEmpty(path))
            return null;
        return item.GetType().GetProperty(path)?.GetValue(item)?.ToString();
    }

    private string GetDisplayText(object? item)
    {
        if (item == null)
            return Placeholder;
        return ReadMember(item, DisplayMemberPath) ?? item.ToString() ?? Placeholder;
    }

    private void Rebuild()
    {
        if (DisplayLabel == null || OptionsContainer == null || DropBorder == null)
            return;

        var selected = SelectedItem;
        DisplayLabel.Text = GetDisplayText(selected);
        DisplayLabel.TextColor = selected == null
            ? Color.FromArgb("#94A3B8")
            : Color.FromArgb("#0F172A");

        OptionsContainer.Children.Clear();
        var items = ItemsSource;
        if (items == null || items.Count == 0)
        {
            OptionsContainer.Children.Add(new Label
            {
                Text = "No options yet",
                FontSize = 13,
                TextColor = Color.FromArgb("#94A3B8"),
                Margin = new Thickness(12, 8)
            });
            return;
        }

        var hasIcon = !string.IsNullOrEmpty(IconMemberPath);
        foreach (var item in items)
        {
            var captured = item;
            var isSelected = Equals(item, selected);
            var row = new HorizontalStackLayout { Spacing = 12, Padding = new Thickness(10, 9) };

            if (hasIcon)
            {
                var badge = new Border
                {
                    WidthRequest = 36,
                    HeightRequest = 36,
                    StrokeThickness = 0,
                    StrokeShape = new Ellipse(),
                    VerticalOptions = LayoutOptions.Center
                };
                var colorHex = ReadMember(item, ColorMemberPath);
                try
                {
                    badge.BackgroundColor = !string.IsNullOrEmpty(colorHex)
                        ? Color.FromArgb(colorHex)
                        : Color.FromArgb("#E2E8F0");
                }
                catch
                {
                    badge.BackgroundColor = Color.FromArgb("#E2E8F0");
                }
                badge.Content = new Label
                {
                    Text = ReadMember(item, IconMemberPath) ?? "•",
                    FontSize = 16,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };
                row.Children.Add(badge);
            }

            row.Children.Add(new Label
            {
                Text = GetDisplayText(item),
                FontSize = 15,
                TextColor = Color.FromArgb("#0F172A"),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Fill
            });

            row.Children.Add(new Label
            {
                Text = "✓",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#0E6B4F"),
                VerticalOptions = LayoutOptions.Center,
                IsVisible = isSelected
            });

            var tapRow = row;
            tapRow.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    SelectedItem = captured;
                    _isOpen = false;
                    DropBorder.IsVisible = false;
                    ChevronLabel.Text = "⌄";
                })
            });
            OptionsContainer.Children.Add(row);
        }
    }
}
