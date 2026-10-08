namespace FinanceApp.Mobile.Views.Controls;

using FinanceApp.Mobile.Helpers;

public partial class FinoraTabBar : ContentView
{
    public static readonly BindableProperty ActiveTabProperty =
        BindableProperty.Create(nameof(ActiveTab), typeof(string), typeof(FinoraTabBar), "Dashboard",
            propertyChanged: (b, _, _) => ((FinoraTabBar)b).Refresh());

    public string ActiveTab
    {
        get => (string)GetValue(ActiveTabProperty);
        set => SetValue(ActiveTabProperty, value);
    }

    private static Color Lime => FinoraOverlay.Resolve("FinoraLime", "#CDF463");
    private static Color Ink => FinoraOverlay.Resolve("FinoraInk", "#161B16");
    private static Color IdleCircle => FinoraOverlay.Resolve("FinoraInkSoft", "#2C332C");
    private static Color IdleIcon => FinoraOverlay.Resolve("MutedSoft", "#9AA393");

    public FinoraTabBar()
    {
        InitializeComponent();
        IconDashboard.Data = FinoraIcons.GetGeometry("grid");
        IconTransactions.Data = FinoraIcons.GetGeometry("swap");
        IconAccounts.Data = FinoraIcons.GetGeometry("wallet");
        IconBudgets.Data = FinoraIcons.GetGeometry("pie");
        IconMore.Data = FinoraIcons.GetGeometry("sliders");
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
