using System;

namespace CaisseQuartier.Controls;

public partial class StatusBadge : Border
{
    public static readonly BindableProperty IsLateProperty =
        BindableProperty.Create(nameof(IsLate), typeof(bool), typeof(StatusBadge), false, propertyChanged: OnIsLateChanged);

    public StatusBadge()
    {
        InitializeComponent();
        UpdateContent();
    }

    public bool IsLate
    {
        get => (bool)GetValue(IsLateProperty);
        set => SetValue(IsLateProperty, value);
    }

    private static void OnIsLateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((StatusBadge)bindable).UpdateContent();
    }

    private void UpdateContent()
    {
        var isLate = IsLate;
        StatusLabel.Text = isLate ? "En retard" : "À jour";
        StatusLabel.TextColor = isLate ? Color.FromArgb("#B91C1C") : Color.FromArgb("#166534");
        BackgroundColor = isLate ? Color.FromArgb("#FEE2E2") : Color.FromArgb("#DCFCE7");
        Stroke = isLate ? Color.FromArgb("#FCA5A5") : Color.FromArgb("#86EFAC");
        Indicator.BackgroundColor = isLate ? Color.FromArgb("#DC2626") : Color.FromArgb("#16A34A");
    }
}
