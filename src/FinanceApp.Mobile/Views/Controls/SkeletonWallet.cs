namespace FinanceApp.Mobile.Views.Controls;

/// <summary>
/// Wallet: a total-balance card across the top, then account rows that carry a
/// trailing balance. The page's history view is a separate tab, so the skeleton
/// is for the account list.
/// </summary>
public class SkeletonWallet : SkeletonPage
{
    public SkeletonWallet()
        : base(new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 14, 18, 100) })
    {
    }

    protected override void Populate()
    {
        Host.Children.Add(BuildTotalCard());

        for (var i = 0; i < 5; i++)
        {
            Host.Children.Add(BuildRow(i));
        }
    }

    private static View BuildTotalCard()
    {
        var card = SkeletonShapes.Card(104, radius: 20);
        card.Content = SkeletonShapes.VStack(10,
            SkeletonShapes.Bar(190, 11, width: 84),
            SkeletonShapes.Bar(190, 26, width: 168),
            SkeletonShapes.Bar(190, 10, width: 112));

        return card;
    }

    private static View BuildRow(int index)
    {
        var content = SkeletonShapes.HStack(12,
            // The page uses a rounded square account badge, not a circle.
            SkeletonShapes.Slab(44, width: 44, radius: 14),
            SkeletonShapes.Filling(
                SkeletonShapes.Bar(190, 12, width: index % 3 == 2 ? 112 : 146),
                SkeletonShapes.Bar(190, 10, width: index % 2 == 0 ? 74 : 96)),
            SkeletonShapes.Trailing(
                SkeletonShapes.Bar(190, 12, width: 72),
                SkeletonShapes.Bar(190, 9, width: 52)));

        return SkeletonShapes.Row(76, 18, content);
    }
}