namespace CaisseQuartier.Controls;

/// <summary>Ligne de menu de l'accueil : icône, libellé et chevron.</summary>
public partial class MenuRow : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(MenuRow), string.Empty);

    public static readonly BindableProperty IconSourceProperty =
        BindableProperty.Create(nameof(IconSource), typeof(ImageSource), typeof(MenuRow), default(ImageSource));

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public ImageSource? IconSource { get => (ImageSource?)GetValue(IconSourceProperty); set => SetValue(IconSourceProperty, value); }

    public event EventHandler? Clicked;

    public MenuRow()
    {
        InitializeComponent();
    }

    private void OnTapped(object? sender, TappedEventArgs e) =>
        Clicked?.Invoke(this, EventArgs.Empty);
}
