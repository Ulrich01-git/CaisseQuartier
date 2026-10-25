using CaisseQuartier.Controls;
using CaisseQuartier.Services;

namespace CaisseQuartier.Views;

public partial class ConnexionPage : ContentPage
{
    public ConnexionPage()
    {
        InitializeComponent();
        ServerField.Text = ApiService.Shared.ServerUrl;
    }

    // Efface le message d'erreur dès que l'utilisateur corrige sa saisie
    private void OnFieldChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is FormField field && field.ErrorText.Length > 0)
            field.ErrorText = string.Empty;
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        string id = IdField.Text?.Trim() ?? string.Empty;
        string password = PasswordField.Text ?? string.Empty;
        if (id.Length == 0 || password.Length == 0)
        {
            Notice.Show("Saisissez votre téléphone/email et votre mot de passe.");
            return;
        }

        try
        {
            ApiService.Shared.ConfigureServer(ServerField.Text ?? string.Empty);
            LoginButton.IsEnabled = false;
            await ApiService.Shared.LoginAsync(id, password);
            await Shell.Current.GoToAsync("//AccueilPage");
        }
        catch (ApiException exception)
        {
            Notice.Show(exception.Message);
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private async void OnCreateAccountTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync($"//{nameof(InscriptionPage)}");
}
