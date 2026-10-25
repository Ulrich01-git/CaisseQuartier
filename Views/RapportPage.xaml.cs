using CaisseQuartier.Controls;
using CaisseQuartier.Helpers;
using CaisseQuartier.Models;
using CaisseQuartier.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;

namespace CaisseQuartier.Views;

public partial class RapportPage : ContentPage
{
    private static readonly string[] PeriodNames =
    [
        "Aujourd'hui", "Cette semaine", "14 derniers jours", "Ce mois-ci",
        "2 mois", "3 mois", "4 mois", "5 mois", "6 mois", "Période personnalisée"
    ];

    private static readonly string[] ReportTypes =
        ["Toutes les activités", "Cotisations", "Dépenses", "Événements"];

    private int _loadVersion;
    private bool _initialized;
    private bool _updatingDates;
    private DateOnly _from;
    private DateOnly _to;
    private string _periodLabel = string.Empty;
    private string _typeKey = string.Empty;
    private IReadOnlyList<TransactionInfo> _transactions = [];
    private IReadOnlyList<AuditEventInfo> _events = [];

    public RapportPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_initialized)
        {
            PeriodPicker.ItemsSource = PeriodNames;
            TypePicker.ItemsSource = ReportTypes;
            PeriodPicker.SelectedIndex = 3;
            TypePicker.SelectedIndex = 0;
            _initialized = true;
            ApplySelectedPeriod();
        }
        await LoadAsync();
    }

    private async void OnPeriodChanged(object? sender, EventArgs e)
    {
        if (!_initialized || PeriodPicker.SelectedIndex < 0) return;
        ApplySelectedPeriod();
        await LoadAsync();
    }

    private async void OnTypeChanged(object? sender, EventArgs e)
    {
        if (!_initialized || TypePicker.SelectedIndex < 0) return;
        _typeKey = TypePicker.SelectedIndex switch
        {
            1 => "contribution",
            2 => "expense",
            3 => "event",
            _ => string.Empty
        };
        await LoadAsync();
    }

    private async void OnDateChanged(object? sender, DateTime date)
    {
        if (!_initialized || _updatingDates || PeriodPicker.SelectedIndex != PeriodNames.Length - 1) return;
        await LoadAsync();
    }

    private void ApplySelectedPeriod()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        int index = PeriodPicker.SelectedIndex;
        _periodLabel = PeriodNames[index];
        if (index == PeriodNames.Length - 1)
        {
            if (_from == default)
            {
                _from = new DateOnly(today.Year, today.Month, 1);
                _to = today;
                SetDateInputs(_from, _to);
            }
            return;
        }

        _to = today;
        _from = index switch
        {
            0 => today,
            1 => today.AddDays(-(((int)today.DayOfWeek + 6) % 7)),
            2 => today.AddDays(-13),
            3 => new DateOnly(today.Year, today.Month, 1),
            _ => new DateOnly(today.Year, today.Month, 1).AddMonths(-(index - 3))
        };
        SetDateInputs(_from, _to);
    }

    private void SetDateInputs(DateOnly from, DateOnly to)
    {
        _updatingDates = true;
        try
        {
            StartInput.Date = from.ToDateTime(TimeOnly.MinValue);
            EndInput.Date = to.ToDateTime(TimeOnly.MinValue);
        }
        finally
        {
            _updatingDates = false;
        }
    }

    private async Task LoadAsync()
    {
        int version = ++_loadVersion;
        _from = DateOnly.FromDateTime(StartInput.Date);
        _to = DateOnly.FromDateTime(EndInput.Date);
        if (_from > _to)
        {
            RangeError.IsVisible = true;
            LoadingCard.IsVisible = false;
            ReportSection.IsVisible = false;
            ExportButton.IsEnabled = false;
            return;
        }

        RangeError.IsVisible = false;
        ReportSection.IsVisible = false;
        LoadingCard.IsVisible = true;
        ExportButton.IsEnabled = false;
        try
        {
            if (_typeKey == "event")
            {
                _events = await ApiService.Shared.GetEventsAsync(_from, _to);
                _transactions = [];
            }
            else
            {
                _transactions = await ApiService.Shared.GetTransactionsAsync(
                    _from, _to, string.IsNullOrEmpty(_typeKey) ? null : _typeKey);
                _events = _typeKey.Length == 0
                    ? await ApiService.Shared.GetEventsAsync(_from, _to)
                    : [];
            }
            if (version != _loadVersion) return;
            Render();
            ExportButton.IsEnabled = true;
        }
        catch (ApiException exception)
        {
            if (version != _loadVersion) return;
            Notice.Show(exception.Message);
        }
        finally
        {
            if (version == _loadVersion)
                LoadingCard.IsVisible = false;
        }
    }

    private void Render()
    {
        decimal contributions = _transactions.Where(item => item.Type == "contribution").Sum(item => item.Amount);
        decimal expenses = _transactions.Where(item => item.Type == "expense").Sum(item => item.Amount);
        ContributionsValue.Text = Money.Fcfa(contributions);
        ExpensesValue.Text = Money.Fcfa(expenses);
        NetValue.Text = Money.Fcfa(contributions - expenses);
        CountLabel.Text = $"{_transactions.Count + _events.Count} activité(s) enregistrée(s) · {_periodLabel}";
        ReportTitle.Text = $"{TypePicker.SelectedItem} · {_periodLabel} · FCFA";

        bool negative = contributions - expenses < 0;
        NetBox.BackgroundColor = Theme.Get(negative ? "CqRedSoft" : "CqGreenSoft");
        NetCaption.TextColor = Theme.Get(negative ? "CqRed" : "CqGreen");
        NetValue.TextColor = Theme.Get(negative ? "CqRed" : "CqGreen");

        var activity = new VerticalStackLayout { Spacing = 0 };
        var entries = _transactions.Select(transaction => new
            {
                Date = transaction.Date.ToDateTime(TimeOnly.MinValue),
                Title = transaction.Type == "contribution"
                    ? $"{transaction.MemberName} · {transaction.Title}"
                    : $"{transaction.Title} · {transaction.RecordedBy}",
                Detail = $"{transaction.Date:dd/MM/yyyy} · {Money.Fcfa(transaction.Amount)}"
            })
            .Concat(_events.Select(item => new
            {
                Date = item.CreatedAt,
                Title = item.Summary,
                Detail = $"{item.CreatedAt:dd/MM/yyyy HH:mm} · {item.ActorName}"
            }))
            .OrderByDescending(item => item.Date)
            .Take(30)
            .ToList();

        for (int index = 0; index < entries.Count; index++)
        {
            if (index > 0) activity.Add(new BoxView { HeightRequest = 1, Color = Theme.Get("CqBorder") });
            activity.Add(new VerticalStackLayout
            {
                Padding = new Thickness(4, 10),
                Spacing = 3,
                Children =
                {
                    new Label { Text = entries[index].Title, FontSize = 14, FontAttributes = FontAttributes.Bold,
                        TextColor = Theme.Get("CqText") },
                    new Label { Text = entries[index].Detail, FontSize = 12, TextColor = Theme.Get("CqTextMuted") }
                }
            });
        }
        ActivityCard.Content = activity;
        ActivitySection.IsVisible = entries.Count > 0;
        EmptyState.IsVisible = entries.Count == 0;
        NoteLabel.Text = "Les rapports sont établis à partir des cotisations, dépenses et événements réellement synchronisés.";
        ReportSection.IsVisible = true;
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        try
        {
            ExportButton.IsEnabled = false;
            var pdf = await ApiService.Shared.DownloadReportAsync(_from, _to, _typeKey, _periodLabel);
            var fileName = $"CaisseQuartier-{DateTime.Now:yyyyMMdd-HHmmss}.pdf";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllBytesAsync(filePath, pdf);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Partager le rapport CaisseQuartier",
                File = new ShareFile(filePath, "application/pdf")
            });
        }
        catch (ApiException exception)
        {
            Notice.Show(exception.Message);
        }
        finally
        {
            ExportButton.IsEnabled = true;
        }
    }

    private async void OnProjectReportClicked(object? sender, EventArgs e)
    {
        try
        {
            ProjectReportButton.IsEnabled = false;
            var pdf = await ApiService.Shared.DownloadProjectReportAsync();
            var filePath = Path.Combine(FileSystem.CacheDirectory, "CaisseQuartier-Rapport-du-projet.pdf");
            await File.WriteAllBytesAsync(filePath, pdf);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Rapport du projet CaisseQuartier",
                File = new ShareFile(filePath, "application/pdf")
            });
        }
        catch (ApiException exception)
        {
            Notice.Show(exception.Message);
        }
        finally
        {
            ProjectReportButton.IsEnabled = true;
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
