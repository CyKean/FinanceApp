namespace FinanceApp.Mobile.Views.Calendar;

using FinanceApp.Mobile.ViewModels;

public partial class CalendarPage : ContentPage
{
    private static readonly Color Ink = Color.FromArgb("#161B16");
    private static readonly Color Lime = Color.FromArgb("#CDF463");
    private static readonly Color Cream = Color.FromArgb("#EFF3DF");
    private static readonly Color Muted = Color.FromArgb("#9AA393");

    private CalendarViewModel? _vm;

    public CalendarPage(CalendarViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _vm = viewModel;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CalendarViewModel.SelectedMonth)
                or nameof(CalendarViewModel.Events)
                or nameof(CalendarViewModel.SelectedDate))
                BuildMonth();
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is CalendarViewModel vm && vm.LoadCommand.CanExecute(null))
            await vm.LoadCommand.ExecuteAsync(null);

        BuildMonth();
    }

    /// <summary>Creates the day cells for the selected month, with event dots and tap targets.</summary>
    private void BuildMonth()
    {
        if (_vm == null || CalendarGrid == null) return;

        CalendarGrid.Children.Clear();

        var month = _vm.SelectedMonth;
        var first = new DateTime(month.Year, month.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
        var leading = (int)first.DayOfWeek;
        var today = DateTime.Today;

        var eventDays = _vm.Events
            .GroupBy(e => e.Date.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        for (var i = 0; i < leading; i++)
        {
            var spacer = new BoxView { WidthRequest = 0, HeightRequest = 0 };
            Grid.SetRow(spacer, 0);
            Grid.SetColumn(spacer, i);
            CalendarGrid.Children.Add(spacer);
        }

        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = first.AddDays(day - 1);
            eventDays.TryGetValue(date.Date, out var count);

            var isToday = date.Date == today;
            var isSelected = _vm.SelectedDate?.Date == date.Date;
            var isPast = date.Date < today;

            var cell = new Border
            {
                HeightRequest = 44,
                Margin = new Thickness(2),
                BackgroundColor = isSelected ? Lime : isToday ? Color.FromRgba(Lime.Red, Lime.Green, Lime.Blue, 0.35) : Colors.Transparent,
                Stroke = isToday && !isSelected ? new SolidColorBrush(Ink) : Colors.Transparent,
                StrokeThickness = isToday && !isSelected ? 1.5 : 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
                {
                    CornerRadius = new CornerRadius(16)
                }
            };

            var stack = new VerticalStackLayout
            {
                Spacing = 2,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            stack.Add(new Label
            {
                Text = date.Day.ToString(),
                FontSize = 13,
                FontAttributes = isSelected || isToday ? FontAttributes.Bold : FontAttributes.None,
                TextColor = isPast ? Muted : Ink,
                HorizontalTextAlignment = TextAlignment.Center
            });

            if (count > 0)
            {
                stack.Add(new Border
                {
                    WidthRequest = 6,
                    HeightRequest = 6,
                    BackgroundColor = isPast ? Muted : Ink,
                    StrokeThickness = 0,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                    HorizontalOptions = LayoutOptions.Center
                });
            }

            cell.Content = stack;

            var captured = date;
            cell.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = _vm.SelectDateCommand,
                CommandParameter = captured
            });

            // Grid does not auto-place programmatically added children, so set the cell explicitly.
            var slot = leading + day - 1;
            Grid.SetRow(cell, slot / 7);
            Grid.SetColumn(cell, slot % 7);
            CalendarGrid.Children.Add(cell);
        }
    }

    private async void OnBackTapped(object? sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync(".."); }
        catch { await Shell.Current.GoToAsync("//Main/More"); }
    }

    /// <summary>Adds a transaction dated on the selected day.</summary>
    private async void OnAddForDayTapped(object? sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("//AddTransactionSheet?type=Expense");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not open the transaction form: {ex.Message}");
        }
    }

    /// <summary>Calendar entries are transactions, so editing happens in Transactions.</summary>
    private async void OnEditEventTapped(object? sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("//Main/Transactions"); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not open transactions: {ex.Message}");
        }
    }
}