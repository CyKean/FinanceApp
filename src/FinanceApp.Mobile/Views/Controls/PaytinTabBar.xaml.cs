namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Mobile.Helpers;

public partial class PaytinTabBar : ContentView
{
    public static readonly BindableProperty ActiveTabProperty =
        BindableProperty.Create(nameof(ActiveTab), typeof(string), typeof(PaytinTabBar), "Dashboard",
            propertyChanged: (b, _, _) => ((PaytinTabBar)b).Refresh());

    public string ActiveTab
    {
        get => (string)GetValue(ActiveTabProperty);
        set => SetValue(ActiveTabProperty, value);
    }

    private static readonly Color Lime = Color.FromArgb("#CDF463");
    private static readonly Color Ink = Color.FromArgb("#161B16");
    private static readonly Color IdleCircle = Color.FromArgb("#2C332C");
    private static readonly Color IdleIcon = Color.FromArgb("#9AA393");

    public PaytinTabBar()
    {
        InitializeComponent();
        IconDashboard.Data = PaytinIcons.GetGeometry("grid");
        IconTransactions.Data = PaytinIcons.GetGeometry("swap");
        IconAccounts.Data = PaytinIcons.GetGeometry("wallet");
        IconBudgets.Data = PaytinIcons.GetGeometry("pie");
        IconMore.Data = PaytinIcons.GetGeometry("sliders");
        Refresh();
    }

    private void Refresh()
    {
        Paint(TabDashboard, IconDashboard, "Dashboard");
        Paint(TabTransactions, IconTransactions, "Transactions");
        Paint(TabAccounts, IconAccounts, "Accounts");
        Paint(TabBudgets, IconBudgets, "Budgets");
        Paint(TabMore, IconMore, "More");
    }

    private void Paint(Border tab, Microsoft.Maui.Controls.Shapes.Path icon, string name)
    {
        var active = string.Equals(ActiveTab, name, StringComparison.OrdinalIgnoreCase);
        tab.Background = active ? Lime : IdleCircle;
        icon.Stroke = active ? Ink : IdleIcon;
    }

    private async void OnTabTapped(object? sender, TappedEventArgs e)
    {
        var tab = e.Parameter as string;
        if (string.IsNullOrWhiteSpace(tab) ||
            string.Equals(tab, ActiveTab, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            await Shell.Current.GoToAsync($"//Main/{tab}");
        }
        catch
        {
            // Already there or route unavailable — stay put.
        }
    }
}
