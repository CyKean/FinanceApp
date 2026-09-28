namespace FinanceApp.Mobile.Views.Controls;

public partial class PageHeader : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(PageHeader), string.Empty);

    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(PageHeader), string.Empty);

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

    public PageHeader()
    {
        InitializeComponent();
    }
}
