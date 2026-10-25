using CaisseQuartier.Controls;
using CaisseQuartier.Helpers;
using CaisseQuartier.Models;
using CaisseQuartier.Services;

namespace CaisseQuartier.Views;

public partial class AssociationPage : ContentPage
{
    public AssociationPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var association = await ApiService.Shared.GetAssociationAsync();
            NameField.Text = association.Name;
            PurposeField.Text = association.Purpose;
            GoalField.Text = association.MonthlyGoal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            JoinCodeLabel.Text = association.JoinCode;
            bool isAdmin = ApiService.Shared.CurrentMember?.Role == "admin";
            NameField.IsEnabled = isAdmin;
            PurposeField.IsEnabled = isAdmin;
            GoalField.IsEnabled = isAdmin;
            SaveButton.IsVisible = isAdmin;
            RoleLabel.Text = isAdmin
                ? "Vous pouvez modifier le projet et son objectif."
                : "Seul un administrateur peut modifier le projet commun.";
        }
        catch (ApiException exception)
        {
            Notice.Show(exception.Message);
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (!Money.TryParse(GoalField.Text, out decimal goal) || goal < 0 ||
            decimal.Truncate(goal) != goal ||
            (NameField.Text?.Trim().Length ?? 0) < 3 ||
            (PurposeField.Text?.Trim().Length ?? 0) < 8)
        {
            Notice.Show("Complétez le nom, l'objectif et le montant mensuel.");
            return;
        }

        try
        {
            SaveButton.IsEnabled = false;
            var association = await ApiService.Shared.UpdateAssociationAsync(new AssociationUpdateRequest(
                NameField.Text!.Trim(), PurposeField.Text!.Trim(), goal));
            Notice.Show($"Projet « {association.Name} » enregistré.", true);
        }
        catch (ApiException exception)
        {
            Notice.Show(exception.Message);
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
