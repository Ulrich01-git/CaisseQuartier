using System.Collections.ObjectModel;

namespace CaisseQuartier;

public partial class RemindersPage : ContentPage
{
    private readonly ObservableCollection<ReminderItem> _reminders = new()
    {
        new() { Name = "Awa Sarr", Description = "Cotisation du mois de juin", Date = "Échéance: 15 juin", IsLate = true },
        new() { Name = "Moussa Diop", Description = "Cotisation du mois de juin", Date = "Échéance: 20 juin", IsLate = false },
        new() { Name = "Ibrahima Fall", Description = "Cotisation du mois de juin", Date = "Échéance: 27 juin", IsLate = true }
    };

    public RemindersPage()
    {
        InitializeComponent();
        RemindersList.ItemsSource = _reminders;
    }

    private async void OnReminderClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Relance", "Une relance a été préparée pour ce membre.", "OK");
    }

    private class ReminderItem
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public bool IsLate { get; set; }
    }
}
