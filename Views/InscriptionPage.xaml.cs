using System.Text.RegularExpressions;
using CaisseQuartier.Controls;
using CaisseQuartier.Helpers;
using CaisseQuartier.Models;
using CaisseQuartier.Services;

namespace CaisseQuartier.Views;

public partial class InscriptionPage : ContentPage
{
    private static readonly string[] RegistrationModes =
        ["Créer une association", "Rejoindre avec un code"];
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex =
        new(@"^(?:\+?237[\s.-]?)?[236]\d{2}[\s.-]?\d{3}[\s.-]?\d{3}$", RegexOptions.Compiled);

    public InscriptionPage()
    {
        InitializeComponent();
        ServerField.Text = ApiService.Shared.ServerUrl;
        RegistrationModePicker.ItemsSource = RegistrationModes;
        RegistrationModePicker.SelectedIndex = 0;
        Refresh();
    }

    private void OnFieldChanged(object? sender, TextChangedEventArgs e)
    {
        Notice.IsVisible = false;
        Refresh();
    }
    private void OnRegistrationModeChanged(object? sender, EventArgs e)
    {
        if (RegistrationModePicker.SelectedIndex < 0) return;
        Notice.IsVisible = false;
        JoinCodeField.Text = string.Empty;
        bool joining = RegistrationModePicker.SelectedIndex == 1;
        JoinAssociationSection.IsVisible = joining;
        CreateAssociationSection.IsVisible = !joining;
        RegistrationModeHint.Text = joining
            ? "Choisissez cette option uniquement si un administrateur vous a déjà communiqué son code."
            : "Choisissez cette option pour créer l'association et obtenir un nouveau code à partager.";
        Refresh();
    }

    /// <summary>
    /// Validation en direct : le bouton « S'inscrire » reste désactivé tant que le
    /// formulaire est incomplet ou contient une erreur.
    /// </summary>
    private void Refresh()
    {
        string name = NameField.Text?.Trim() ?? string.Empty;
        string contact = ContactField.Text?.Trim() ?? string.Empty;
        string password = PasswordField.Text ?? string.Empty;
        string associationName = AssociationNameField.Text?.Trim() ?? string.Empty;
        string purpose = PurposeField.Text?.Trim() ?? string.Empty;
        bool joining = RegistrationModePicker.SelectedIndex == 1;
        bool goalOk = joining ||
                      (Money.TryParse(MonthlyGoalField.Text, out decimal goal) &&
                       goal > 0 && decimal.Truncate(goal) == goal);

        bool contactOk = EmailRegex.IsMatch(contact) || PhoneRegex.IsMatch(contact);
        bool passwordOk = password.Length >= 8;

        ContactField.ErrorText = contact.Length > 0 && !contactOk
            ? "Saisissez un email ou un numéro camerounais valide (ex. +237 6XX XXX XXX)."
            : string.Empty;

        PasswordField.ErrorText = password.Length > 0 && !passwordOk
            ? "Le mot de passe doit contenir au moins 8 caractères."
            : string.Empty;

        bool associationOk = joining
            ? !string.IsNullOrWhiteSpace(JoinCodeField.Text)
            : associationName.Length >= 3 && purpose.Length >= 8 && goalOk;
        bool hasError = ContactField.ErrorText.Length > 0 || PasswordField.ErrorText.Length > 0;
        bool complete = name.Length >= 2 && contactOk && passwordOk && associationOk;

        SubmitButton.IsEnabled = complete;
        HelperLabel.IsVisible = !complete;
        HelperLabel.Text = hasError
            ? "Corrigez le champ indiqué pour continuer."
            : joining
                ? "Ce compte rejoindra l'association correspondant au code saisi."
                : "Le code n'est pas nécessaire pour créer une association. Vous recevrez le vôtre après l'inscription.";
    }

    private async void OnSubmitClicked(object? sender, EventArgs e)
    {
        if (!SubmitButton.IsEnabled) return;
        string contact = ContactField.Text?.Trim() ?? string.Empty;
        string password = PasswordField.Text ?? string.Empty;
        Money.TryParse(MonthlyGoalField.Text, out decimal goal);
        try
        {
            ApiService.Shared.ConfigureServer(ServerField.Text ?? string.Empty);
            SubmitButton.IsEnabled = false;
            var request = new RegisterRequest(
                NameField.Text!.Trim(),
                contact,
                password,
                RegistrationModePicker.SelectedIndex == 1 ? JoinCodeField.Text.Trim() : null,
                RegistrationModePicker.SelectedIndex == 0 ? AssociationNameField.Text?.Trim() : null,
                RegistrationModePicker.SelectedIndex == 0 ? PurposeField.Text?.Trim() : null,
                RegistrationModePicker.SelectedIndex == 0 ? goal : 0);
            await ApiService.Shared.RegisterAsync(request);
            await Shell.Current.GoToAsync("//AccueilPage");
        }
        catch (ApiException exception)
        {
            Notice.Show(exception.Message);
            Refresh();
        }
    }

    private async void OnLoginTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync($"//{nameof(ConnexionPage)}");
}
