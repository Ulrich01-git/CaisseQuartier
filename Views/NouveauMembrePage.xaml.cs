using System.Text.RegularExpressions;
using CaisseQuartier.Controls;
using CaisseQuartier.Helpers;
using CaisseQuartier.Models;
using CaisseQuartier.Services;

namespace CaisseQuartier.Views;

public partial class NouveauMembrePage : ContentPage
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex =
        new(@"^(?:\+?237[\s.-]?)?[236]\d{2}[\s.-]?\d{3}[\s.-]?\d{3}$", RegexOptions.Compiled);

    public NouveauMembrePage()
    {
        InitializeComponent();
        foreach (var field in new[] { NameField, ContactField, ContributionField })
            field.TextChanged += OnFormChanged;
        RefreshValidation();
    }

    private void OnFormChanged(object? sender, TextChangedEventArgs e) => RefreshValidation();

    private void RefreshValidation()
    {
        bool contactValid = EmailRegex.IsMatch(ContactField.Text?.Trim() ?? string.Empty) ||
                            PhoneRegex.IsMatch(ContactField.Text?.Trim() ?? string.Empty);
        bool contributionValid = string.IsNullOrWhiteSpace(ContributionField.Text) ||
                                 (Money.TryParse(ContributionField.Text, out decimal amount) &&
                                  amount >= 0 && decimal.Truncate(amount) == amount);
        SaveButton.IsEnabled = NameField.Text?.Trim().Length >= 2 && contactValid && contributionValid;
        ContactField.ErrorText = ContactField.Text?.Length > 0 && !contactValid
            ? "Saisissez un email ou un numéro camerounais valide."
            : string.Empty;
        ContributionField.ErrorText = !contributionValid
            ? "Saisissez un montant entier en FCFA."
            : string.Empty;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (!SaveButton.IsEnabled || !Money.TryParse(ContributionField.Text, out decimal contribution))
            return;
        try
        {
            SaveButton.IsEnabled = false;
            string contact = ContactField.Text!.Trim();
            bool isEmail = EmailRegex.IsMatch(contact);
            var member = await ApiService.Shared.AddMemberAsync(new MemberCreateRequest(
                NameField.Text!.Trim(),
                contact,
                EmptyToNull(PhoneField.Text) ?? (isEmail ? null : contact),
                EmptyToNull(EmailField.Text) ?? (isEmail ? contact : null),
                EmptyToNull(NeighborhoodField.Text),
                EmptyToNull(OccupationField.Text),
                DateOnly.FromDateTime(DateTime.Today),
                contribution));

            string code = ApiService.Shared.CurrentAssociation?.JoinCode ?? string.Empty;
            await DisplayAlertAsync("Profil créé",
                $"{member.FullName} est maintenant visible par les membres de l'association.\n\n" +
                $"Code à communiquer pour activer son compte : {code}",
                "Continuer");
            var parameters = new Dictionary<string, object> { ["id"] = member.Id.ToString("D") };
            await Shell.Current.GoToAsync(nameof(MembreDetailPage), parameters);
        }
        catch (ApiException exception)
        {
            Notice.Show(exception.Message);
            RefreshValidation();
        }
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async void OnBackClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
