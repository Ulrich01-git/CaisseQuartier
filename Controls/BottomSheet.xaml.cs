namespace CaisseQuartier.Controls;

/// <summary>
/// Feuille d'ajout qui monte du bas de l'écran.
/// </summary>
public partial class BottomSheet : ContentView
{
    private View? _body;

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(BottomSheet), string.Empty);

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public View? Body
    {
        get => _body;
        set
        {
            _body = value;
            if (BodyHost is not null)
                BodyHost.Content = value;
        }
    }

    public BottomSheet()
    {
        InitializeComponent();
        BodyHost.Content = _body;
    }

    public void Open() => IsVisible = true;

    public void Close() => IsVisible = false;

    private void OnBackdropTapped(object? sender, TappedEventArgs e) => Close();
}
