using CaisseQuartier.Controls;
using CaisseQuartier.Helpers;
using CaisseQuartier.Models;
using CaisseQuartier.Services;

namespace CaisseQuartier.Views;

public partial class AccueilPage : ContentPage
{
    private int _loadVersion;

    public AccueilPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        int loadVersion = ++_loadVersion;
        BalanceLoading.IsVisible = true;
        BalanceValue.IsVisible = false;
        ContributionsValue.Text = "—";
        ExpensesValue.Text = "—";
        EmptyHint.IsVisible = false;
        try
        {
            var dashboard = await ApiService.Shared.GetDashboardAsync();
            if (loadVersion != _loadVersion) return;

            var monthName = DateTime.Today.ToString("MMMM yyyy",
                System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));
            monthName = char.ToUpper(monthName[0]) + monthName[1..];
            AssociationLabel.Text = dashboard.Association.Name;
            PurposeLabel.Text = dashboard.Association.Purpose;
            PeriodLabel.Text = $"{monthName} · Montants en FCFA";
            MemberWelcome.Text = "Bonjour " + (ApiService.Shared.CurrentMember?.FullName.Split(' ')[0] ?? "membre");

            BalanceValue.Text = Money.Fcfa(dashboard.Balance);
            BalanceValue.TextColor = Theme.Get(dashboard.Balance < 0 ? "CqRed" : "CqGreen");
            ContributionsValue.Text = Money.Fcfa(dashboard.Contributions);
            ExpensesValue.Text = Money.Fcfa(dashboard.Expenses);
            GoalCaption.Text = $"{Money.Fcfa(dashboard.Contributions)} / {Money.Fcfa(dashboard.Goal)}";
            GoalProgress.Progress = dashboard.Goal <= 0
                ? 0
                : (double)Math.Clamp(dashboard.Contributions / dashboard.Goal, 0, 1);
            EmptyHint.IsVisible = dashboard.Contributions == 0 && dashboard.Expenses == 0;
            WelcomeNotice.IsVisible = false;
        }
        catch (ApiException exception)
        {
            WelcomeNotice.Show(exception.Message);
            BalanceValue.Text = "—";
            GoalCaption.Text = "Objectif non disponible";
        }
        finally
        {
            if (loadVersion == _loadVersion)
            {
                BalanceLoading.IsVisible = false;
                BalanceValue.IsVisible = true;
            }
        }
    }

    private async void OnAssociationClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(AssociationPage));

    private async void OnProfileClicked(object? sender, EventArgs e)
    {
        if (ApiService.Shared.CurrentMember is not { } member) return;
        var parameters = new Dictionary<string, object> { ["id"] = member.Id.ToString("D") };
        await Shell.Current.GoToAsync(nameof(MembreDetailPage), parameters);
    }

    private async void OnMembresClicked(object? sender, EventArgs e) =>
        await NavigateAsync(nameof(MembresPage));

    private async void OnCotisationClicked(object? sender, EventArgs e) =>
        await NavigateAsync(nameof(NouvelleCotisationPage));

    private async void OnDepensesClicked(object? sender, EventArgs e) =>
        await NavigateAsync(nameof(DepensesPage));

    private async void OnRapportClicked(object? sender, EventArgs e) =>
        await NavigateAsync(nameof(RapportPage));

    private async void OnRelancesClicked(object? sender, EventArgs e) =>
        await NavigateAsync(nameof(RelancesPage));

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        bool logout = await DisplayAlertAsync("Déconnexion", "Voulez-vous fermer votre session ?", "Oui", "Non");
        if (!logout) return;
        try
        {
            await ApiService.Shared.LogoutAsync();
        }
        catch (ApiException exception)
        {
            WelcomeNotice.Show(exception.Message);
            return;
        }
        await Shell.Current.GoToAsync("//WelcomePage");
    }

    private static async Task NavigateAsync(string route)
    {
        try
        {
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation vers {route} impossible : {exception}");
            await Shell.Current.DisplayAlertAsync("Navigation impossible", exception.Message, "Fermer");
        }
    }
}
