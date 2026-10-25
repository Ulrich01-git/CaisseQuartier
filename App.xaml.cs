using CaisseQuartier.Helpers;

namespace CaisseQuartier;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // L'application suit une charte claire unique (maquettes en thème clair).
        UserAppTheme = AppTheme.Light;
        PlatformTweaks.Apply();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Fenêtre au format téléphone (sert aussi aux captures et à la vidéo de démonstration)
        return new Window(new AppShell())
        {
            Title = "CaisseQuartier",
            Width = 420,
            Height = 880
        };
    }
}
