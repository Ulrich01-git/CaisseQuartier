using System.Globalization;

namespace CaisseQuartier;

public partial class NewContributionPage : ContentPage
{
    public NewContributionPage()
    {
        InitializeComponent();
        ContributionDate.Date = DateTime.Today;
    }

    private async void OnValidateClicked(object sender, EventArgs e)
    {
        var amountText = AmountField.Text?.Trim();

        if (string.IsNullOrWhiteSpace(amountText))
        {
            AmountField.ErrorText = "Le montant est obligatoire.";
            ValidationMessage.Text = "Veuillez saisir un montant valide.";
            ValidationMessage.IsVisible = true;
            return;
        }

        if (!decimal.TryParse(amountText.Replace(" ", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
        {
            AmountField.ErrorText = "Le montant doit être supérieur à zéro.";
            ValidationMessage.Text = "Le montant saisi est invalide.";
            ValidationMessage.IsVisible = true;
            return;
        }

        AmountField.ErrorText = string.Empty;
        ValidationMessage.IsVisible = false;
        await DisplayAlertAsync("Cotisation enregistrée", $"La cotisation de {amount:N0} FCFA a bien été enregistrée.", "OK");
        await Shell.Current.GoToAsync("//DashboardPage");
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//DashboardPage");
    }
}
