namespace CaisseQuartier.Controls;

/// <summary>État vide : icône, titre et message explicatif.</summary>
public partial class EmptyStateView : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(EmptyStateView), string.Empty);

    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(EmptyStateView), string.Empty);

    public static readonly BindableProperty IconSourceProperty =
        BindableProperty.Create(nameof(IconSource), typeof(ImageSource), typeof(EmptyStateView), default(ImageSource),
            propertyChanged: (b, _, _) => ((EmptyStateView)b).Apply());

    /// <summary>true = présentation « positive » (vert), ex. « Aucun retard, bravo ! ».</summary>
    public static readonly BindableProperty IsPositiveProperty =
        BindableProperty.Create(nameof(IsPositive), typeof(bool), typeof(EmptyStateView), false,
            propertyChanged: (b, _, _) => ((EmptyStateView)b).Apply());

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public ImageSource? IconSource { get => (ImageSource?)GetValue(IconSourceProperty); set => SetValue(IconSourceProperty, value); }
    public bool IsPositive { get => (bool)GetValue(IsPositiveProperty); set => SetValue(IsPositiveProperty, value); }

    public EmptyStateView()
    {
        InitializeComponent();
        Apply();
    }

    private void Apply()
    {
        if (IconImage is null) return;

        IconImage.Source = IconSource;
        IconBox.BackgroundColor = Theme.Get(IsPositive ? "CqGreenSoft" : "CqInfoSoft");
        TitleLabel.TextColor = Theme.Get(IsPositive ? "CqGreen" : "CqNavy");
    }
}
