using System.Globalization;
using CaisseQuartier.Controls;
using CaisseQuartier.Helpers;
using CaisseQuartier.Models;
using CaisseQuartier.Services;

namespace CaisseQuartier.Views;

[QueryProperty(nameof(MemberKey), "id")]
public partial class MembreDetailPage : ContentPage
{
    private Guid _memberId;
    private MemberProfile? _profile;

    public string MemberKey
    {
        set
        {
            if (Guid.TryParse(value, out var id))
                _memberId = id;
        }
    }

    public MembreDetailPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_memberId == Guid.Empty)
        {
            Notice.Show("L'identifiant du profil est invalide.");
            return;
        }

        try
        {
            _profile = await ApiService.Shared.GetMemberAsync(_memberId);
            NameField.Text = _profile.FullName;
            PhoneField.Text = _profile.Phone ?? string.Empty;
            EmailField.Text = _profile.Email ?? string.Empty;
            NeighborhoodField.Text = _profile.Neighborhood ?? string.Empty;
            OccupationField.Text = _profile.Occupation ?? string.Empty;
            ContributionField.Text = _profile.MonthlyContribution.ToString(CultureInfo.InvariantCulture);
            ProfileTitle.Text = _profile.FullName;
            ProfileStatus.Text = _profile.IsLate ? "Cotisation du mois à régulariser" : "Membre de l'association";
            ProfileStatus.TextColor = Theme.Get(_profile.IsLate ? "CqRed" : "CqGreen");
            ContactLabel.Text = "Contact du compte : " + _profile.Contact;
            JoinedLabel.Text = $"Membre depuis le {_profile.JoinedOn:dd/MM/yyyy}";
            PaidLabel.Text = $"Cotisé ce mois : {Money.Fcfa(_profile.PaidThisMonth)} · Reste : {Money.Fcfa(_profile.DueThisMonth)}";

            bool canEdit = _profile.CanEdit;
            foreach (var field in new[] { NameField, PhoneField, EmailField, NeighborhoodField, OccupationField, ContributionField })
                field.IsEnabled = canEdit;
            PhotoButton.IsVisible = canEdit;
            SaveButton.IsVisible = canEdit;
            var photo = await ApiService.Shared.GetMemberPhotoAsync(_memberId);
            if (photo is not null) ProfilePhoto.Source = photo;

            var from = DateOnly.FromDateTime(DateTime.Today.AddMonths(-12));
            var transactions = await ApiService.Shared.GetTransactionsAsync(from, DateOnly.FromDateTime(DateTime.Today), "contribution");
            var contributions = transactions.Where(item => item.MemberId == _memberId).Take(8).ToList();
            var activity = new VerticalStackLayout { Spacing = 0 };
            if (contributions.Count == 0)
                activity.Add(new Label
                {
                    Text = "Aucune cotisation enregistrée sur les 12 derniers mois.",
                    Style = (Style)Application.Current!.Resources["CaptionText"]
                });
            else
                for (int index = 0; index < contributions.Count; index++)
                    activity.Add(RowFactory.ExpenseRow(contributions[index], index > 0));
            ActivityCard.Content = activity;
        }
        catch (ApiException exception)
        {
            Notice.Show(exception.Message);
        }
    }

    private async void OnPhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choisir une photo de profil",
                FileTypes = FilePickerFileType.Images
            });
            if (file is null) return;

            await using var stream = await file.OpenReadAsync();
            await using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            if (buffer.Length > 5_000_000)
            {
                Notice.Show("Choisissez une photo de moins de 5 Mo.");
                return;
            }
            byte[] photoBytes = buffer.ToArray();
            var contentType = Path.GetExtension(file.FileName).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                _ => string.Empty
            };
            if (contentType.Length == 0)
            {
                Notice.Show("Choisissez une image JPEG ou PNG.");
                return;
            }

            await ApiService.Shared.UploadMemberPhotoAsync(_memberId, photoBytes, contentType);
            ProfilePhoto.Source = ImageSource.FromStream(() => new MemoryStream(photoBytes, writable: false));
            Notice.Show("Photo mise à jour pour les membres de l'association.", true);
        }
        catch (ApiException exception)
        {
            Notice.Show(exception.Message);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_profile is null || !Money.TryParse(ContributionField.Text, out decimal contribution) ||
            contribution < 0 || decimal.Truncate(contribution) != contribution)
        {
            ContributionField.ErrorText = "Saisissez un montant entier positif en FCFA.";
            return;
        }
        try
        {
            SaveButton.IsEnabled = false;
            _profile = await ApiService.Shared.UpdateMemberAsync(_memberId, new MemberUpdateRequest(
                NameField.Text?.Trim() ?? string.Empty,
                EmptyToNull(PhoneField.Text),
                EmptyToNull(EmailField.Text),
                EmptyToNull(NeighborhoodField.Text),
                EmptyToNull(OccupationField.Text),
                contribution));
            ProfileTitle.Text = _profile.FullName;
            Notice.Show("Le profil partagé a été mis à jour.", true);
        }
        catch (ApiException exception)
        {
            Notice.Show(exception.Message);
        }
        finally
        {
            SaveButton.IsEnabled = _profile?.CanEdit == true;
        }
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async void OnBackClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
