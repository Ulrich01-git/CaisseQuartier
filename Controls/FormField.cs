using System;

namespace CaisseQuartier.Controls;

public partial class FormField : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(FormField), string.Empty, propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(FormField), string.Empty, propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(FormField), string.Empty, propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty IsPasswordProperty =
        BindableProperty.Create(nameof(IsPassword), typeof(bool), typeof(FormField), false, propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty ErrorTextProperty =
        BindableProperty.Create(nameof(ErrorText), typeof(string), typeof(FormField), string.Empty, propertyChanged: OnVisualPropertyChanged);

    public FormField()
    {
        InitializeComponent();
        ValueEntry.TextChanged += (_, e) => Text = e.NewTextValue ?? string.Empty;
        UpdateState();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsPassword
    {
        get => (bool)GetValue(IsPasswordProperty);
        set => SetValue(IsPasswordProperty, value);
    }

    public string ErrorText
    {
        get => (string)GetValue(ErrorTextProperty);
        set => SetValue(ErrorTextProperty, value);
    }

    private static void OnVisualPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((FormField)bindable).UpdateState();
    }

    private void UpdateState()
    {
        TitleLabel.Text = Title;
        ValueEntry.Placeholder = Placeholder;
        ValueEntry.Text = Text;
        ValueEntry.IsPassword = IsPassword;
        TogglePasswordButton.IsVisible = IsPassword;
        TogglePasswordButton.Text = ValueEntry.IsPassword ? "Afficher" : "Masquer";
        ErrorLabel.Text = ErrorText;
        ErrorLabel.IsVisible = !string.IsNullOrWhiteSpace(ErrorText);
        InputBorder.Stroke = string.IsNullOrWhiteSpace(ErrorText) ? Color.FromArgb("#D9E2EC") : Color.FromArgb("#EF4444");
    }

    private void OnPasswordToggleClicked(object sender, EventArgs e)
    {
        ValueEntry.IsPassword = !ValueEntry.IsPassword;
        TogglePasswordButton.Text = ValueEntry.IsPassword ? "Afficher" : "Masquer";
    }
}
