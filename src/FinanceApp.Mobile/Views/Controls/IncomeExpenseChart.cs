namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Domain.ValueObjects;

public class IncomeExpenseChart : GraphicsView
{
    public static readonly BindableProperty IncomeProperty =
        BindableProperty.Create(nameof(Income), typeof(Money), typeof(IncomeExpenseChart), null,
            propertyChanged: OnChartChanged);

    public static readonly BindableProperty ExpenseProperty =
        BindableProperty.Create(nameof(Expense), typeof(Money), typeof(IncomeExpenseChart), null,
            propertyChanged: OnChartChanged);

    public static readonly BindableProperty NetProperty =
        BindableProperty.Create(nameof(Net), typeof(Money), typeof(IncomeExpenseChart), null,
            propertyChanged: OnChartChanged);

    public Money? Income
    {
        get => (Money?)GetValue(IncomeProperty);
        set => SetValue(IncomeProperty, value);
    }

    public Money? Expense
    {
        get => (Money?)GetValue(ExpenseProperty);
        set => SetValue(ExpenseProperty, value);
    }

    public Money? Net
    {
        get => (Money?)GetValue(NetProperty);
        set => SetValue(NetProperty, value);
    }

    private readonly IncomeExpenseDrawable _drawable = new();

    public IncomeExpenseChart()
    {
        Drawable = _drawable;
    }

    private static void OnChartChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var chart = (IncomeExpenseChart)bindable;
        chart._drawable.Income = chart.Income;
        chart._drawable.Expense = chart.Expense;
        chart._drawable.Net = chart.Net;
        chart.Invalidate();
    }
}

internal sealed class IncomeExpenseDrawable : IDrawable
{
    private static readonly Color IncomeColor = Color.FromArgb("#15803D");
    private static readonly Color ExpenseColor = Color.FromArgb("#DC2626");

    public Money? Income { get; set; }
    public Money? Expense { get; set; }
    public Money? Net { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        decimal income = Income?.Amount ?? 0;
        decimal expense = Expense?.Amount ?? 0;
        decimal net = Net?.Amount ?? 0;

        double max = Math.Max((double)income, Math.Max((double)expense, Math.Abs((double)net)));
        if (max <= 0)
            max = 1;

        var rows = new[]
        {
            new BarRow("Income", (float)((double)income / max), income.ToString("N2"), IncomeColor),
            new BarRow("Expenses", (float)((double)expense / max), expense.ToString("N2"), ExpenseColor),
            new BarRow("Net", (float)(Math.Abs((double)net) / max), net.ToString("N2"),
                net >= 0 ? IncomeColor : ExpenseColor)
        };

        BarRowsPainter.Draw(canvas, dirtyRect, rows);
    }
}
