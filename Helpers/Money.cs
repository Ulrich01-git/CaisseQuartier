using System.Globalization;

namespace CaisseQuartier.Helpers;

/// <summary>Formatage et lecture des montants en FCFA.</summary>
public static class Money
{
    /// <summary>15000 -> "15 000 FCFA" (espaces insécables, sans décimales).</summary>
    public static string Fcfa(decimal amount)
    {
        string digits = Math.Abs(amount)
            .ToString("N0", CultureInfo.InvariantCulture)
            .Replace(',', '\u00A0');

        return (amount < 0 ? "-" : string.Empty) + digits + "\u00A0FCFA";
    }

    /// <summary>Lit une saisie utilisateur ("15 000", "2500,50", "2500 FCFA"...).</summary>
    public static bool TryParse(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string cleaned = text
            .Replace("FCFA", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("\u00A0", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(',', '.');

        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
