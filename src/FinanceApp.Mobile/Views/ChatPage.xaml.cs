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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // The view-model is transient today, so this was not a live leak - but
        // registering it as a singleton would have turned it into one.
        _viewModel.MessagesChanged -= OnMessagesChanged;
        _viewModel.FocusRequested -= OnFocusRequested;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _viewModel.MessagesChanged -= OnMessagesChanged;
        _viewModel.MessagesChanged += OnMessagesChanged;
        _viewModel.FocusRequested -= OnFocusRequested;
        _viewModel.FocusRequested += OnFocusRequested;

        if (_viewModel.LoadCommand.CanExecute(null))
            await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private void OnMessagesChanged(object? sender, EventArgs e)
    {
        var last = _viewModel.Messages.LastOrDefault();
        if (last is null) return;

        // ScrollTo throws if the collection view is not realised, which is
        // exactly the state it is in when a reply lands after the user left.
        try
        {
            MessagesCollectionView.ScrollTo(last, position: ScrollToPosition.End, animate: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not scroll chat: {ex.Message}");
        }
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
