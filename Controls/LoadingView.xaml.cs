namespace CaisseQuartier.Controls;

/// <summary>État de chargement : indicateur d'activité + message.</summary>
public partial class LoadingView : ContentView
{
    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(LoadingView), "Chargement…");

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public LoadingView()
    {
        InitializeComponent();
    }
}
