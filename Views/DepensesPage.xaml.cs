using CaisseQuartier.Controls;
using CaisseQuartier.Helpers;
using CaisseQuartier.Models;
using CaisseQuartier.Services;

namespace CaisseQuartier.Views;

public partial class DepensesPage : ContentPage
{
    private IReadOnlyList<TransactionInfo> _expenses = [];

    public DepensesPage()
    {
        InitializeComponent();
        ExpenseDateField.Date = DateTime.Today;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        Loading.IsVisible = true;
        ListCard.IsVisible = false;
        EmptyState.IsVisible = false;
        TotalLabel.Text = "—";
        CountLabel.Text = "Synchronisation…";
        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstDay = new DateOnly(today.Year, today.Month, 1);
        PeriodCaption.Text = $"Depuis le {firstDay:dd/MM/yyyy} · FCFA";
        try
        {
            _expenses = await ApiService.Shared.GetTransactionsAsync(firstDay, today, "expense");
            Render();
        }
        catch (ApiException exception)
        {
            Notice.Show(exception.Message);
            EmptyState.Title = "Dépenses indisponibles";
            EmptyState.Message = "Le serveur n'a pas pu charger les dépenses partagées.";
            EmptyState.IsVisible = true;
        }
        finally
        {
            Loading.IsVisible = false;
        }
    }

    private void Render()
    {
        decimal total = _expenses.Sum(expense => expense.Amount);
        TotalLabel.Text = Money.Fcfa(total);
        CountLabel.Text = _expenses.Count switch
        {
            0 => "Aucune dépense ce mois-ci",
            1 => "1 dépense enregistrée ce mois-ci",
            _ => $"{_expenses.Count} dépenses enregistrées ce mois-ci"
        };

        if (_expenses.Count == 0)
        {
            EmptyState.IsVisible = true;
            ListCard.IsVisible = false;
            return;
        }

        EmptyState.IsVisible = false;
        var stack = new VerticalStackLayout { Spacing = 0 };
        for (int index = 0; index < _expenses.Count; index++)
            stack.Add(RowFactory.ExpenseRow(_expenses[index], index > 0));
        ListCard.Content = stack;
        ListCard.IsVisible = true;
    }

    private void OnAddClicked(object? sender, EventArgs e)
    {
        TitleField.Text = string.Empty;
        DescriptionField.Text = string.Empty;
        AmountField.Text = string.Empty;
        TitleField.ErrorText = string.Empty;
        AmountField.ErrorText = string.Empty;
        ExpenseDateField.Date = DateTime.Today;
        AddSheet.Open();
    }

    private async void OnConfirmAddClicked(object? sender, EventArgs e)
    {
        string title = TitleField.Text?.Trim() ?? string.Empty;
        bool amountOk = Money.TryParse(AmountField.Text, out decimal amount) &&
                        amount > 0 && decimal.Truncate(amount) == amount;
        TitleField.ErrorText = title.Length < 2 ? "Décrivez la dépense" : string.Empty;
        AmountField.ErrorText = amountOk ? string.Empty : "Saisissez un montant entier en FCFA supérieur à 0.";
        if (title.Length < 2 || !amountOk) return;

        try
        {
            await ApiService.Shared.AddExpenseAsync(new TransactionCreateRequest(
                Guid.Empty, title, DescriptionField.Text?.Trim(), amount,
                DateOnly.FromDateTime(ExpenseDateField.Date)));
            AddSheet.Close();
            Notice.Show($"Dépense « {title} » enregistrée et partagée.", true);
            await LoadAsync();
        }
        catch (ApiException exception)
        {
            Notice.Show(exception.Message);
        }
    }

    private void OnCancelAddClicked(object? sender, EventArgs e) => AddSheet.Close();

    private async void OnBackClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
