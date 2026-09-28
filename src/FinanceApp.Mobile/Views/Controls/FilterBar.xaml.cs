namespace FinanceApp.Mobile.Views.Controls;

using System.Windows.Input;
using FinanceApp.Domain.Enums;

public partial class FilterBar : ContentView
{
    public static readonly BindableProperty StartDateProperty =
        BindableProperty.Create(nameof(StartDate), typeof(DateTime?), typeof(FilterBar), null,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly BindableProperty EndDateProperty =
        BindableProperty.Create(nameof(EndDate), typeof(DateTime?), typeof(FilterBar), null,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly BindableProperty SelectedTypeProperty =
        BindableProperty.Create(nameof(SelectedType), typeof(TransactionType?), typeof(FilterBar), null,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly BindableProperty ApplyCommandProperty =
        BindableProperty.Create(nameof(ApplyCommand), typeof(ICommand), typeof(FilterBar));

    public static readonly BindableProperty ClearCommandProperty =
        BindableProperty.Create(nameof(ClearCommand), typeof(ICommand), typeof(FilterBar));

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

    public FilterBar()
    {
        InitializeComponent();
    }
}
