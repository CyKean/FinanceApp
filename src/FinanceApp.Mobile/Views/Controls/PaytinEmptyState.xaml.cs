namespace FinanceApp.Mobile.Views.Controls;

public partial class PaytinEmptyState : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(PaytinEmptyState), "Nothing here yet");

    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(PaytinEmptyState), string.Empty);

    public static readonly BindableProperty IconKeyProperty =
        BindableProperty.Create(nameof(IconKey), typeof(string), typeof(PaytinEmptyState), "tag");

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string IconKey
    {
        get => (string)GetValue(IconKeyProperty);
        set => SetValue(IconKeyProperty, value);
    }

    public PaytinEmptyState()
    {
        InitializeComponent();
    }
}
