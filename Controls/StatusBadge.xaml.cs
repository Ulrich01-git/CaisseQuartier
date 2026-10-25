namespace CaisseQuartier.Controls;

/// <summary>Pastille de statut : « À jour » (vert) ou « En retard » (rouge).</summary>
public partial class StatusBadge : ContentView
{
    public static readonly BindableProperty IsLateProperty =
        BindableProperty.Create(nameof(IsLate), typeof(bool), typeof(StatusBadge), false,
            propertyChanged: (b, _, _) => ((StatusBadge)b).Apply());

    public bool IsLate
    {
        get => (bool)GetValue(IsLateProperty);
        set => SetValue(IsLateProperty, value);
    }

    public StatusBadge()
    {
        InitializeComponent();
        Apply();
    }

    private void Apply()
    {
        if (Pill is null) return;

        string strong = IsLate ? "CqRed" : "CqGreen";
        string soft = IsLate ? "CqRedSoft" : "CqGreenSoft";

        Pill.BackgroundColor = Theme.Get(soft);
        Dot.Fill = new SolidColorBrush(Theme.Get(strong));
        Caption.TextColor = Theme.Get(strong);
        Caption.Text = IsLate ? "En retard" : "À jour";
    }
}
