using System.Globalization;
using System.Text;

namespace CaisseQuartier.Api;

public sealed class PdfReport
{
    public byte[] CreateProjectReport(AssociationInfo association, DateTime generatedAt)
    {
        string[] pageOne =
        [
            Clean(association.Name),
            "Rapport du projet CaisseQuartier - Bilan d'avancement",
            $"Document de presentation genere le {generatedAt:dd/MM/yyyy}",
            string.Empty,
            "1. CONTEXTE ET FINALITE",
            "CaisseQuartier est une application mobile de gestion partagee de la caisse",
            "d'une association de quartier. Elle vise une gestion collective, lisible et",
            "adaptee aux usages locaux, avec des montants exprimes en FCFA.",
            string.Empty,
            "PROJET COLLECTIF DE L'ASSOCIATION",
            .. Wrap(Truncate(Clean(association.Purpose), 280), 96),
            string.Empty,
            "2. OBJECTIFS DU PROJET",
            "- Centraliser les informations des membres et faciliter leur consultation.",
            "- Enregistrer les cotisations et depenses dans un historique partage.",
            "- Permettre le suivi d'un objectif commun defini par l'association.",
            "- Renforcer la transparence grace au tableau de bord et aux rapports.",
            "- Proposer une utilisation sur Android avec un serveur local sur le PC.",
            string.Empty,
            "3. TAUX DE REALISATION",
            "Avancement fonctionnel estime : 87,5 % (7 lots realises sur 8).",
            "Calcul : 7 lots fonctionnels implementes / 8 lots de reference x 100.",
            "Les 7 lots realises couvrent l'interface, l'API et SQLite, l'inscription,",
            "les profils membres, les operations financieres, les rapports periodiques",
            "et l'installation Android avec verification de l'acces au serveur.",
            "Le dernier lot, la validation complete avec les comptes reels de plusieurs",
            "membres de l'association, reste a effectuer. Ce taux mesure l'avancement",
            "de l'implementation; il ne certifie pas une recette utilisateur complete.",
            string.Empty,
            "4. PRINCIPALES FONCTIONNALITES LIVREES",
            "- Comptes individuels et creation ou adhesion a une association par code.",
            "- Profils partages, modification des informations et photo JPEG/PNG.",
            "- Cotisations, depenses, activites, synthese financiere et exports PDF.",
            "- Donnees conservees localement dans une base SQLite sur le PC serveur.",
            "Suite du bilan et difficultes rencontrees : page suivante."
        ];

        string[] pageTwo =
        [
            Clean(association.Name),
            "Bilan d'avancement - Difficultes et perspectives",
            $"Suite du rapport genere le {generatedAt:dd/MM/yyyy}",
            string.Empty,
            "5. DIFFICULTES RENCONTREES ET REPONSES APPORTEES",
            "Distinction entre creation d'une association et adhesion : le formulaire",
            "initial pouvait interpreter un nom comme un code d'invitation. Deux choix",
            "explicites ont ete ajoutes pour eviter cette confusion.",
            string.Empty,
            "Navigation Android : une navigation relative provoquait la fermeture de",
            "l'application. Les liens vers les ecrans d'entree ont ete corriges, puis",
            "le parcours a ete verifie sur le telephone.",
            string.Empty,
            "Connexion au serveur : le telephone doit joindre le PC qui conserve la",
            "base. Un tunnel USB a permis le test sans ouvrir le pare-feu sur le Wi-Fi",
            "public. Le cable, le debogage USB et le serveur doivent rester actifs.",
            string.Empty,
            "Validation fonctionnelle : l'installation et l'acces au serveur sont",
            "confirmes, mais la creation de l'association avec les donnees definitives",
            "et le test complet avec plusieurs comptes restent a realiser.",
            string.Empty,
            "6. LIMITES ET CONDITIONS D'UTILISATION",
            "- Le serveur local et le PC doivent rester allumes pendant l'utilisation.",
            "- En USB, le telephone doit rester branche et le tunnel ADB actif.",
            "- La version actuelle n'est pas un hebergement Internet permanent.",
            "- Prevoir une sauvegarde de la base et securiser l'hebergement avant",
            "  tout usage a grande echelle ou contenant des donnees sensibles.",
            string.Empty,
            "7. PROCHAINES ETAPES",
            "1. Creer l'association avec ses informations reelles et verifier le code.",
            "2. Faire rejoindre un second membre et confirmer le partage des donnees.",
            "3. Tester cotisation, depense et generation des rapports sur le telephone.",
            "4. Organiser une sauvegarde reguliere et documenter l'administration.",
            string.Empty,
            "CONCLUSION",
            "Le socle fonctionnel de CaisseQuartier est en place : il soutient un projet",
            "collectif, rend les operations consultables par les membres et produit des",
            "rapports. La recette avec les utilisateurs et la sauvegarde restent les",
            "priorites avant une utilisation reguliere par toute l'association."
        ];

        return BuildPdf([pageOne, pageTwo]);
    }

