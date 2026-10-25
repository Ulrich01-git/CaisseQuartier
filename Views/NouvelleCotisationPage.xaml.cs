using CaisseQuartier.Helpers;
using CaisseQuartier.Models;
using CaisseQuartier.Services;

namespace CaisseQuartier.Views;

public partial class NouvelleCotisationPage : ContentPage
{
    private IReadOnlyList<MemberProfile> _members = [];

    public NouvelleCotisationPage()
    {
        InitializeComponent();
        DateInput.Date = DateTime.Today;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _members = await ApiService.Shared.GetMembersAsync();
            MemberField.SetItems(_members.Select(member => member.FullName).ToList(), 0);
            Validate();
        }
        catch (ApiException exception)
        {
            Notice.Show(exception.Message);
        }
    }

    private void OnMemberChanged(object? sender, EventArgs e) => Validate();

    private void OnAmountChanged(object? sender, TextChangedEventArgs e) => Validate();

    /// <summary>« Valider » reste désactivé tant que le membre ou le montant est invalide.</summary>
    private bool Validate()
    {
        string text = AmountField.Text?.Trim() ?? string.Empty;
        bool amountOk = Money.TryParse(text, out decimal amount) &&
                        amount > 0 && decimal.Truncate(amount) == amount;

        AmountField.ErrorText = text.Length > 0 && !amountOk
            ? "Saisissez un montant entier en FCFA supérieur à 0."
            : string.Empty;

        bool ok = MemberField.SelectedItem is not null && amountOk;
        ValidateButton.IsEnabled = ok;
        return ok;
    }

    private async void OnValidateClicked(object? sender, EventArgs e)
    {
        if (!Validate() || MemberField.SelectedItem is not string name) return;

        if (!Money.TryParse(AmountField.Text, out decimal amount)) return;
        var member = _members.FirstOrDefault(candidate => candidate.FullName == name);
        if (member is null) return;
        try
        {
            ValidateButton.IsEnabled = false;
            await ApiService.Shared.AddContributionAsync(new TransactionCreateRequest(
                member.Id, "Cotisation", null, amount, DateOnly.FromDateTime(DateInput.Date)));
            Notice.Show($"Cotisation de {Money.Fcfa(amount)} enregistrée pour {name} et partagée.", true);
            await Task.Delay(700);
            await Shell.Current.GoToAsync("..");
        }
        catch (ApiException exception)
        {
            Notice.Show(exception.Message);
            Validate();
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
