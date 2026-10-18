namespace CaisseQuartier;

public partial class RegisterPage : ContentPage
{
    public RegisterPage()
    {
        InitializeComponent();
    }

    private async void OnCreateAccountClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Compte créé", "Votre compte a bien été créé. Vous pouvez maintenant vous connecter.", "OK");
        await Shell.Current.GoToAsync("//MainPage");
    }

    private async void OnBackToLoginClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//MainPage");
    }
}
