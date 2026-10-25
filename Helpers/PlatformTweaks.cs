using Microsoft.Maui.Handlers;

namespace CaisseQuartier.Helpers;

/// <summary>
/// Retire le cadre natif des champs de saisie (Entry) pour que seul
/// le cadre arrondi de la charte graphique soit visible.
/// </summary>
public static class PlatformTweaks
{
    private static bool _applied;

    public static void Apply()
    {
        if (_applied) return;
        _applied = true;

        EntryHandler.Mapper.AppendToMapping("CqBareEntry", (handler, view) =>
        {
#if ANDROID
            handler.PlatformView.BackgroundTintList =
                Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#elif IOS || MACCATALYST
            handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
#elif WINDOWS
            var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
            handler.PlatformView.Background = transparent;
            handler.PlatformView.Resources["TextControlBackgroundPointerOver"] = transparent;
            handler.PlatformView.Resources["TextControlBackgroundFocused"] = transparent;
            handler.PlatformView.Resources["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
#endif
        });
    }
}
