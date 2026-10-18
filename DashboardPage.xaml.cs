namespace CaisseQuartier;

public partial class DashboardPage : ContentPage
{
    public DashboardPage()
    {
        InitializeComponent();
    }

    private async void OnCreateContributionClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//NewContributionPage");
    }

    private async void OnMembersClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//MembersPage");
    }

    private async void OnContributionsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//NewContributionPage");
    }

    private async void OnExpensesClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//ExpensesPage");
    }

    private async void OnReportsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//ReportsPage");
    }

    private async void OnRemindersClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//RemindersPage");
    }
}
