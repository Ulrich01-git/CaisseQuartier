using System.Collections.ObjectModel;

namespace CaisseQuartier;

public partial class MembersPage : ContentPage
{
    private readonly ObservableCollection<MemberItem> _members = new()
    {
        new() { Name = "Moussa Diop", Phone = "+221 77 123 45 67", Initials = "MD", IsLate = false, Amount = "50 000" },
        new() { Name = "Awa Sarr", Phone = "+221 76 987 12 10", Initials = "AS", IsLate = true, Amount = "100 000" },
        new() { Name = "Ibrahima Fall", Phone = "+221 70 654 98 77", Initials = "IF", IsLate = false, Amount = "50 000" },
        new() { Name = "Seynabou Ndiaye", Phone = "+221 78 441 55 99", Initials = "SN", IsLate = false, Amount = "50 000" }
    };

    public MembersPage()
    {
        InitializeComponent();
        MembersList.ItemsSource = _members;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var query = e.NewTextValue ?? string.Empty;
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _members
            : new ObservableCollection<MemberItem>(_members.Where(m => m.Name.Contains(query, StringComparison.OrdinalIgnoreCase)));

        MembersList.ItemsSource = filtered;
    }

    private async void OnAddMemberClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Membre", "Ajouter un nouveau membre depuis cette action.", "OK");
    }

    private class MemberItem
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public bool IsLate { get; set; }
        public string Amount { get; set; } = string.Empty;
    }
}
