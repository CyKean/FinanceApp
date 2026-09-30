namespace FinanceApp.Mobile.Views;

using FinanceApp.Mobile.ViewModels;

public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel _viewModel;

    public ChatPage(ChatViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _viewModel.MessagesChanged += OnMessagesChanged;
        _viewModel.FocusRequested += OnFocusRequested;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.LoadCommand.CanExecute(null))
            await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private void OnMessagesChanged(object? sender, EventArgs e)
    {
        var last = _viewModel.Messages.LastOrDefault();
        if (last is null) return;

        MessagesCollectionView.ScrollTo(last, position: ScrollToPosition.End, animate: true);
    }

    private void OnFocusRequested(object? sender, EventArgs e)
    {
        _ = ComposerEditor.Focus();
    }

    private async void OnBackTapped(object? sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not pop chat: {ex.Message}");
            await Shell.Current.GoToAsync("//Main/More");
        }
    }
}
