namespace CaisseQuartier.Controls;

/// <summary>Champ de formulaire : libellé, saisie, états normal / actif / erreur.</summary>
public partial class FormField : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(FormField), string.Empty);

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(FormField), string.Empty);

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(FormField), string.Empty,
            BindingMode.TwoWay);

    public static readonly BindableProperty IsPasswordProperty =
        BindableProperty.Create(nameof(IsPassword), typeof(bool), typeof(FormField), false);

    public static readonly BindableProperty ErrorTextProperty =
        BindableProperty.Create(nameof(ErrorText), typeof(string), typeof(FormField), string.Empty,
            propertyChanged: (b, _, _) => ((FormField)b).UpdateVisual());

    public static readonly BindableProperty SuffixProperty =
        BindableProperty.Create(nameof(Suffix), typeof(string), typeof(FormField), string.Empty,
            propertyChanged: (b, _, _) => ((FormField)b).UpdateSuffix());

    public static readonly BindableProperty IsNumericProperty =
        BindableProperty.Create(nameof(IsNumeric), typeof(bool), typeof(FormField), false,
            propertyChanged: (b, _, _) => ((FormField)b).UpdateKeyboard());

    private bool _focused;

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsPassword { get => (bool)GetValue(IsPasswordProperty); set => SetValue(IsPasswordProperty, value); }
    public string ErrorText { get => (string)GetValue(ErrorTextProperty) ?? string.Empty; set => SetValue(ErrorTextProperty, value); }
    public string Suffix { get => (string)GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }
    public bool IsNumeric { get => (bool)GetValue(IsNumericProperty); set => SetValue(IsNumericProperty, value); }

    public event EventHandler<TextChangedEventArgs>? TextChanged;

    public FormField()
    {
        InitializeComponent();
        UpdateVisual();
    }

    private void OnInputFocused(object? sender, FocusEventArgs e)
    {
        _focused = true;
        UpdateVisual();
    }

    private void OnInputUnfocused(object? sender, FocusEventArgs e)
    {
        _focused = false;
        UpdateVisual();
    }

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e) =>
        TextChanged?.Invoke(this, e);

    private void UpdateSuffix()
    {
        if (SuffixLabel is null) return;
        SuffixLabel.Text = Suffix;
        SuffixLabel.IsVisible = !string.IsNullOrEmpty(Suffix);
    }

    private void UpdateKeyboard()
    {
        if (Input is null) return;
        Input.Keyboard = IsNumeric ? Keyboard.Numeric : Keyboard.Default;
    }

    /// <summary>Normal : bordure grise. Actif : bordure bleue. Erreur : bordure rouge + message.</summary>
    private void UpdateVisual()
    {
        if (InputBorder is null || ErrorRow is null) return;

        bool hasError = !string.IsNullOrWhiteSpace(ErrorText);
        ErrorRow.IsVisible = hasError;

        if (hasError)
        {
            InputBorder.Stroke = new SolidColorBrush(Theme.Get("CqRed"));
            InputBorder.StrokeThickness = 1.5;
        }
        else if (_focused)
        {
            InputBorder.Stroke = new SolidColorBrush(Theme.Get("CqNavy"));
            InputBorder.StrokeThickness = 1.5;
        }
        else
        {
            InputBorder.Stroke = new SolidColorBrush(Theme.Get("CqBorder"));
            InputBorder.StrokeThickness = 1;
        }
    }
}
