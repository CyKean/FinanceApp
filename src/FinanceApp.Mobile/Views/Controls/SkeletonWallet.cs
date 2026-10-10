namespace FinanceApp.Mobile.Views.Controls;

/// <summary>
/// Accounts: header, the shared balance hero, the Accounts/History switcher,
/// then account rows and the Add Account button.
/// <para>
/// This one started a whole hero's worth of content too high, because the
/// placeholder opened on the hero card - skipping both the 46pt header above it
/// and the pill switcher below it - and its rows were 11pt shorter than the
/// page's. Every dimension here is taken from AccountsPage.xaml.
/// </para>
/// </summary>
public class SkeletonWallet : SkeletonPage
{
    public SkeletonWallet()
        : base(new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 14, 18, 100) })
    {
    }

    protected override void Populate()
    {
        Host.Children.Add(SkeletonShapes.PageHeader(
            trailing: null,
            titleWidth: 104,
            subtitleWidth: 176));

        Host.Children.Add(SkeletonShapes.BalanceHero());

        Host.Children.Add(SkeletonShapes.SegmentedTabs(count: 2, segmentHeight: 41, activeIndex: 0, labelWidth: 74));

        var rows = SkeletonShapes.VStack(8);
        for (var i = 0; i < 4; i++)
        {
            rows.Add(BuildRow(i));
        }

        Host.Children.Add(rows);

        Host.Children.Add(SkeletonShapes.ActionButton(56));
    }

    /// <summary>
    /// A row is the 52pt icon beside a three-line name/type/balance column
    /// (19 + 3 + 14 + 3 + 20 = 59, which wins), inside 14 of padding.
    /// </summary>
    private static View BuildRow(int index)
    {
        var details = SkeletonShapes.Filling(3,
            SkeletonShapes.Bar(190, 15, width: index % 2 == 0 ? 74 : 58),
            SkeletonShapes.Bar(190, 11, width: 52),
            SkeletonShapes.Bar(190, 16, width: index % 3 == 0 ? 96 : 82));

        // The page's trailing control is a circular overflow button, not text.
        var actions = SkeletonShapes.Circle(38, SkeletonPalette.Surface);
        actions.VerticalOptions = LayoutOptions.Center;

        var content = SkeletonShapes.HStack(12, SkeletonShapes.IconDisc(52), details, actions);

        return SkeletonShapes.Row(87, 24, content, padding: 14);
    }
}