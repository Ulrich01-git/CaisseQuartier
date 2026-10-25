namespace CaisseQuartier.Controls;

/// <summary>Liste déroulante aux couleurs de la charte (chevron à droite).</summary>
public partial class PickerField : ContentView
{
    public string Placeholder { get; set; } = "Choisir";

    public event EventHandler? SelectionChanged;

    public string? SelectedItem => Selector.SelectedItem as string;

    public PickerField()
    {
        InitializeComponent();
        UpdateLabel();
    }

    public void SetItems(IReadOnlyList<string> items, int selectedIndex = 0)
    {
        Selector.ItemsSource = items.ToList();
        Selector.SelectedIndex = items.Count > 0 && selectedIndex >= 0 && selectedIndex < items.Count
            ? selectedIndex
            : -1;
        UpdateLabel();
    }

    private void OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        UpdateLabel();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateLabel()
    {
        if (ValueLabel is null) return;

        string? selected = SelectedItem;
        ValueLabel.Text = selected ?? Placeholder;
        ValueLabel.TextColor = Theme.Get(selected is null ? "CqTextMuted" : "CqText");
    }
}
