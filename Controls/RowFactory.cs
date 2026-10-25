using CaisseQuartier.Helpers;
using CaisseQuartier.Models;
using Microsoft.Maui.Controls.Shapes;

namespace CaisseQuartier.Controls;

/// <summary>Construit les lignes de listes (membres, dépenses) et les cartes de relance.</summary>
public static class RowFactory
{
    public static Border Card(double padding = 0) => new()
    {
        BackgroundColor = Theme.Get("CqSurface"),
        Stroke = new SolidColorBrush(Theme.Get("CqBorder")),
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
        Padding = new Thickness(padding)
    };

    private static Label MakeLabel(string text, double size, string colorKey, bool bold = false)
    {
        var label = new Label { Text = text, FontSize = size, TextColor = Theme.Get(colorKey) };
        if (bold) label.FontAttributes = FontAttributes.Bold;
        return label;
    }

    private static View WithSeparator(View content, bool separator)
    {
        var stack = new VerticalStackLayout { Spacing = 0 };
        if (separator)
            stack.Add(new BoxView { HeightRequest = 1, Color = Theme.Get("CqBorder") });
        stack.Add(content);
        return stack;
    }

    private static Grid TwoColumns(bool padded = false)
    {
        var grid = new Grid { Padding = padded ? new Thickness(16, 12) : new Thickness(0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        return grid;
    }

    public static View MemberRow(
        MemberProfile member, bool separator, Action<MemberProfile> onTap)
    {
        Grid grid = TwoColumns(true);

        var info = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        info.Add(MakeLabel(member.Name, 15, "CqText", true));
        info.Add(MakeLabel(member.AmountLabel, 12, "CqTextMuted"));

        grid.Add(info, 0, 0);
        grid.Add(new StatusBadge { IsLate = member.IsLate, VerticalOptions = LayoutOptions.Center }, 1, 0);
        grid.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => onTap(member)) });
        SemanticProperties.SetHint(grid, "Ouvrir le profil de " + member.FullName);
        return WithSeparator(grid, separator);
    }

    public static View ExpenseRow(TransactionInfo expense, bool separator)
    {
        Grid grid = TwoColumns(true);

        var info = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        info.Add(MakeLabel(expense.Title, 15, "CqText", true));
        info.Add(MakeLabel(expense.DateLabel, 12, "CqTextMuted"));

        var amount = MakeLabel(expense.AmountLabel, 15, "CqText", true);
        amount.VerticalOptions = LayoutOptions.Start;

        grid.Add(info, 0, 0);
        grid.Add(amount, 1, 0);
        return WithSeparator(grid, separator);
    }

    public static View RelanceCard(MemberProfile member, Action<MemberProfile, ActionButton> onSend)
    {
        Border card = Card(16);
        var body = new VerticalStackLayout { Spacing = 12 };

        Grid top = TwoColumns();
        var name = MakeLabel(member.Name, 15, "CqText", true);
        name.VerticalOptions = LayoutOptions.Center;
        top.Add(name, 0, 0);
        top.Add(new StatusBadge { IsLate = true, VerticalOptions = LayoutOptions.Center }, 1, 0);

        Grid due = TwoColumns();
        due.Add(MakeLabel("Montant dû", 14, "CqTextMuted"), 0, 0);
        due.Add(MakeLabel(Money.Fcfa(member.Due), 15, "CqRed", true), 1, 0);

        var send = new ActionButton
        {
            Text = "Envoyer une relance",
            IconSource = "icon_send_white.png",
            Kind = "Primary"
        };
        send.Clicked += (_, _) => onSend(member, send);

        body.Add(top);
        body.Add(due);
        body.Add(send);
        card.Content = body;
        return card;
    }
}
