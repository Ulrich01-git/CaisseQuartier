using CaisseQuartier.Views;

namespace CaisseQuartier;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(NouveauMembrePage), typeof(NouveauMembrePage));
        Routing.RegisterRoute(nameof(MembreDetailPage), typeof(MembreDetailPage));
        Routing.RegisterRoute(nameof(AssociationPage), typeof(AssociationPage));
        Routing.RegisterRoute(nameof(MembresPage), typeof(MembresPage));
        Routing.RegisterRoute(nameof(NouvelleCotisationPage), typeof(NouvelleCotisationPage));
        Routing.RegisterRoute(nameof(DepensesPage), typeof(DepensesPage));
        Routing.RegisterRoute(nameof(RapportPage), typeof(RapportPage));
        Routing.RegisterRoute(nameof(RelancesPage), typeof(RelancesPage));
    }
}
