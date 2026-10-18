using System.Collections.ObjectModel;

namespace CaisseQuartier;

public partial class ExpensesPage : ContentPage
{
    private readonly ObservableCollection<ExpenseItem> _expenses = new()
    {
        new() { Name = "Salle de réunion", Category = "Logistique", Amount = "120 000", Date = "12 juin 2026" },
        new() { Name = "Transport", Category = "Mobilité", Amount = "80 000", Date = "09 juin 2026" },
        new() { Name = "Matériel", Category = "Gestion", Amount = "280 000", Date = "03 juin 2026" }
    };

    public ExpensesPage()
    {
        InitializeComponent();
        ExpensesList.ItemsSource = _expenses;
    }

    private async void OnAddExpenseClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Dépense", "Ajout d'une nouvelle dépense à venir.", "OK");
    }

    private class ExpenseItem
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
    }
}
