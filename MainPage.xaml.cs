namespace CaisseQuartier;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var identifier = IdentifierField?.Text ?? string.Empty;
        var password = PasswordField?.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlertAsync("Connexion", "Veuillez renseigner votre identifiant et votre mot de passe.", "OK");
            return;
        }

        await Shell.Current.GoToAsync("//DashboardPage");
    }

    private async void OnCreateAccountClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//RegisterPage");
    }

    private async void OnForgotPasswordTapped(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Mot de passe oublié", "Contactez l'administrateur pour réinitialiser votre accès.", "OK");
    }
}