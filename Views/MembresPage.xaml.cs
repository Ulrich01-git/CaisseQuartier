using CaisseQuartier.Controls;
using CaisseQuartier.Models;
using CaisseQuartier.Services;

namespace CaisseQuartier.Views;

public partial class MembresPage : ContentPage
{
    private IReadOnlyList<MemberProfile> _members = [];

    public MembresPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        Loading.IsVisible = true;
        ListSection.IsVisible = false;
        EmptyState.IsVisible = false;
        try
        {
            _members = await ApiService.Shared.GetMembersAsync();
            Render();
        }
        catch (ApiException exception)
        {
            Notice.Show(exception.Message);
            EmptyState.Title = "Annuaire indisponible";
            EmptyState.Message = "Vérifiez la connexion au serveur de l'association, puis réessayez.";
            EmptyState.IsVisible = true;
        }
        finally
        {
            Loading.IsVisible = false;
        }
    }

    private void Render()
    {
        string query = SearchEntry.Text?.Trim() ?? string.Empty;
        var filtered = _members
            .Where(member => member.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             member.Contact.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             (member.Phone?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();

        if (filtered.Count == 0)
        {
            EmptyState.Title = _members.Count == 0 ? "Aucun profil pour le moment" : "Aucun résultat";
            EmptyState.Message = _members.Count == 0
                ? "Ajoutez un membre. Son profil sera partagé avec l'association."
                : $"Aucun membre ne correspond à « {query} ».";
            EmptyState.IsVisible = true;
            ListSection.IsVisible = false;
            return;
        }

        EmptyState.IsVisible = false;
        int late = _members.Count(member => member.IsLate);
        SummaryLabel.Text = $"{_members.Count} membres · {_members.Count - late} à jour · {late} à régulariser";

        var stack = new VerticalStackLayout { Spacing = 0 };
        for (int index = 0; index < filtered.Count; index++)
            stack.Add(RowFactory.MemberRow(filtered[index], index > 0, OnMemberTapped));
        ListCard.Content = stack;
        ListSection.IsVisible = true;
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (!Loading.IsVisible) Render();
    }

    private async void OnAddClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(NouveauMembrePage));

    private async void OnMemberTapped(MemberProfile member)
    {
        var parameters = new Dictionary<string, object> { ["id"] = member.Id.ToString("D") };
        await Shell.Current.GoToAsync(nameof(MembreDetailPage), parameters);
    }

    private async void OnBackClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
