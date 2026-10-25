namespace CaisseQuartier.Controls;

/// <summary>
/// Bouton de la charte : « Primary » (bleu plein) ou « Secondary » (contour).
/// L'état désactivé est géré automatiquement via IsEnabled.
/// </summary>
public partial class ActionButton : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(ActionButton), string.Empty,
            propertyChanged: (b, _, _) => ((ActionButton)b).Refresh());

    public static readonly BindableProperty IconSourceProperty =
        BindableProperty.Create(nameof(IconSource), typeof(ImageSource), typeof(ActionButton), default(ImageSource),
            propertyChanged: (b, _, _) => ((ActionButton)b).Refresh());

    public static readonly BindableProperty KindProperty =
        BindableProperty.Create(nameof(Kind), typeof(string), typeof(ActionButton), "Primary",
            propertyChanged: (b, _, _) => ((ActionButton)b).Refresh());

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public ImageSource? IconSource { get => (ImageSource?)GetValue(IconSourceProperty); set => SetValue(IconSourceProperty, value); }
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    public event EventHandler? Clicked;

    public ActionButton()
    {
        InitializeComponent();
        Refresh();
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == IsEnabledProperty.PropertyName) Refresh();
    }

    private void Refresh()
    {
        if (Surface is null) return;

        bool primary = Kind == "Primary";

        Caption.Text = Text;
        IconView.Source = IconSource;
        IconView.IsVisible = IconSource is not null;

        if (!IsEnabled)
        {
            Surface.BackgroundColor = Theme.Get("CqDisabledBg");
            Surface.Stroke = new SolidColorBrush(Theme.Get("CqDisabledBg"));
            Caption.TextColor = Theme.Get("CqDisabledText");
        }
        else if (primary)
        {
            Surface.BackgroundColor = Theme.Get("CqNavy");
            Surface.Stroke = new SolidColorBrush(Theme.Get("CqNavy"));
            Caption.TextColor = Colors.White;
        }
        else
        {
            Surface.BackgroundColor = Theme.Get("CqSurface");
            Surface.Stroke = new SolidColorBrush(Theme.Get("CqBorder"));
            Caption.TextColor = Theme.Get("CqNavy");
        }
    }

    private async void OnTapped(object? sender, TappedEventArgs e)
    {
        if (!IsEnabled) return;

        await Surface.FadeToAsync(0.8, 60);
        await Surface.FadeToAsync(1, 90);
        Clicked?.Invoke(this, EventArgs.Empty);
    }
}
