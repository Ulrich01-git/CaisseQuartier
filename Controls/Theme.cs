namespace CaisseQuartier.Controls;

/// <summary>Accès aux couleurs de la charte (CqColors.xaml) depuis le code.</summary>
public static class Theme
{
    public static Color Get(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Colors.Gray;
}
