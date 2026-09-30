namespace FinanceApp.Mobile.Views.Controls;

using System.Windows.Input;

public partial class FabButton : ContentView
{
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(FabButton));

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(FabButton));

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(FabButton), "+");

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public FabButton()
    {
        InitializeComponent();
    }

    private async void OnTapped(object? sender, TappedEventArgs e)
    {
        await FabBorder.ScaleToAsync(0.88, 70, Easing.CubicOut);
        await FabBorder.ScaleToAsync(1.0, 160, Easing.SpringOut);
    }
}
