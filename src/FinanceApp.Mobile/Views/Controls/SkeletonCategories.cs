namespace FinanceApp.Mobile.Views.Controls;

using Microsoft.Maui.Controls.Shapes;
using static FinanceApp.Mobile.Views.Controls.SkeletonShapes;

/// <summary>
/// Categories: header, the Expenses/Income switcher, then category rows and the
/// Add Category button.
/// <para>
/// The trailing control used to be drawn as a switch, on the strength of an
/// assumption in this file's own comment. The page has no toggle - it opens a
/// circular overflow menu. The rows also carry an optional "Inactive" caption,
/// so the detail column is ragged rather than a fixed pair of lines.
/// </para>
/// </summary>
public class SkeletonCategories : SkeletonPage
{
    public SkeletonCategories()
        : base(new VerticalStackLayout { Spacing = 14, Padding = new Thickness(18, 14, 18, 24) })
    {
    }

    protected override void Populate()
    {
        Host.Children.Add(SkeletonShapes.PageHeader(
            trailing: SkeletonShapes.ActionPill(60, 34),
            titleWidth: 96,
            subtitleWidth: 168));

        Host.Children.Add(SkeletonShapes.SegmentedTabs(count: 2, segmentHeight: 46, activeIndex: 0, labelWidth: 68));

        var rows = SkeletonShapes.VStack(16);
        for (var i = 0; i < 6; i++)
        {
            rows.Add(BuildRow(i));
        }

        Host.Children.Add(rows);

        Host.Children.Add(SkeletonShapes.ActionButton(56));
    }

    /// <summary>
    /// The row is 46 of icon inside 12 of padding, and the page's trailing
    /// control is a 35pt rounded overflow button.
    /// </summary>
    private static View BuildRow(int index)
    {
        // The detail column is the category name plus, for a deactivated row, a
        // warning caption - so some rows are two lines and some are one.
        var details = index % 3 == 0
            ? SkeletonShapes.Filling(4,
                SkeletonShapes.Bar(190, 15, width: index % 2 == 0 ? 96 : 74),
                SkeletonShapes.Bar(190, 12, width: 66))
            : SkeletonShapes.Filling(4,
                SkeletonShapes.Bar(190, 15, width: index % 2 == 0 ? 112 : 88));

        var overflow = Slab(35, width: 38, fill: SkeletonPalette.Block, radius: 12);
        overflow.VerticalOptions = LayoutOptions.Center;

        var content = SkeletonShapes.HStack(14, SkeletonShapes.IconDisc(46), details, overflow);

        return SkeletonShapes.Row(70, 22, content, padding: 14);
    }
}