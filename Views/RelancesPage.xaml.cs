using CaisseQuartier.Controls;
using CaisseQuartier.Helpers;
using CaisseQuartier.Models;
using CaisseQuartier.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace CaisseQuartier.Views;

public partial class RelancesPage : ContentPage
{
    private IReadOnlyList<MemberProfile> _lateMembers = [];

    public RelancesPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        PeriodCaption.Text = $"Situation du mois · {DateTime.Today:MMMM yyyy} · FCFA";
        Loading.IsVisible = true;
        ListSection.IsVisible = false;
        EmptyState.IsVisible = false;
        try
        {
            var members = await ApiService.Shared.GetMembersAsync();
            _lateMembers = members.Where(member => member.IsLate).ToList();
            Render();
        }
        catch (ApiException exception)
        {
            Notice.Show(exception.Message);
            EmptyState.Title = "Relances indisponibles";
            EmptyState.Message = "Vérifiez la connexion au serveur de l'association.";
            EmptyState.IsVisible = true;
        }
        finally
        {
            Loading.IsVisible = false;
        }
    }

    private void Render()
    {
        if (_lateMembers.Count == 0)
        {
            EmptyState.Title = "Aucun retard, bravo !";
            EmptyState.Message = "Tous les membres sont à jour sur la période.";
            EmptyState.IsVisible = true;
            ListSection.IsVisible = false;
            return;
        }

        EmptyState.IsVisible = false;
        DueLabel.Text = $"{Money.Fcfa(_lateMembers.Sum(member => member.Due))} à régulariser";
        CountLabel.Text = _lateMembers.Count == 1
            ? "1 membre à relancer"
            : $"{_lateMembers.Count} membres à relancer";
        Cards.Clear();
        foreach (var member in _lateMembers)
            Cards.Add(RowFactory.RelanceCard(member, OnSend));
        ListSection.IsVisible = true;
    }

    private async void OnSend(MemberProfile member, ActionButton button)
    {
        string phone = NormalizePhone(member.Phone ?? member.Contact);
        if (phone.Length < 8)
        {
            Notice.Show($"Aucun numéro de téléphone valide n'est enregistré pour {member.FullName}.");
            return;
        }
        string message = $"Bonjour {member.FullName}, il reste {Money.Fcfa(member.Due)} à régulariser " +
                         $"pour la cotisation de {DateTime.Today:MMMM yyyy} de notre association. Merci !";
        try
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = "Partager la relance",
                Text = $"{message}\n\nTéléphone du membre : {phone}"
            });
            Notice.Show("Relance prête à être partagée. Choisissez WhatsApp ou SMS.", true);
        }
        catch (ApiException exception)
        {
            Notice.Show(exception.Message);
        }
    }

    private static string NormalizePhone(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 9 && digits[0] is '2' or '3' or '6')
            return "237" + digits;
        return digits;
    }

    private async void OnBackClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
