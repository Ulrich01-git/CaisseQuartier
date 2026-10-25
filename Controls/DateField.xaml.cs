using System.Globalization;

namespace CaisseQuartier.Controls;

/// <summary>Champ date aux couleurs de la charte (jj/mm/aaaa + icône calendrier).</summary>
public partial class DateField : ContentView
{
    private bool _isSyncing;

    public static readonly BindableProperty DateProperty =
        BindableProperty.Create(nameof(Date), typeof(DateTime), typeof(DateField), DateTime.Today,
            BindingMode.TwoWay,
            propertyChanged: (b, _, n) => ((DateField)b).Sync((DateTime)n));

    public DateTime Date
    {
        get => (DateTime)GetValue(DateProperty);
        set => SetValue(DateProperty, value);
    }

    /// <summary>Déclenché uniquement quand l'utilisateur choisit une date.</summary>
    public event EventHandler<DateTime>? DateChanged;

    public DateField()
    {
        InitializeComponent();
        Sync(Date);
    }

    private void Sync(DateTime day)
    {
        if (DateLabel is null || Picker is null) return;

        DateLabel.Text = day.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        try
        {
            _isSyncing = true;
            if (Picker.Date != day.Date) Picker.Date = day.Date;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void OnDateSelected(object? sender, DateChangedEventArgs e)
    {
        DateTime? chosen = e.NewDate;
        if (_isSyncing || chosen is null || Date.Date == chosen.Value.Date) return;

        Date = chosen.Value;
        DateChanged?.Invoke(this, chosen.Value);
    }
}