    public byte[] Create(
        AssociationInfo? association,
        IReadOnlyList<TransactionInfo> transactions,
        DateOnly from,
        DateOnly to,
        string? type,
        string? period)
    {
        var included = transactions
            .Where(row => string.IsNullOrWhiteSpace(type) || row.Type == type)
            .OrderBy(row => row.Date)
            .ThenBy(row => row.CreatedAt)
            .ToList();
        var contributions = included.Where(row => row.Type == "contribution").Sum(row => row.Amount);
        var expenses = included.Where(row => row.Type == "expense").Sum(row => row.Amount);
        string reportType = type switch
        {
            "contribution" => "Cotisations",
            "expense" => "Depenses",
            "event" => "Evenements",
            _ => "Activite complete"
        };

        var rows = new List<string>
        {
            Clean(association?.Name ?? "CaisseQuartier"),
            "Rapport periodique - " + reportType,
            $"Periode : {from:dd/MM/yyyy} au {to:dd/MM/yyyy}" +
                (string.IsNullOrWhiteSpace(period) ? string.Empty : " (" + Clean(period) + ")"),
            $"Cotisations : {Money(contributions)} FCFA",
            $"Depenses : {Money(expenses)} FCFA",
            $"Solde net : {Money(contributions - expenses)} FCFA",
            $"Operations : {included.Count}",
            string.Empty,
            "DATE       TYPE          MEMBRE / LIBELLE                     MONTANT FCFA"
        };

        rows.AddRange(included.Select(row =>
        {
            var label = row.Type == "contribution"
                ? $"{row.MemberName} - {row.Title}"
                : $"{row.Title} - enregistre par {row.RecordedBy}";
            string transactionType = row.Type switch
            {
                "contribution" => "Cotisation",
                "expense" => "Depense",
                _ => "Evenement"
            };
            return $"{row.Date:dd/MM/yyyy}  {transactionType,-12}  " +
                   $"{Truncate(Clean(label), 36),-36}  {Money(row.Amount),12}";
        }));

        if (included.Count == 0) rows.Add("Aucune operation sur la periode selectionnee.");
        rows.Add(string.Empty);
        rows.Add("Document genere par CaisseQuartier. Les informations refletent les donnees");
        rows.Add("enregistrees par les membres de l'association dans la base partagee.");

        var pages = rows.Chunk(43).ToArray();
        return BuildPdf(pages);
    }

    private static byte[] BuildPdf(IReadOnlyList<string[]> pages)
    {
        var objects = new List<string>();
        int pageCount = pages.Count;
        int regularFontId = 3 + pageCount * 2;
        int boldFontId = regularFontId + 1;

        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
        objects.Add("<< /Type /Pages /Kids [" +
                    string.Join(" ", Enumerable.Range(0, pageCount).Select(index => $"{3 + index * 2} 0 R")) +
                    $"] /Count {pageCount} >>");

        for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            int pageId = 3 + pageIndex * 2;
            int streamId = pageId + 1;
            string content = BuildPageContent(pages[pageIndex], pageIndex + 1, pageCount);
            int length = Encoding.ASCII.GetByteCount(content);
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] " +
                        $"/Resources << /Font << /F1 {regularFontId} 0 R /F2 {boldFontId} 0 R >> >> " +
                        $"/Contents {streamId} 0 R >>");
            objects.Add($"<< /Length {length} >>\nstream\n{content}\nendstream");
        }

        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        var output = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        foreach (var item in objects)
        {
            offsets.Add(output.Length);
            output.Append(offsets.Count - 1).Append(" 0 obj\n").Append(item).Append("\nendobj\n");
        }

        int xrefOffset = output.Length;
        output.Append("xref\n0 ").Append(objects.Count + 1).Append("\n");
        output.Append("0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
            output.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        output.Append("trailer\n<< /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\n");
        output.Append("startxref\n").Append(xrefOffset).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(output.ToString());
    }

    private static string BuildPageContent(IReadOnlyList<string> lines, int pageNumber, int pageCount)
    {
        var content = new StringBuilder();
        content.Append("0.10 0.23 0.37 rg\n");
        AddText(content, lines.FirstOrDefault() ?? "CaisseQuartier", 42, 795, 19, true);
        content.Append("0.25 0.36 0.47 rg\n");
        AddText(content, lines.ElementAtOrDefault(1) ?? "Rapport", 42, 770, 12, true);
        content.Append("0.12 0.16 0.20 rg\n");

        for (int index = 2; index < lines.Count; index++)
        {
            if (string.IsNullOrEmpty(lines[index]))
                continue;
            AddText(content, lines[index], 42, 738 - (index - 2) * 15, index == 2 ? 10 : 8.5, false);
        }

        content.Append("0.65 0.68 0.72 RG 0.5 w 42 34 m 553 34 l S\n");
        content.Append("0.35 0.39 0.44 rg\n");
        AddText(content, $"CaisseQuartier - page {pageNumber}/{pageCount}", 42, 20, 8, false);
        return content.ToString();
    }

    private static void AddText(StringBuilder content, string value, double x, double y, double size, bool bold)
    {
        var font = bold ? "F2" : "F1";
        content.Append("BT /").Append(font).Append(' ')
            .Append(size.ToString("0.##", CultureInfo.InvariantCulture)).Append(" Tf ")
            .Append(x.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ')
            .Append(y.ToString("0.##", CultureInfo.InvariantCulture))
            .Append(" Td (").Append(Escape(value)).Append(") Tj ET\n");
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("(", "\\(", StringComparison.Ordinal)
             .Replace(")", "\\)", StringComparison.Ordinal);

    private static string Clean(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;
            result.Append(character is >= ' ' and <= '~' ? character : '?');
        }
        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..(length - 3)] + "...";

    private static IEnumerable<string> Wrap(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield return "Objectif collectif a preciser par l'association.";
            yield break;
        }

        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();
        foreach (var word in words)
        {
            if (line.Length > 0 && line.Length + word.Length + 1 > maxLength)
            {
                yield return line.ToString();
                line.Clear();
            }

            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }

        if (line.Length > 0) yield return line.ToString();
    }

    private static string Money(decimal value) =>
        value.ToString("#,##0", CultureInfo.GetCultureInfo("fr-FR")).Replace('\u202f', ' ');
}
