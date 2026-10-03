namespace FinanceApp.Mobile.Views.Controls;

using System.Globalization;
using System.Windows.Input;
using FinanceApp.Domain.Enums;
using FinanceApp.Mobile.Helpers;

public partial class FilterBar : ContentView
{
    private const string NoFilterSummary = "All transactions";

    private static readonly Color Lime = Color.FromArgb("#CDF463");
    private static readonly Color Ink = Color.FromArgb("#161B16");
    private static readonly Color Muted = Color.FromArgb("#6F7668");

    public static readonly BindableProperty StartDateProperty =
        BindableProperty.Create(nameof(StartDate), typeof(DateTime?), typeof(FilterBar), null,
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: (b, _, _) => ((FilterBar)b).SyncState());

    public static readonly BindableProperty EndDateProperty =
        BindableProperty.Create(nameof(EndDate), typeof(DateTime?), typeof(FilterBar), null,
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: (b, _, _) => ((FilterBar)b).SyncState());

    public static readonly BindableProperty SelectedTypeProperty =
        BindableProperty.Create(nameof(SelectedType), typeof(TransactionType?), typeof(FilterBar), null,
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: (b, _, _) => ((FilterBar)b).SyncState());

    public static readonly BindableProperty ApplyCommandProperty =
        BindableProperty.Create(nameof(ApplyCommand), typeof(ICommand), typeof(FilterBar));

    public static readonly BindableProperty ClearCommandProperty =
        BindableProperty.Create(nameof(ClearCommand), typeof(ICommand), typeof(FilterBar));

    public static readonly BindableProperty IsExpandedProperty =
        BindableProperty.Create(nameof(IsExpanded), typeof(bool), typeof(FilterBar), false);

    public static readonly BindableProperty ActiveSummaryProperty =
        BindableProperty.Create(nameof(ActiveSummary), typeof(string), typeof(FilterBar), NoFilterSummary);

    public static readonly BindableProperty HasActiveFiltersProperty =
        BindableProperty.Create(nameof(HasActiveFilters), typeof(bool), typeof(FilterBar), false);

    public DateTime? StartDate
    {
        get => (DateTime?)GetValue(StartDateProperty);
        set => SetValue(StartDateProperty, value);
    }

    public DateTime? EndDate
    {
        get => (DateTime?)GetValue(EndDateProperty);
        set => SetValue(EndDateProperty, value);
    }

    public TransactionType? SelectedType
    {
        get => (TransactionType?)GetValue(SelectedTypeProperty);
        set => SetValue(SelectedTypeProperty, value);
    }

    public ICommand? ApplyCommand
    {
        get => (ICommand?)GetValue(ApplyCommandProperty);
        set => SetValue(ApplyCommandProperty, value);
    }

    public ICommand? ClearCommand
    {
        get => (ICommand?)GetValue(ClearCommandProperty);
        set => SetValue(ClearCommandProperty, value);
    }

    /// <summary>False keeps the panel collapsed to a single summary row.</summary>
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public string ActiveSummary
    {
        get => (string)GetValue(ActiveSummaryProperty);
        set => SetValue(ActiveSummaryProperty, value);
    }

    public bool HasActiveFilters
    {
        get => (bool)GetValue(HasActiveFiltersProperty);
        set => SetValue(HasActiveFiltersProperty, value);
    }

    public ICommand ToggleCommand { get; }

    public FilterBar()
    {
        // Must precede InitializeComponent: the XAML binds ToggleCommand while
        // parsing, and a plain CLR property never re-resolves once it is null.
        ToggleCommand = new Command(() => IsExpanded = !IsExpanded);
        InitializeComponent();
        FilterIcon.Data = PaytinIcons.GetGeometry("funnel");
        SyncState();
    }

    private void OnTypeTapped(object? sender, TappedEventArgs e)
    {
        SelectedType = e.Parameter as string switch
        {
            "income" => TransactionType.Income,
            "expense" => TransactionType.Expense,
            _ => null
        };
    }

    private void SyncState()
    {
        if (TypeAllPill is null)
            return;

        var range = DescribeRange();
        var type = SelectedType?.ToString();

        ActiveSummary = range is null && type is null
            ? NoFilterSummary
            : string.Join(" · ", new[] { range, type }.Where(p => p is not null));

        HasActiveFilters = range is not null || type is not null;

        PaintPill(TypeAllPill, TypeAllLabel, SelectedType is null);
        PaintPill(TypeIncomePill, TypeIncomeLabel, SelectedType == TransactionType.Income);
        PaintPill(TypeExpensePill, TypeExpenseLabel, SelectedType == TransactionType.Expense);
    }

    private static void PaintPill(Border pill, Label label, bool selected)
    {
        pill.Background = selected ? Lime : Colors.Transparent;
        label.TextColor = selected ? Ink : Muted;
    }

    private string? DescribeRange()
    {
        if (StartDate.HasValue && EndDate.HasValue)
            return string.Format(CultureInfo.InvariantCulture, "{0:MMM d} – {1:MMM d}", StartDate.Value, EndDate.Value);

        if (StartDate.HasValue)
            return string.Format(CultureInfo.InvariantCulture, "From {0:MMM d}", StartDate.Value);

        if (EndDate.HasValue)
            return string.Format(CultureInfo.InvariantCulture, "To {0:MMM d}", EndDate.Value);

        return null;
    }
}
