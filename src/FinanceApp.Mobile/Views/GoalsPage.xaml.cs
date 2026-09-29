namespace FinanceApp.Mobile.Views;

using FinanceApp.Application.DTOs;
using FinanceApp.Mobile.ViewModels;
using Microsoft.Maui;

public partial class GoalsPage : ContentPage
{
    public GoalsPage(GoalsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.AnimateDeleteAsync = AnimateGoalDeletionAsync;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is GoalsViewModel vm && vm.LoadCommand.CanExecute(null))
            await vm.LoadCommand.ExecuteAsync(null);
    }

    private async void OnBackTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//More");
    }

    private async Task AnimateGoalDeletionAsync(FinancialGoalDto goal)
    {
        var container = FindGoalContainer(goal);
        if (container == null)
            return;

        await Task.WhenAll(
            container.FadeToAsync(0, 280, Easing.CubicIn),
            container.ScaleToAsync(0.85, 280, Easing.CubicIn),
            container.TranslateToAsync(-90, 0, 280, Easing.CubicIn));
    }

    private SwipeView? FindGoalContainer(FinancialGoalDto goal)
    {
        foreach (var element in EnumerateVisualTree(this))
        {
            if (element is SwipeView swipe && ReferenceEquals(swipe.BindingContext, goal))
                return swipe;
        }

        return null;
    }

    private static IEnumerable<Element> EnumerateVisualTree(Element root)
    {
        if (root is not IVisualTreeElement node)
            yield break;

        foreach (var child in node.GetVisualChildren())
        {
            if (child is not Element element)
                continue;

            yield return element;

            foreach (var descendant in EnumerateVisualTree(element))
                yield return descendant;
        }
    }
}
