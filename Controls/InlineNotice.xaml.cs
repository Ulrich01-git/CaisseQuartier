namespace CaisseQuartier.Controls;

/// <summary>Bandeau d'information (bleu) ou de confirmation (vert) affiché dans la page.</summary>
public partial class InlineNotice : ContentView
{
    public InlineNotice()
    {
        InitializeComponent();
    }

    public void Show(string text, bool positive = false)
    {
        Caption.Text = text;
        Box.BackgroundColor = Theme.Get(positive ? "CqGreenSoft" : "CqInfoSoft");
        Caption.TextColor = Theme.Get(positive ? "CqGreen" : "CqNavy");
        IsVisible = true;
    }
}
